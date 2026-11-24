using FairPlay.Seguridad;
using FairPlay.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.ViewComponents;

/// <summary>
/// Pieza reutilizable de la barra de navegación: el enlace "Solicitudes de inscripción" con un globo
/// que indica cuántas solicitudes esperan decisión.
///
/// Solo aparece si el usuario organiza algún torneo abierto a inscripciones con aprobación, o si
/// tiene solicitudes pendientes. En cualquier otro caso no dibuja nada.
///
/// Se usa en Views/Shared/_Layout.cshtml con:  @await Component.InvokeAsync("SolicitudesNav")
/// (un "ViewComponent" es como una mini-vista con su propia lógica; el layout no tiene controlador).
/// </summary>
public class SolicitudesNavViewComponent : ViewComponent
{
    private readonly IServicioEquipos _equipos;

    public SolicitudesNavViewComponent(IServicioEquipos equipos)
    {
        _equipos = equipos;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var idUsuario = UserClaimsPrincipal.ObtenerIdUsuario();

        // Visitante sin sesión: no se dibuja nada.
        if (idUsuario is null)
        {
            return Content(string.Empty);
        }

        var resumen = await _equipos.ObtenerResumenSolicitudesAsync(idUsuario.Value);

        if (!resumen.MostrarEnMenu)
        {
            return Content(string.Empty);
        }

        return View(resumen); // Views/Shared/Components/SolicitudesNav/Default.cshtml
    }
}
