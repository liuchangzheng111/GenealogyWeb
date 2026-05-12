// =============================================================================
// GenealogyWeb 应用入口（.NET 8 最小宿主模型）
// -----------------------------------------------------------------------------
// 职责概览：
//   1) 注册 Blazor Server、MVC API、EF Core（MySQL）、Cookie 认证与授权策略；
//   2) 配置「GenealogyApi」HttpClient：转发浏览器 Cookie，供 Blazor 服务端调用同源 API；
//   3) 启动时执行数据库迁移与种子数据；
//   4) 映射 /logout（浏览器请求，用于真正清除认证 Cookie）、控制器、SignalR Hub、Blazor 回退页。
// 团队协作：修改管道顺序或新增认证方案前，请先阅读 docs/开发协作说明.md。
// =============================================================================

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Data;
using GenealogyWeb.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- UI 与 API ---
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

// HttpContext 在 Blazor 与 Cookie 转发中都需要（勿删）。
builder.Services.AddHttpContextAccessor();

// Blazor 的 AuthorizeView 等组件依赖 CascadingAuthenticationState + 下方注册的 AuthenticationStateProvider。
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookiePrincipalAuthenticationStateProvider>();
builder.Services.AddScoped<IGenealogyAccessService, GenealogyAccessService>();
builder.Services.AddScoped<ToastService>();

// --- 供 Blazor 组件注入的 HttpClient：同源 API 且携带当前请求的 Cookie ---
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddHttpClient("GenealogyApi")
    .ConfigureHttpClient((sp, client) =>
    {
        // 预渲染（ServerPrerendered）阶段 NavigationManager 尚未初始化，故用当前请求的 Scheme/Host 拼 BaseAddress。
        var request = sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request;
        if (request != null)
        {
            client.BaseAddress = new Uri($"{request.Scheme}://{request.Host.Value}{request.PathBase}/");
        }
    })
    .AddHttpMessageHandler<CookieForwardingHandler>();

builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("GenealogyApi");
});

// --- 数据库 ---
var mysqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(mysqlConnection))
{
    throw new InvalidOperationException("请在 appsettings.json 或用户机密中配置 ConnectionStrings:DefaultConnection（MySQL 连接字符串）。");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(mysqlConnection, new MySqlServerVersion(new Version(8, 0, 36))));

// --- 认证 / 授权：API 在未登录时返回 401 JSON，避免把 SPA 重定向到登录页的 HTML ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "GenealogyAuth";
        options.LoginPath = "/login";
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 迁移 + 种子：首次部署或 CI 需保证连接串可用。
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    SeedData.EnsureSeeded(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 必须在浏览器发起的 GET 中 SignOut，Set-Cookie 才会写回客户端，从而清除 GenealogyAuth。
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
