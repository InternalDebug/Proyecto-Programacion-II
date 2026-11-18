using System.Security.Claims;
using FairPlay.Models;
using FairPlay.Seguridad;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Registro, inicio y cierre de sesión. Es un controlador "delgado": valida lo básico del
/// formulario y delega el trabajo real en IServicioCuentas.
/// Rutas: /Cuenta/Registro, /Cuenta/Login, /Cuenta/Logout, /Cuenta/Perfil
/// </summary>
public class CuentaController : Controller
{
    private readonly IServicioCuentas _cuentas;

    public CuentaController(IServicioCuentas cuentas)
    {
        _cuentas = cuentas;
    }

    // ------------------------------------------------------------------
    // REGISTRO
    // ------------------------------------------------------------------

    // GET: /Cuenta/Registro  -> muestra el formulario vacío
    [HttpGet]
    public IActionResult Registro()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegistroViewModel());
    }

    // POST: /Cuenta/Registro  -> procesa el formulario
    [HttpPost]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> Registro(RegistroViewModel modelo)
    {
        // 1) Validaciones de los atributos del ViewModel (largo, formato, contraseñas iguales...)
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // 2) Regla de negocio: el servicio revisa duplicados, genera el hash y guarda.
        var resultado = await _cuentas.RegistrarAsync(modelo.NombreUsuario, modelo.Email, modelo.Password);

        if (!resultado.Exito)
        {
            // El error se asocia al campo correspondiente (o al formulario completo si Campo es vacío).
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.MensajeError ?? "No se pudo completar el registro.");
            return View(modelo);
        }

        // 3) Registro correcto: iniciamos sesión de inmediato.
        await IniciarSesionAsync(resultado.Usuario!, recordarme: false);

        TempData["Mensaje"] = $"¡Bienvenido a FairPlay, {resultado.Usuario!.NombreUsuario}!";
        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------
    // INICIO DE SESIÓN
    // ------------------------------------------------------------------

    // GET: /Cuenta/Login?returnUrl=/Torneos
    // ASP.NET nos manda aquí (con returnUrl) cuando se visita una página con [Authorize] sin sesión.
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Cuenta/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario = await _cuentas.ValidarCredencialesAsync(modelo.UsuarioOCorreo, modelo.Password);

        if (usuario is null)
        {
            // Mensaje único y genérico: no revelamos si falló el usuario o la contraseña.
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(modelo);
        }

        await IniciarSesionAsync(usuario, modelo.Recordarme);

        // Solo volvemos a direcciones de NUESTRO sitio. Si no, un enlace malicioso del tipo
        // /Cuenta/Login?returnUrl=https://sitio-falso.com nos llevaría a otro sitio (open redirect).
        if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl))
        {
            return LocalRedirect(modelo.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------
    // CIERRE DE SESIÓN
    // ------------------------------------------------------------------

    // Es POST (no GET) para que nadie pueda cerrarte la sesión con solo un enlace o una imagen.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------
    // PERFIL (página protegida de prueba)
    // ------------------------------------------------------------------

    // GET: /Cuenta/Perfil  -> solo con sesión iniciada. Sirve para probar [Authorize] y returnUrl.
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        // El id sale de la cookie (claim); los demás datos se leen de la base de datos.
        var idUsuario = User.ObtenerIdUsuario();
        var usuario = idUsuario is null ? null : await _cuentas.ObtenerPorIdAsync(idUsuario.Value);

        if (usuario is null)
        {
            // La cookie existe pero el usuario ya no está en la BD: cerramos esa sesión inválida.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        return View(usuario);
    }

    // GET: /Cuenta/AccesoDenegado -> ASP.NET redirige aquí si hay sesión pero faltan permisos.
    // Lo usaremos cuando apliquemos los permisos por rol (organizador, árbitro...).
    [HttpGet]
    public IActionResult AccesoDenegado()
    {
        return View();
    }

    // ------------------------------------------------------------------
    // Método auxiliar privado
    // ------------------------------------------------------------------

    /// <summary>
    /// Crea la cookie de sesión. Solo guardamos la identidad (id y nombre); NO guardamos roles
    /// porque en FairPlay los roles dependen del torneo, equipo o partido (son contextuales).
    /// </summary>
    private async Task IniciarSesionAsync(Usuario usuario, bool recordarme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario)
        };

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identidad);

        var propiedades = new AuthenticationProperties
        {
            // IsPersistent = true -> la cookie sobrevive aunque cierres el navegador.
            IsPersistent = recordarme,
            ExpiresUtc = recordarme ? DateTimeOffset.UtcNow.AddDays(14) : null
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, propiedades);
    }
}
