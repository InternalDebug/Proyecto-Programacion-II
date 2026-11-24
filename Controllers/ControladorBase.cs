using FairPlay.Seguridad;
using FairPlay.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Clase base de los controladores que gestionan datos de torneos, equipos y jugadores.
/// Reúne lo que antes se repetía en cada controlador (el id del usuario actual y la traducción
/// de los resultados del servicio a respuestas HTTP).
/// </summary>
public abstract class ControladorBase : Controller
{
    /// <summary>
    /// Id del usuario con sesión iniciada. Solo se usa en acciones con [Authorize], donde la cookie
    /// siempre trae el id (lo guarda CuentaController.IniciarSesionAsync).
    /// </summary>
    protected int IdUsuarioActual =>
        User.ObtenerIdUsuario() ?? throw new InvalidOperationException("La sesión no contiene el id del usuario.");

    /// <summary>
    /// Traduce NoEncontrado y SinPermiso a su respuesta HTTP. Devuelve null cuando el resultado es
    /// Correcto o Invalido: esos dos casos los resuelve cada acción a su manera.
    /// </summary>
    protected IActionResult? RespuestaDeError<T>(ResultadoServicio<T> resultado) where T : class
    {
        if (resultado.Estado == EstadoResultado.NoEncontrado)
        {
            return NotFound(); // 404: no existe (o no tienes derecho a saber que existe)
        }

        if (resultado.Estado == EstadoResultado.SinPermiso)
        {
            return Forbid(); // 403: existe, pero no tienes permiso (ASP.NET redirige a /Cuenta/AccesoDenegado)
        }

        return null;
    }

    /// <summary>
    /// Redirige a returnUrl si es una dirección de ESTE sitio; si no, usa la respuesta por defecto.
    /// Url.IsLocalUrl evita el "open redirect": un enlace manipulado con returnUrl=https://sitio-falso.com
    /// no puede llevar al usuario a otro sitio.
    /// </summary>
    protected IActionResult RedirigirA(string? returnUrl, IActionResult respuestaPorDefecto)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return respuestaPorDefecto;
    }
}
