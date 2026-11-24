using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Agregar, editar y eliminar jugadores de un equipo. Solo el responsable del equipo o el organizador
/// del torneo pueden hacerlo (el servicio lo comprueba en cada operación).
///
/// Las rutas cuelgan del código del torneo y del equipo:
///   GET/POST /Torneos/{código}/Equipos/{idEquipo}/Jugadores/Create
///   GET/POST /Torneos/{código}/Equipos/{idEquipo}/Jugadores/{idJugador}/Edit
///   GET/POST /Torneos/{código}/Equipos/{idEquipo}/Jugadores/{idJugador}/Delete
///
/// Los jugadores se muestran en la página del equipo (EquiposController.Details), por eso aquí no hay Index.
/// </summary>
[Authorize]
[Route("Torneos/{codigoTorneo}/Equipos/{idEquipo:int}/Jugadores")]
public class JugadoresController : ControladorBase
{
    private readonly IServicioJugadores _jugadores;

    public JugadoresController(IServicioJugadores jugadores)
    {
        _jugadores = jugadores;
    }

    // ------------------------------------------------------------------
    // AGREGAR
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5/Jugadores/Create
    [HttpGet("Create")]
    public async Task<IActionResult> Create(string codigoTorneo, int idEquipo)
    {
        var resultado = await _jugadores.ObtenerEquipoParaAgregarAsync(IdUsuarioActual, codigoTorneo, idEquipo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: por ejemplo, el torneo ya finalizó
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction("Details", "Equipos", new { codigoTorneo, idEquipo });
        }

        return View(new JugadorFormViewModel
        {
            CodigoTorneo = codigoTorneo,
            IdEquipo = idEquipo,
            NombreEquipo = resultado.Valor!.Nombre
        });
    }

    // POST: /Torneos/ABCD2345/Equipos/5/Jugadores/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string codigoTorneo, int idEquipo, JugadorFormViewModel modelo)
    {
        // El torneo y el equipo son los de la URL, pase lo que pase con el formulario.
        modelo.CodigoTorneo = codigoTorneo;
        modelo.IdEquipo = idEquipo;

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // El servicio comprueba el permiso, el dorsal único y el capitán único.
        var resultado = await _jugadores.AgregarAsync(IdUsuarioActual, codigoTorneo, idEquipo, modelo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensaje ?? "No se pudo agregar el jugador.");
            return View(modelo);
        }

        TempData["Mensaje"] = $"Jugador {resultado.Valor!.NombreCompleto} agregado al equipo.";
        return RedirectToAction("Details", "Equipos", new { codigoTorneo, idEquipo });
    }

    // ------------------------------------------------------------------
    // EDITAR
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5/Jugadores/12/Edit
    [HttpGet("{idJugador:int}/Edit")]
    public async Task<IActionResult> Edit(string codigoTorneo, int idEquipo, int idJugador)
    {
        var resultado = await _jugadores.ObtenerParaGestionAsync(IdUsuarioActual, codigoTorneo, idEquipo, idJugador);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var modelo = JugadorFormViewModel.DesdeJugador(resultado.Valor!);
        modelo.CodigoTorneo = codigoTorneo;
        modelo.NombreEquipo = resultado.Valor!.Equipo?.Nombre;

        return View(modelo);
    }

    // POST: /Torneos/ABCD2345/Equipos/5/Jugadores/12/Edit
    [HttpPost("{idJugador:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string codigoTorneo, int idEquipo, int idJugador, JugadorFormViewModel modelo)
    {
        modelo.CodigoTorneo = codigoTorneo;
        modelo.IdEquipo = idEquipo;
        modelo.IdJugador = idJugador;

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await _jugadores.EditarAsync(IdUsuarioActual, codigoTorneo, idEquipo, idJugador, modelo);

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

        TempData["Mensaje"] = "Los datos del jugador se guardaron correctamente.";
        return RedirectToAction("Details", "Equipos", new { codigoTorneo, idEquipo });
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Equipos/5/Jugadores/12/Delete -> pantalla de confirmación
    [HttpGet("{idJugador:int}/Delete")]
    public async Task<IActionResult> Delete(string codigoTorneo, int idEquipo, int idJugador)
    {
        var resultado = await _jugadores.ObtenerParaGestionAsync(IdUsuarioActual, codigoTorneo, idEquipo, idJugador);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        return View(resultado.Valor);
    }

    // POST: /Torneos/ABCD2345/Equipos/5/Jugadores/12/Delete
    [HttpPost("{idJugador:int}/Delete"), ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string codigoTorneo, int idEquipo, int idJugador)
    {
        var resultado = await _jugadores.EliminarAsync(IdUsuarioActual, codigoTorneo, idEquipo, idJugador);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: era el último jugador, o tiene eventos de partido
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            TempData["Mensaje"] = $"Jugador {resultado.Valor!.NombreCompleto} eliminado del equipo.";
        }

        return RedirectToAction("Details", "Equipos", new { codigoTorneo, idEquipo });
    }
}
