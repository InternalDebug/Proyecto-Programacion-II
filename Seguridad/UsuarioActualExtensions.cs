using System.Security.Claims;

namespace FairPlay.Seguridad;

/// <summary>
/// Atajos para saber QUIÉN es el usuario que hizo la petición.
///
/// Importante para el diseño de FairPlay: la cookie solo guarda la IDENTIDAD (id y nombre),
/// NO un rol. Los roles son contextuales y se calculan en cada caso comparando este id:
///   organizador  -> torneo.IdOrganizador   == idUsuario
///   responsable  -> equipo.IdResponsable   == idUsuario
///   árbitro      -> partido.IdArbitro      == idUsuario
///   espectador   -> cualquier otro usuario
/// Eso lo haremos en los pasos 2 al 4, usando este método.
/// </summary>
public static class UsuarioActualExtensions
{
    /// <summary>Id del usuario con sesión iniciada, o null si es un visitante anónimo.</summary>
    public static int? ObtenerIdUsuario(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(valor, out var id) ? id : (int?)null;
    }
}
