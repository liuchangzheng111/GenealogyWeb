using Microsoft.AspNetCore.Mvc;
using GenealogyApp.Data;
using GenealogyApp.Models;
using GenealogyApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GenealogyApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

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

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

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

    public record RegisterDto(string? UserName, string Email, string Password);
    public record LoginDto(string Email, string Password);
}
