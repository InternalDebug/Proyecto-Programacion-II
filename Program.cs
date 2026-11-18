using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// --- Base de datos (SQLite + EF Core) ---
// Ajusta "FairPlay" si tu cadena de conexión tiene otro nombre en appsettings.json
var cadenaConexion = builder.Configuration.GetConnectionString("FairPlay")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'FairPlay' en appsettings.json.");
builder.Services.AddDbContext<FairPlayContext>(o => o.UseSqlite(cadenaConexion));

// --- Servicios de la aplicación ---
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<IServicioCuentas, ServicioCuentas>();

// --- Autenticación por cookies ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Cuenta/Login";              // a dónde mandar a quien no inició sesión
        o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        o.Cookie.Name = "FairPlay.Auth";
        o.Cookie.HttpOnly = true;                   // JavaScript no puede leer la cookie
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;                 // se renueva mientras uses el sitio
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // NUEVO: debe ir ANTES de UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();