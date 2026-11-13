using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Anjian.Core.Permissions;

public sealed class ScriptHttpSession : IDisposable
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ScriptHttpSession Shared { get; } = new();

    private readonly HttpClient _client;

    public ScriptHttpSession(HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? CreateHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<string> SendAsync(
        ScriptPermissionGrant? grant,
        string? payload,
        CancellationToken stopToken)
    {
        var effectiveGrant = grant ?? ScriptPermissionGrant.Empty;
        var request = ScriptHttpPolicy.ParseRequest(effectiveGrant, payload);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        timeout.CancelAfter(request.TimeoutMilliseconds);

        try
        {
            var currentUri = ScriptHttpPolicy.ValidateTarget(effectiveGrant, request.Url, request.Method);
            var currentMethod = request.Method;
            var currentBody = request.Body;
            var currentHeaders = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
            var redirects = 0;

            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                ScriptHttpPolicy.ValidateTarget(effectiveGrant, currentUri, currentMethod);

                using var message = CreateMessage(currentUri, currentMethod, currentHeaders, currentBody);
                using var response = await _client.SendAsync(
                    message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

                if (request.FollowRedirects && ScriptHttpPolicy.IsRedirect(response.StatusCode) &&
                    response.Headers.Location is { } location)
                {
                    if (redirects >= ScriptHttpPolicy.MaximumRedirects)
                        throw new InvalidDataException($"HTTP 重定向次数超过 {ScriptHttpPolicy.MaximumRedirects} 次。");

                    Uri nextUri;
                    try
                    {
                        nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    }
                    catch (UriFormatException ex)
                    {
                        throw new InvalidDataException("HTTP 重定向地址无效。", ex);
                    }

                    var nextMethod = ScriptHttpPolicy.RedirectMethod(response.StatusCode, currentMethod);
                    var methodChanged = !nextMethod.Equals(currentMethod, StringComparison.OrdinalIgnoreCase);
                    var originChanged = !SameOrigin(currentUri, nextUri);
                    if (methodChanged)
                    {
                        currentBody = null;
                        currentHeaders.Remove("Content-Type");
                    }
                    if (originChanged)
                    {
                        foreach (var key in currentHeaders.Keys.ToArray())
                        {
                            if (key.Equals("Accept", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) ||
                                (!methodChanged && key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                                continue;
                            currentHeaders.Remove(key);
                        }
                    }

                    ScriptHttpPolicy.ValidateTarget(effectiveGrant, nextUri, nextMethod);
                    currentUri = nextUri;
                    currentMethod = nextMethod;
                    redirects++;
                    continue;
                }

                return await SerializeResponseAsync(response, currentUri, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!stopToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"HTTP 请求超过 {request.TimeoutMilliseconds}ms 超时上限。");
        }
    }

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseDefaultCredentials = false,
        Credentials = null,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        MaxResponseHeadersLength = ScriptHttpPolicy.MaximumResponseHeaderBytes / 1024,
        MaxConnectionsPerServer = 8,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    };

    private static HttpRequestMessage CreateMessage(
        Uri uri,
        string method,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);
        var contentType = headers.FirstOrDefault(x =>
            x.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

        if (body is not null || !string.IsNullOrEmpty(contentType.Key))
        {
            message.Content = new ByteArrayContent(body is null ? [] : Encoding.UTF8.GetBytes(body));
            if (string.IsNullOrEmpty(contentType.Key) && body is not null)
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain")
                {
                    CharSet = "utf-8"
                };
        }

        foreach (var (name, value) in headers)
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                message.Content ??= new ByteArrayContent([]);
                if (!message.Content.Headers.TryAddWithoutValidation(name, value))
                    throw new InvalidDataException("HTTP Content-Type 请求头无效。");
            }
            else if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                throw new InvalidDataException($"HTTP 请求头 {name} 无法添加。");
            }
        }

        return message;
    }

    private static async Task<string> SerializeResponseAsync(
        HttpResponseMessage response,
        Uri finalUri,
        CancellationToken token)
    {
        var headers = CollectResponseHeaders(response);
        var bodyBytes = await ReadResponseBodyAsync(response, token).ConfigureAwait(false);
        string body;
        try
        {
            body = new UTF8Encoding(false, true).GetString(bodyBytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("HTTP 响应正文不是有效 UTF-8。", ex);
        }

        if (body.Length > 0 && body[0] == '\uFEFF')
            body = body[1..];

        JsonElement? json = null;
        var contentType = response.Content.Headers.ContentType;
        var charset = contentType?.CharSet?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(charset) &&
            !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"HTTP 响应字符集不受支持：{charset}；只接受 UTF-8。");

        var mediaType = contentType?.MediaType;
        if (bodyBytes.Length > 0 && IsJsonMediaType(mediaType))
        {
            try
            {
                using var document = JsonDocument.Parse(body,
                    new JsonDocumentOptions { MaxDepth = ScriptHttpPolicy.MaximumJsonDepth });
                json = document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("服务器声明返回 JSON，但正文不是有效 JSON 或超过嵌套深度。", ex);
            }
        }

        var result = new ScriptHttpResponse(
            (int)response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            response.IsSuccessStatusCode,
            finalUri.AbsoluteUri,
            headers,
            body,
            json);
        return JsonSerializer.Serialize(result, ResponseJsonOptions);
    }

    private static Dictionary<string, string> CollectResponseHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0;
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            if (name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Set-Cookie2", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = string.Join(", ", values);
            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(name) +
                Encoding.UTF8.GetByteCount(value));
            if (result.Count >= ScriptHttpPolicy.MaximumResponseHeaderCount ||
                totalBytes > ScriptHttpPolicy.MaximumResponseHeaderBytes)
                throw new InvalidDataException("HTTP 响应头超过允许的数量或总长度上限。");
            result[name] = value;
        }
        return result;
    }

    private static async Task<byte[]> ReadResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken token)
    {
        if (response.Content.Headers.ContentLength is long declared &&
            declared > ScriptHttpPolicy.MaximumResponseBodyBytes)
            throw new InvalidDataException(
                $"HTTP 响应正文超过 {ScriptHttpPolicy.MaximumResponseBodyBytes} 字节上限。");

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[32 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, token).ConfigureAwait(false);
            if (read == 0)
                break;
            if (buffer.Length + read > ScriptHttpPolicy.MaximumResponseBodyBytes)
                throw new InvalidDataException(
                    $"HTTP 响应正文超过 {ScriptHttpPolicy.MaximumResponseBodyBytes} 字节上限。");
            await buffer.WriteAsync(chunk.AsMemory(0, read), token).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }

    private static bool IsJsonMediaType(string? mediaType) =>
        mediaType is not null &&
        (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
         mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    private static bool SameOrigin(Uri left, Uri right) =>
        left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;

    public void Dispose() => _client.Dispose();
}

public sealed record ScriptHttpResponse(
    int StatusCode,
    string ReasonPhrase,
    bool Ok,
    string Url,
    Dictionary<string, string> Headers,
    string Body,
    JsonElement? Json);
