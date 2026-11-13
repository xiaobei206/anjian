using System.Net;
using System.Text;
using System.Text.Json;

namespace Anjian.Core.Permissions;

public sealed class ScriptHttpRequest
{
    public string Url { get; init; } = "";
    public string Method { get; init; } = "GET";
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; init; }
    public int TimeoutMilliseconds { get; init; } = ScriptHttpPolicy.DefaultTimeoutMilliseconds;
    public bool FollowRedirects { get; init; } = true;
}

public static class ScriptHttpPolicy
{
    public const int MaximumUrlLength = 2_048;
    public const int MaximumHeaderCount = 32;
    public const int MaximumHeaderNameLength = 128;
    public const int MaximumHeaderValueLength = 8_192;
    public const int MaximumHeaderBytes = 32 * 1024;
    public const int MaximumRequestBodyBytes = 1024 * 1024;
    public const int MaximumResponseBodyBytes = 4 * 1024 * 1024;
    public const int MaximumResponseHeaderBytes = 32 * 1024;
    public const int MaximumResponseHeaderCount = 64;
    public const int MaximumRedirects = 5;
    public const int DefaultTimeoutMilliseconds = 30_000;
    public const int MinimumTimeoutMilliseconds = 1_000;
    public const int MaximumTimeoutMilliseconds = 120_000;
    public const int MaximumJsonDepth = 32;

    private static readonly HashSet<string> AllowedMethods =
        new(["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ForbiddenHeaders =
        new([
            "Host", "Cookie", "Cookie2", "Proxy-Authorization", "Proxy-Authenticate",
            "Connection", "Keep-Alive", "Proxy-Connection", "Content-Length",
            "Transfer-Encoding", "Upgrade", "TE", "Trailer", "Expect"
        ], StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ScriptHttpRequest CreateRequest(
        ScriptPermissionGrant? grant,
        string url,
        string? method,
        IReadOnlyDictionary<string, string>? headers,
        string? body,
        int timeoutMilliseconds,
        bool followRedirects)
    {
        var request = new ScriptHttpRequest
        {
            Url = url,
            Method = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant(),
            Headers = headers is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : headers.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Body = body,
            TimeoutMilliseconds = timeoutMilliseconds,
            FollowRedirects = followRedirects
        };
        ValidateRequest(grant ?? ScriptPermissionGrant.Empty, request);
        return request;
    }

    public static ScriptHttpRequest ParseRequest(ScriptPermissionGrant? grant, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("HTTP 请求 IPC 缺少数据。");

        ScriptHttpRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ScriptHttpRequest>(payload, JsonOptions)
                ?? throw new InvalidDataException("HTTP 请求为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("HTTP 请求不是有效 JSON。", ex);
        }

        request = new ScriptHttpRequest
        {
            Url = request.Url,
            Method = request.Method,
            Headers = request.Headers is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : request.Headers.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Body = request.Body,
            TimeoutMilliseconds = request.TimeoutMilliseconds,
            FollowRedirects = request.FollowRedirects
        };
        ValidateRequest(grant ?? ScriptPermissionGrant.Empty, request);
        return request;
    }

    public static Uri ValidateTarget(ScriptPermissionGrant? grant, string url, string method)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaximumUrlLength)
            throw new InvalidDataException($"HTTP URL 不能为空且不能超过 {MaximumUrlLength} 个字符。");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidDataException("HTTP URL 必须是有效的绝对 URL。");

        ValidateTarget(grant ?? ScriptPermissionGrant.Empty, uri, method);
        return uri;
    }

    public static void ValidateTarget(ScriptPermissionGrant grant, Uri uri, string method)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.AbsoluteUri.Length > MaximumUrlLength ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("HTTP URL 只允许绝对 HTTPS 或 HTTP 地址。");

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("HTTP URL 不允许 userinfo 或 fragment。");

        if (!uri.IsDefaultPort)
            throw new InvalidDataException("HTTP 权限不包含端口范围；本批只允许 HTTPS 443 和 HTTP 80 默认端口。");

        if (Uri.CheckHostName(uri.IdnHost) != UriHostNameType.Dns)
            throw new InvalidDataException("HTTP 权限只允许精确 DNS 主机名，不允许 IP 字面量。");

        if (!AllowedMethods.Contains(method))
            throw new InvalidDataException($"HTTP 方法不支持：{method}");

        if (!grant.AllowsHttp(uri, method))
            throw new UnauthorizedAccessException($"本次运行未批准 {method} 访问主机 {uri.IdnHost}。");
    }

    public static void ValidateHeaders(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (headers.Count > MaximumHeaderCount)
            throw new InvalidDataException($"请求头最多允许 {MaximumHeaderCount} 项。");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0;
        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumHeaderNameLength ||
                !IsHeaderToken(name) || !seen.Add(name))
                throw new InvalidDataException("HTTP 请求头名称无效或重复。");
            if (ForbiddenHeaders.Contains(name))
                throw new InvalidDataException($"HTTP 请求头 {name} 由传输层管理或禁止脚本设置。");
            if (value is null || value.Length > MaximumHeaderValueLength ||
                value.Any(ch => ch is '\r' or '\n' or '\0' || (char.IsControl(ch) && ch != '\t')))
                throw new InvalidDataException($"HTTP 请求头 {name} 的值无效或超过长度上限。");

            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(name) +
                Encoding.UTF8.GetByteCount(value));
            if (totalBytes > MaximumHeaderBytes)
                throw new InvalidDataException($"HTTP 请求头总长度不能超过 {MaximumHeaderBytes} 字节。");
        }
    }

    public static void ValidateRequest(ScriptPermissionGrant grant, ScriptHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = ValidateTarget(grant, request.Url, request.Method);
        if (request.TimeoutMilliseconds is < MinimumTimeoutMilliseconds or > MaximumTimeoutMilliseconds)
            throw new InvalidDataException(
                $"HTTP timeoutMs 必须在 {MinimumTimeoutMilliseconds}–{MaximumTimeoutMilliseconds}ms 范围内。");
        ValidateHeaders(request.Headers ?? new Dictionary<string, string>());
        if (request.Body is not null &&
            Encoding.UTF8.GetByteCount(request.Body) > MaximumRequestBodyBytes)
            throw new InvalidDataException($"HTTP 请求正文不能超过 {MaximumRequestBodyBytes} 字节。");
    }

    public static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
            HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    public static string RedirectMethod(HttpStatusCode statusCode, string currentMethod)
    {
        if ((statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect) &&
            currentMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return "GET";
        if (statusCode == HttpStatusCode.SeeOther &&
            !currentMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            return "GET";
        return currentMethod;
    }

    private static bool IsHeaderToken(string value)
    {
        foreach (var ch in value)
        {
            if (ch > 0x7f || !(char.IsAsciiLetterOrDigit(ch) ||
                "!#$%&'*+-.^_`|~".IndexOf(ch) >= 0))
                return false;
        }
        return true;
    }
}
