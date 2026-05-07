using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped(sp => sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient());

var mysqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(mysqlConnection))
{
    throw new InvalidOperationException("请在 appsettings.json 或用户机密中配置 ConnectionStrings:DefaultConnection（MySQL 连接字符串）。");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(mysqlConnection, new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "GenealogyAuth";
        options.LoginPath = "/";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure database is created for prototype
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
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

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
