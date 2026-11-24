using FairPlay.Models.Enums;
using FairPlay.Seguridad;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Inscripción, edición y eliminación de equipos, y decisión del organizador (aprobar / rechazar).
///
/// Todas las rutas cuelgan del CÓDIGO del torneo (así un equipo solo se alcanza conociendo el torneo):
///   GET/POST /Torneos/{código}/Equipos/Create                   -> Inscribir un equipo (con su primer jugador)
///   GET      /Torneos/{código}/Equipos/{idEquipo}               -> Ver el equipo con sus jugadores
///   GET/POST /Torneos/{código}/Equipos/{idEquipo}/Edit          -> Editar (responsable u organizador)
///   GET/POST /Torneos/{código}/Equipos/{idEquipo}/Delete        -> Eliminar (responsable u organizador)
///   POST     /Torneos/{código}/Equipos/{idEquipo}/Aprobar       -> Aprobar (solo el organizador)
///   POST     /Torneos/{código}/Equipos/{idEquipo}/Rechazar      -> Rechazar (solo el organizador)
///
/// Los nombres de los parámetros (codigoTorneo, idEquipo) deben coincidir con las llaves {...} de las rutas.
/// </summary>
[Authorize]
[Route("Torneos/{codigoTorneo}/Equipos")]
public class EquiposController : ControladorBase
{
    private readonly IServicioEquipos _equipos;

    public EquiposController(IServicioEquipos equipos)
    {
        _equipos = equipos;
    }

    // ------------------------------------------------------------------
    // INSCRIBIR (según el modo de inscripción del torneo)
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/Create
    [HttpGet("Create")]
    public async Task<IActionResult> Create(string codigoTorneo)
    {
        // El servicio aplica las reglas: modo (Cerrada / ConAprobación / Automática), estado y cupo.
        var resultado = await _equipos.ObtenerTorneoParaInscribirAsync(IdUsuarioActual, codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: hoy no puede inscribir -> se le explica el motivo en el detalle del torneo
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction("Details", "Torneos", new { id = codigoTorneo });
        }

        return View(new InscribirEquipoViewModel { CodigoTorneo = codigoTorneo });
    }

    // POST: /Torneos/ABCD2345/Equipos/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> Create(string codigoTorneo, InscribirEquipoViewModel modelo)
    {
        // El torneo es el de la URL, pase lo que pase con el formulario.
        modelo.CodigoTorneo = codigoTorneo;

        // 1) Validaciones de los atributos (equipo y primer jugador: obligatorios, largos, fechas...).
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // 2) El servicio vuelve a aplicar las reglas de inscripción, asigna al responsable (el usuario actual),
        //    decide el estado (Aprobado o Pendiente) y guarda equipo + primer jugador juntos.
        var resultado = await _equipos.InscribirAsync(IdUsuarioActual, codigoTorneo, modelo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensaje ?? "No se pudo inscribir el equipo.");
            return View(modelo);
        }

        var equipo = resultado.Valor!;

        TempData["Mensaje"] = equipo.EstadoInscripcion == EstadoInscripcion.Pendiente
            ? "Solicitud enviada: el organizador debe aprobar la inscripción de tu equipo. Mientras tanto puedes seguir agregando jugadores."
            : "Equipo inscrito correctamente. Ahora puedes agregar más jugadores.";

