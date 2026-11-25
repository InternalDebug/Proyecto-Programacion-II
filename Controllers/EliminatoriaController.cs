using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Eliminatoria de un torneo (Paso 6, entrega C: Copa): ver la llave y registrar los penales.
/// Controlador "delgado": llama al servicio y traduce el resultado a una respuesta HTTP.
/// El avance de los ganadores NO se pide desde aquí: ocurre solo cuando se finaliza el partido que cierra
/// un cruce (ver ServicioPartidos.CambiarEstadoAsync y ServicioEliminatoria).
///
/// Rutas (el torneo se identifica por su CÓDIGO):
///   GET  /Torneos/{código}/Llave                           -> La llave (cuadro) del torneo. PÚBLICA.
///   POST /Torneos/{código}/Partidos/{idPartido}/Penales    -> Registrar o quitar los penales (organizador o árbitro del partido)
///
/// Como en PartidosController, cada acción trae su ruta completa y los nombres de los parámetros
/// (codigoTorneo, idPartido) deben coincidir con las llaves {...}.
///
/// [Authorize] a nivel de clase = todas las acciones exigen sesión, salvo las marcadas [AllowAnonymous].
/// </summary>
[Authorize]
public class EliminatoriaController : ControladorBase
{
    private readonly IServicioEliminatoria _eliminatoria;

    public EliminatoriaController(IServicioEliminatoria eliminatoria)
    {
        _eliminatoria = eliminatoria;
    }

    // ------------------------------------------------------------------
    // LLAVE
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Llave
    // Pública: conocer el código del torneo es la "llave" para verla (igual que los partidos).
    [AllowAnonymous]
    [HttpGet("Torneos/{codigoTorneo}/Llave")]
    public async Task<IActionResult> Llave(string codigoTorneo)
    {
        var resultado = await _eliminatoria.ObtenerLlaveAsync(codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var llave = resultado.Valor!;

        return View(new LlaveViewModel
        {
            Torneo = llave.Torneo,
            Rondas = llave.Rondas.ToList(),
            Campeon = llave.Campeon
        });
    }

    // ------------------------------------------------------------------
    // PENALES
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Penales
    // El formulario del detalle del partido envía PenalesLocal y PenalesVisitante (los dos vacíos = quitarlos).
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Penales")]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> RegistrarPenales(string codigoTorneo, int idPartido, PenalesFormViewModel datos)
    {
        var resultado = await _eliminatoria.RegistrarPenalesAsync(
            IdUsuarioActual, codigoTorneo, idPartido, datos.PenalesLocal, datos.PenalesVisitante);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: no hay empate, hay empate en penales, el partido no está En vivo...
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            var partido = resultado.Valor!;

            TempData["Mensaje"] = partido.PenalesLocal.HasValue
                ? $"Penales registrados: {partido.PenalesLocal} - {partido.PenalesVisitante}."
                : "Se quitaron los penales del partido.";
        }

        // Es un formulario sin pantalla propia: siempre se vuelve al detalle del partido.
        return RedirectToAction("Details", "Partidos", new { codigoTorneo, idPartido });
    }
}
