using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GenealogyApp.Services;

/// <summary>
/// 为 Blazor 提供与 MVC Cookie 认证一致的 <see cref="AuthenticationState"/>。
/// 从 <see cref="Microsoft.AspNetCore.Http.HttpContext.User"/> 读取（已由 UseAuthentication 填充 Claims）。
/// </summary>
public sealed class CookiePrincipalAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookiePrincipalAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User
            ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
