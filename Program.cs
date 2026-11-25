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

// --- Servicio de gestion de torneos ---
builder.Services.AddScoped<IServicioTorneos, ServicioTorneos>();

// --- Equipos, jugadores y escudos (Paso 3) ---
builder.Services.AddScoped<IServicioEquipos, ServicioEquipos>();
builder.Services.AddScoped<IServicioJugadores, ServicioJugadores>();
builder.Services.AddSingleton<IAlmacenEscudos, AlmacenEscudos>();

// --- Partidos, árbitro y cambios de estado (Paso 4) ---
builder.Services.AddScoped<IServicioPartidos, ServicioPartidos>();

// --- Eventos del partido: goles y tarjetas (Paso 5) ---
builder.Services.AddScoped<IServicioEventos, ServicioEventos>();

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

// NUEVO (Paso 3): sirve los archivos de wwwroot que se agregan MIENTRAS la app corre (los escudos subidos).
// MapStaticAssets (más abajo) solo conoce los archivos que existían al compilar, así que sin esta línea
// los escudos se guardarían bien pero el navegador no podría mostrarlos (error 404).
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();   // NUEVO: debe ir ANTES de UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();