        return RedirectToAction(nameof(Details), new { codigoTorneo, idEquipo = equipo.IdEquipo });
    }

    // ------------------------------------------------------------------
    // DETALLE
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5
    // Pública para los equipos Aprobados (quien conozca el código del torneo los ve).
    // Los Pendientes y Rechazados solo los ve el organizador o el responsable; para el resto, 404.
    [AllowAnonymous]
    [HttpGet("{idEquipo:int}")]
    public async Task<IActionResult> Details(string codigoTorneo, int idEquipo)
    {
        var idUsuario = User.ObtenerIdUsuario(); // null si es un visitante sin sesión

        var resultado = await _equipos.ObtenerParaVerAsync(idUsuario, codigoTorneo, idEquipo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var equipo = resultado.Valor!;

        var modelo = new EquipoDetalleViewModel
        {
            Equipo = equipo,
            EsOrganizador = idUsuario == equipo.Torneo!.IdOrganizador,
            EsResponsable = idUsuario == equipo.IdResponsable
        };

        return View(modelo);
    }

    // ------------------------------------------------------------------
    // EDITAR (responsable del equipo u organizador del torneo)
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5/Edit
    [HttpGet("{idEquipo:int}/Edit")]
    public async Task<IActionResult> Edit(string codigoTorneo, int idEquipo)
    {
        var resultado = await _equipos.ObtenerParaGestionAsync(IdUsuarioActual, codigoTorneo, idEquipo);

        // NoEncontrado -> 404 | SinPermiso -> 403 (ASP.NET redirige a /Cuenta/AccesoDenegado)
        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var modelo = EquipoFormViewModel.DesdeEquipo(resultado.Valor!);
        modelo.CodigoTorneo = codigoTorneo;

        return View(modelo);
    }

    // POST: /Torneos/ABCD2345/Equipos/5/Edit
    [HttpPost("{idEquipo:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string codigoTorneo, int idEquipo, EquipoFormViewModel modelo)
    {
        // El torneo y el equipo a modificar son los de la URL, pase lo que pase con el formulario.
        modelo.CodigoTorneo = codigoTorneo;
        modelo.IdEquipo = idEquipo;

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // El servicio vuelve a comprobar el permiso (alguien podría enviar este POST a mano).
        var resultado = await _equipos.EditarAsync(IdUsuarioActual, codigoTorneo, idEquipo, modelo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensaje ?? "No se pudieron guardar los cambios.");
            return View(modelo);
        }

        TempData["Mensaje"] = "Los cambios del equipo se guardaron correctamente.";
        return RedirectToAction(nameof(Details), new { codigoTorneo, idEquipo });
    }

    // ------------------------------------------------------------------
    // ELIMINAR (responsable del equipo u organizador del torneo)
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5/Delete?returnUrl=/Solicitudes -> pantalla de confirmación
    [HttpGet("{idEquipo:int}/Delete")]
    public async Task<IActionResult> Delete(string codigoTorneo, int idEquipo, string? returnUrl)
    {
        var resultado = await _equipos.ObtenerParaGestionAsync(IdUsuarioActual, codigoTorneo, idEquipo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        // Para que, tras eliminar, se pueda volver a la pantalla de donde se vino (por ejemplo "Solicitudes").
        ViewData["ReturnUrl"] = returnUrl;

        return View(resultado.Valor);
    }

    // POST: /Torneos/ABCD2345/Equipos/5/Delete
    // [ActionName("Delete")]: se llama DeleteConfirmed en C# (no puede haber dos métodos Delete con la
    // misma firma) pero responde a la misma URL. Es el patrón de CineMVC.
    [HttpPost("{idEquipo:int}/Delete"), ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string codigoTorneo, int idEquipo, string? returnUrl)
    {
        var resultado = await _equipos.EliminarAsync(IdUsuarioActual, codigoTorneo, idEquipo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: el torneo ya empezó, o la BD no lo permitió
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirigirA(returnUrl, RedirectToAction(nameof(Details), new { codigoTorneo, idEquipo }));
        }

        TempData["Mensaje"] = $"El equipo \"{resultado.Valor!.Nombre}\" se eliminó correctamente.";

        // El equipo ya no existe: volvemos a donde se vino, o al detalle del torneo.
        return RedirigirA(returnUrl, RedirectToAction("Details", "Torneos", new { id = codigoTorneo }));
    }

    // ------------------------------------------------------------------
    // APROBAR / RECHAZAR (solo el organizador; los botones están en "Solicitudes de inscripción")
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Equipos/5/Aprobar
    [HttpPost("{idEquipo:int}/Aprobar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Aprobar(string codigoTorneo, int idEquipo, string? returnUrl) =>
        DecidirAsync(codigoTorneo, idEquipo, EstadoInscripcion.Aprobado, returnUrl);

    // POST: /Torneos/ABCD2345/Equipos/5/Rechazar
    [HttpPost("{idEquipo:int}/Rechazar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Rechazar(string codigoTorneo, int idEquipo, string? returnUrl) =>
        DecidirAsync(codigoTorneo, idEquipo, EstadoInscripcion.Rechazado, returnUrl);

    // ------------------------------------------------------------------
    // Método auxiliar privado
    // ------------------------------------------------------------------

    /// <summary>Lógica común de Aprobar y Rechazar (solo cambia la decisión).</summary>
    private async Task<IActionResult> DecidirAsync(string codigoTorneo, int idEquipo, EstadoInscripcion decision, string? returnUrl)
    {
        var resultado = await _equipos.DecidirInscripcionAsync(IdUsuarioActual, codigoTorneo, idEquipo, decision);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: ya estaba en ese estado, sin jugadores, cupo lleno, torneo en juego...
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            TempData["Mensaje"] = decision == EstadoInscripcion.Aprobado
                ? $"El equipo \"{resultado.Valor!.Nombre}\" fue aprobado."
                : $"El equipo \"{resultado.Valor!.Nombre}\" fue rechazado.";
        }

        // Normalmente se vuelve a "Solicitudes" (returnUrl); si no, al detalle del equipo.
        return RedirigirA(returnUrl, RedirectToAction(nameof(Details), new { codigoTorneo, idEquipo }));
    }
}
