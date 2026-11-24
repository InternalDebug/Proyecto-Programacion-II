using FairPlay.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Ventana "Solicitudes de inscripción" de la barra de navegación.
///
/// Muestra, para el organizador, los equipos Pendientes (esperando decisión) y Rechazados de TODOS
/// sus torneos. Desde aquí puede ver cada equipo con sus jugadores, y aprobarlo, rechazarlo o
/// eliminarlo (eliminar un equipo rechazado libera su nombre para que pueda volver a usarse).
///
/// Esta pantalla solo LISTA: las acciones de aprobar, rechazar y eliminar viven en EquiposController
/// y reciben returnUrl=/Solicitudes para volver aquí al terminar.
///
///   GET /Solicitudes
/// </summary>
[Authorize]
[Route("Solicitudes")]
public class SolicitudesController : ControladorBase
{
    private readonly IServicioEquipos _equipos;

    public SolicitudesController(IServicioEquipos equipos)
    {
        _equipos = equipos;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Solo trae equipos de torneos que organiza el usuario actual: nadie ve solicitudes ajenas.
        var solicitudes = await _equipos.ListarSolicitudesAsync(IdUsuarioActual);
        return View(solicitudes);
    }
}
