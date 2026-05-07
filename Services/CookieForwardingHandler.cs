namespace GenealogyApp.Services;

/// <summary>
/// Blazor Server 在**服务端**用 <see cref="System.Net.Http.HttpClient"/> 转发请求时，出站消息默认**不包含**浏览器发来的 Cookie。
/// 本处理器把当前 <see cref="Microsoft.AspNetCore.Http.HttpContext"/> 上的 Cookie 头复制到出站请求，
/// 使 <c>/api/*</c> 控制器上的 <c>[Authorize]</c> 能识别同一用户会话。
/// </summary>
/// <remarks>仅用于同源 API；跨站请求请勿复用此模式。</remarks>
public sealed class CookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request.Headers.TryGetValue("Cookie", out var cookies) == true)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookies.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
