using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenealogyWeb.Data;
using GenealogyWeb.Models;
using GenealogyWeb.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GenealogyWeb.Controllers
{
    /// <summary>
    /// 认证相关 API：注册、登录、登出、当前用户探测。
    /// 全程 <see cref="AllowAnonymousAttribute"/>：勿在本控制器上加 <c>[Authorize]</c>。
    /// </summary>
    /// <remarks>
    /// 登录成功写入 Cookie（方案名 <see cref="CookieAuthenticationDefaults.AuthenticationScheme"/>），
    /// Blazor 与后续 <c>/api/*</c> 受保护接口依赖同一 Cookie。
    /// </remarks>
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>邮箱注册：校验重复邮箱，密码仅存哈希。</summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and password required.");

            var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
            if (exists) return Conflict("Email already registered.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName ?? dto.Email,
                Email = dto.Email,
                PasswordHash = PasswordHelper.HashPassword(dto.Password),
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return Ok(new { message = "注册成功", user.Id, user.UserName, user.Email });
        }

        /// <summary>邮箱 + 密码登录，建立 Cookie 会话。</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return Unauthorized(new { message = "用户不存在" });

            if (!PasswordHelper.Verify(user.PasswordHash, dto.Password)) return Unauthorized(new { message = "密码错误" });

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Ok(new { message = "登录成功", user.Id, user.UserName, user.Email });
        }

        /// <summary>
        /// 服务端登出（清除服务端 Cookie）。浏览器端若需同步清除，应导航到 <c>GET /logout</c>。
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        /// <summary>供静态脚本探测登录态；未登录也返回 200 + JSON。</summary>
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Ok(new { isAuthenticated = false, message = "未登录" });
            }

            return Ok(new
            {
                isAuthenticated = true,
                userName = User.Identity?.Name,
                email = User.FindFirstValue(ClaimTypes.Email),
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
        }
    }

    /// <summary>注册请求体（JSON 属性名大小写不敏感）。</summary>
    public record RegisterDto(string? UserName, string Email, string Password);

    /// <summary>登录请求体。</summary>
    public record LoginDto(string Email, string Password);
}
