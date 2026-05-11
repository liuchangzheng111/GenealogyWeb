using System.Security.Claims;

namespace GenealogyWeb.Controllers;

/// <summary>
/// 控制器内复用的 Claims 解析扩展，避免各处重复 <c>FindFirst(ClaimTypes.NameIdentifier)</c>。
/// </summary>
internal static class ControllerAuthExtensions
{
    /// <summary>从 Cookie 认证主体解析用户 Id；缺失或非法格式时返回 null。</summary>
    internal static Guid? GetUserIdOrNull(this ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var id) ? id : null;
    }
}
