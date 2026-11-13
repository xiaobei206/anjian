
using System.Net;
using System.Text;
using System.Text.Json;
using Anjian.Core.Api;
using Anjian.Core.Permissions;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScriptHttpTests
{
    [Fact]
    public void PermissionManifestRequiresExactHostMethodAndSeparateHttpApproval()
    {
        var root = Path.GetTempPath();
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["*.example.com"],"httpMethods":["GET"]}""", root));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["api.example.com"],"httpMethods":["TRACE"]}""", root));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"allowInsecureHttp":true}""", root));

        var request = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["api.example.com"],"httpMethods":["GET"],"allowInsecureHttp":true}""", root);
        var grant = ScriptPermissionPolicy.CreateGrant(request,
            new ScriptPermissionApproval
            {
                HttpHosts = ["api.example.com"],
                HttpMethods = ["GET"]
            }, root);

        Assert.True(grant.AllowsHttp(new Uri("https://api.example.com/data"), "GET"));
        Assert.False(grant.AllowsHttp(new Uri("https://sub.api.example.com/data"), "GET"));
        Assert.False(grant.AllowsHttp(new Uri("http://api.example.com/data"), "GET"));
        Assert.Throws<UnauthorizedAccessException>(() => ScriptHttpPolicy.ValidateTarget(
            grant, "http://api.example.com/data", "GET"));

        var insecureGrant = ScriptPermissionPolicy.CreateGrant(request,
            new ScriptPermissionApproval
            {
                HttpHosts = ["api.example.com"],
                HttpMethods = ["GET"],
                AllowInsecureHttp = true
            }, root);
        Assert.True(insecureGrant.AllowsHttp(new Uri("http://api.example.com/data"), "GET"));
    }

    [Fact]
    public void PolicyRejectsUnapprovedMethodPortsUserInfoIpLiteralsAndForbiddenHeaders()
    {
        var grant = Grant(["api.example.com"], ["GET"]);
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptHttpPolicy.ValidateTarget(grant, "https://api.example.com/data", "POST"));
        Assert.Throws<InvalidDataException>(() =>
            ScriptHttpPolicy.ValidateTarget(grant, "https://api.example.com:8443/data", "GET"));
        Assert.Throws<InvalidDataException>(() =>
            ScriptHttpPolicy.ValidateTarget(grant, "https://user:pass@api.example.com/data", "GET"));
        Assert.Throws<InvalidDataException>(() =>
            ScriptHttpPolicy.ValidateTarget(grant, "https://127.0.0.1/data", "GET"));
        Assert.Throws<InvalidDataException>(() =>
            ScriptHttpPolicy.ValidateHeaders(new Dictionary<string, string> { ["Host"] = "evil.example" }));
        Assert.Throws<InvalidDataException>(() =>
            ScriptHttpPolicy.ValidateHeaders(new Dictionary<string, string> { ["X-Test"] = "a\r\nb" }));
    }

    [Fact]
    public void WorkerHttpApiValidatesGrantAndSerializesLuaJsonBeforeIpc()
    {
        var grant = Grant(["api.example.com"], ["POST"]);
        var pipe = new Recorder(
            """{"statusCode":201,"reasonPhrase":"Created","ok":true,"url":"https://api.example.com/items","headers":{"Content-Type":"application/json"},"body":"{\"created\":true}","json":{"created":true}}""");
        using var lua = new Lua(openLibs: true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, permissions: grant));
        var result = lua.DoString("""
            local response = HttpRequest("https://api.example.com/items", {
                method = "POST",
                headers = { ["X-Trace"] = "trace-1" },
                json = { name = "测试", items = { 1, 2, 3 } }
            })
            return response.statusCode, response.ok, response.json.created
            """);

        Assert.Equal(201, Convert.ToInt32(result[0]));
        Assert.True(Convert.ToBoolean(result[1]));
        Assert.True(Convert.ToBoolean(result[2]));
        var message = Assert.Single(pipe.Messages);
        Assert.Equal("http-request", message.Type);
        using var payload = JsonDocument.Parse(message.Text!);
        Assert.Equal("POST", payload.RootElement.GetProperty("method").GetString());
        Assert.Equal("trace-1", payload.RootElement.GetProperty("headers").GetProperty("X-Trace").GetString());
        Assert.Equal("application/json; charset=utf-8",
            payload.RootElement.GetProperty("headers").GetProperty("Content-Type").GetString());
        using var body = JsonDocument.Parse(payload.RootElement.GetProperty("body").GetString()!);
        Assert.Equal("测试", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void WorkerRejectsUnapprovedRequestsAndMalformedLuaJsonBeforeIpc()
    {
        var grant = Grant(["api.example.com"], ["GET"]);
        var pipe = new Recorder("{}");
        using var lua = new Lua(openLibs: true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, permissions: grant));
        var result = lua.DoString("""
            local denied = pcall(function() HttpRequest("https://other.example.com/") end)
            local badMethod = pcall(function() HttpRequest("https://api.example.com/", { method = "POST" }) end)
            local mixed = pcall(function() HttpRequest("https://api.example.com/", { json = { [1] = "a", named = "b" } }) end)
            local unknown = pcall(function() HttpRequest("https://api.example.com/", { proxy = "http://localhost" }) end)
            return denied, badMethod, mixed, unknown
            """);

        Assert.All(result, value => Assert.False(Convert.ToBoolean(value)));
        Assert.Empty(pipe.Messages);
    }

    [Fact]
    public async Task HostRevalidatesForgedIpcAgainstCurrentGrant()
    {
        var payload = JsonSerializer.Serialize(new ScriptHttpRequest
        {
            Url = "https://api.example.com/",
            Method = "GET"
        });
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptHttpPolicy.ParseRequest(ScriptPermissionGrant.Empty, payload));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RedirectReauthorizesDestinationHostAndEffectiveMethod()
    {
        var calls = 0;
        using var session = new ScriptHttpSession(new DelegateHandler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://other.example.com/next");
            return Task.FromResult(response);
        }));
        var grant = Grant(["api.example.com"], ["GET"]);
        var payload = RequestPayload("https://api.example.com/start", "GET");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            session.SendAsync(grant, payload, CancellationToken.None));
        Assert.Equal(1, calls);

        calls = 0;
        using var postSession = new ScriptHttpSession(new DelegateHandler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://api.example.com/after-post");
            return Task.FromResult(response);
        }));
        var postGrant = Grant(["api.example.com"], ["POST"]);
        var postPayload = RequestPayload("https://api.example.com/start", "POST", body: "x");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            postSession.SendAsync(postGrant, postPayload, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CrossOriginRedirectDropsAuthorizationAndCustomCredentialHeaders()
    {
        var seen = new List<(string Method, bool Authorization, bool ApiKey)>();
        using var session = new ScriptHttpSession(new DelegateHandler((request, _) =>
        {
            seen.Add((request.Method.Method,
                request.Headers.Contains("Authorization"),
                request.Headers.Contains("X-Api-Key")));
            if (seen.Count == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri("https://cdn.example.net/resource");
                return Task.FromResult(redirect);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain")
            });
        }));
        var grant = Grant(["api.example.com", "cdn.example.net"], ["GET"]);
        var payload = JsonSerializer.Serialize(new ScriptHttpRequest
        {
            Url = "https://api.example.com/resource",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer secret",
                ["X-Api-Key"] = "secret",
                ["Accept"] = "application/json"
            }
        });

        _ = await session.SendAsync(grant, payload, CancellationToken.None);
        Assert.Equal(2, seen.Count);
        Assert.True(seen[0].Authorization);
        Assert.True(seen[0].ApiKey);
        Assert.False(seen[1].Authorization);
        Assert.False(seen[1].ApiKey);
    }

    [Fact]
    public async Task SessionReturnsHttpErrorsAndParsesJsonWithoutTreatingStatusAsTransportFailure()
    {
        using var session = new ScriptHttpSession(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"temporary"}""", Encoding.UTF8, "application/problem+json")
            })));
        var payload = RequestPayload("https://api.example.com/fail", "GET");
        var responseText = await session.SendAsync(Grant(["api.example.com"], ["GET"]), payload, CancellationToken.None);
        using var response = JsonDocument.Parse(responseText);
        Assert.Equal(500, response.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("temporary", response.RootElement.GetProperty("json").GetProperty("error").GetString());
    }

    [Fact]
    public async Task SessionRejectsOversizedResponseAndStopCancelsInFlightRequest()
    {
        using var oversizedSession = new ScriptHttpSession(new DelegateHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1])
            };
            response.Content.Headers.ContentLength = ScriptHttpPolicy.MaximumResponseBodyBytes + 1;
            return Task.FromResult(response);
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            oversizedSession.SendAsync(Grant(["api.example.com"], ["GET"]),
                RequestPayload("https://api.example.com/large", "GET"), CancellationToken.None));

        using var pendingSession = new ScriptHttpSession(new DelegateHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pendingSession.SendAsync(Grant(["api.example.com"], ["GET"]),
                RequestPayload("https://api.example.com/wait", "GET"), stop.Token));
    }

    private static ScriptPermissionGrant Grant(string[] hosts, string[] methods) =>
        new([], [], false, false, [], [], hosts, methods, false);

    private static string RequestPayload(string url, string method, string? body = null) =>
        JsonSerializer.Serialize(new ScriptHttpRequest
        {
            Url = url,
            Method = method,
            Body = body
        });

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class Recorder(string response) : ILuaCommandPipe
    {
        public List<Message> Messages { get; } = [];
        public void Send(string type, string? name = null, double x = 0, double y = 0,
            int milliseconds = 0, string? text = null) =>
            _ = Request(type, name, x, y, milliseconds, text);

        public string? Request(string type, string? name = null, double x = 0, double y = 0,
            int milliseconds = 0, string? text = null)
        {
            Messages.Add(new Message(type, name, text));
            return response;
        }
    }

    private sealed record Message(string Type, string? Name, string? Text);
}
