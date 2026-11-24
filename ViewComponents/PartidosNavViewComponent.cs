using FairPlay.Seguridad;
using FairPlay.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.ViewComponents;

/// <summary>
/// Pieza reutilizable de la barra de navegación: el enlace "Mis partidos" con globos que indican
/// cuántos de los partidos que arbitras se están jugando ahora o están por jugarse.
///
/// Solo aparece si el usuario es árbitro designado en al menos un partido; en cualquier otro caso
/// no dibuja nada (así los demás usuarios no ven un enlace vacío).
///
/// Se usa en Views/Shared/_Layout.cshtml con:  @await Component.InvokeAsync("PartidosNav")
/// Es el mismo patrón de SolicitudesNavViewComponent.
/// </summary>
public class PartidosNavViewComponent : ViewComponent
{
    private readonly IServicioPartidos _partidos;

    public PartidosNavViewComponent(IServicioPartidos partidos)
    {
        _partidos = partidos;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var idUsuario = UserClaimsPrincipal.ObtenerIdUsuario();

        // Visitante sin sesión: no se dibuja nada.
        if (idUsuario is null)
        {
            return Content(string.Empty);
        }

        var resumen = await _partidos.ObtenerResumenArbitrajeAsync(idUsuario.Value);

        if (!resumen.MostrarEnMenu)
        {
            return Content(string.Empty);
        }

        return View(resumen); // Views/Shared/Components/PartidosNav/Default.cshtml
    }
}
