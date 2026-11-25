using FairPlay.Models.Enums;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Calendario de un torneo (Paso 6, entregas A, B y C): iniciar el torneo generando la Liga o la llave de la
/// Copa, regenerarlos mientras no se haya jugado nada y editar el nombre y la fecha de las jornadas (rondas).
/// Controlador "delgado": llama al servicio y traduce el resultado a una respuesta HTTP.
///
/// Rutas (el torneo se identifica por su CÓDIGO). TODAS son solo para el organizador:
///   GET  /Torneos/{código}/Calendario                    -> Panel: jornadas editables y botón de regenerar
///   GET  /Torneos/{código}/Calendario/Preparar           -> Sorteo editable antes de iniciar (o regenerar)
///   POST /Torneos/{código}/Calendario/Sortear            -> Vuelve a sortear al azar (no guarda nada)
///   POST /Torneos/{código}/Calendario/Iniciar            -> Inicia el torneo / regenera el calendario
///   POST /Torneos/{código}/Calendario/Rondas/{idRonda}   -> Cambia el nombre y la fecha de una jornada
///
/// Como en PartidosController, cada acción trae su ruta completa y los nombres de los parámetros
/// (codigoTorneo, idRonda) deben coincidir con las llaves {...}.
/// </summary>
[Authorize]
public class CalendarioController : ControladorBase
{
    private readonly IServicioCalendario _calendario;

    public CalendarioController(IServicioCalendario calendario)
    {
        _calendario = calendario;
    }

    // ------------------------------------------------------------------
    // PANEL DE GESTIÓN
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Calendario
    [HttpGet("Torneos/{codigoTorneo}/Calendario")]
    public async Task<IActionResult> Index(string codigoTorneo)
    {
        var resultado = await _calendario.ObtenerGestionAsync(IdUsuarioActual, codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var gestion = resultado.Valor!;

        return View(new GestionCalendarioViewModel
        {
            Torneo = gestion.Torneo,
            Rondas = gestion.Rondas.ToList(),
            PuedeRegenerar = gestion.PuedeRegenerar,
            MotivoNoRegenerar = gestion.MotivoNoRegenerar
        });
    }

    // ------------------------------------------------------------------
    // PREPARAR (sorteo editable)
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Calendario/Preparar
    [HttpGet("Torneos/{codigoTorneo}/Calendario/Preparar")]
    public async Task<IActionResult> Preparar(string codigoTorneo)
    {
        var resultado = await _calendario.PrepararAsync(IdUsuarioActual, codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: estado o formato que no permiten generar el calendario
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction("Details", "Torneos", new { id = codigoTorneo });
        }

        return View(PreparacionCalendarioViewModel.Desde(resultado.Valor!));
    }

    // POST: /Torneos/ABCD2345/Calendario/Sortear
    // Devuelve la misma pantalla con las posiciones al azar. NO guarda nada: el organizador puede seguir
    // editando los números a mano antes de confirmar.
    [HttpPost("Torneos/{codigoTorneo}/Calendario/Sortear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sortear(string codigoTorneo, bool idaYVuelta)
    {
        var resultado = await _calendario.SortearAsync(IdUsuarioActual, codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction("Details", "Torneos", new { id = codigoTorneo });
        }

        // IMPORTANTE: se limpia el ModelState. Si no, los campos del formulario mostrarían los valores
        // que se acaban de ENVIAR (las posiciones viejas) en lugar de las nuevas del sorteo.
        ModelState.Clear();

        return View("Preparar", PreparacionCalendarioViewModel.Desde(resultado.Valor!, idaYVuelta));
    }

    // ------------------------------------------------------------------
    // INICIAR / REGENERAR
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Calendario/Iniciar
    [HttpPost("Torneos/{codigoTorneo}/Calendario/Iniciar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iniciar(string codigoTorneo, IniciarCalendarioFormViewModel formulario)
    {
        var posiciones = formulario.Posiciones
            .Select(p => new PosicionEquipo(p.IdEquipo, p.Posicion))
            .ToList();

        var resultado = await _calendario.IniciarAsync(IdUsuarioActual, codigoTorneo, formulario.IdaYVuelta, posiciones);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: equipos incompletos, sorteo incorrecto, estado no permitido...
        {
            return await VolverAPrepararAsync(codigoTorneo, formulario, resultado.Mensaje);
        }

        var resumen = resultado.Valor!;

        string mensaje;

        if (resumen.Torneo.Formato == FormatoTorneo.Copa)
        {
            // Copa: se habla de rondas y de equipos que pasan directo (bye), no de jornadas ni descansos.
            mensaje = $"Llave generada: {resumen.Jornadas} ronda(s) y {resumen.Partidos} partido(s) creados.";

            if (resumen.PasanDirecto > 0)
            {
                mensaje += $" {resumen.PasanDirecto} equipo(s) pasan directo a la siguiente ronda porque el número de equipos no es potencia de 2.";
            }

            mensaje += " Los partidos de las rondas siguientes se crean solos cuando se conocen sus equipos.";
        }
        else
        {
            mensaje = $"Calendario generado: {resumen.Jornadas} jornada(s) y {resumen.Partidos} partido(s).";

            if (resumen.Descansos > 0)
            {
                mensaje += $" Hay {resumen.Descansos} descanso(s) porque el número de equipos es impar.";
            }
        }

        if (resumen.EquiposRechazados > 0)
        {
            mensaje += $" Se rechazaron {resumen.EquiposRechazados} solicitud(es) de inscripción pendientes.";
        }

        TempData["Mensaje"] = mensaje;
        return RedirectToAction(nameof(Index), new { codigoTorneo = resumen.Torneo.Codigo });
    }

    // ------------------------------------------------------------------
    // EDITAR UNA JORNADA
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Calendario/Rondas/5
    [HttpPost("Torneos/{codigoTorneo}/Calendario/Rondas/{idRonda:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarRonda(string codigoTorneo, int idRonda, EditarRondaFormViewModel formulario)
    {
        var resultado = await _calendario.EditarRondaAsync(
            IdUsuarioActual, codigoTorneo, idRonda, formulario.Nombre, formulario.FechaInicio);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            TempData["Mensaje"] = "La jornada se actualizó correctamente.";
        }

        return RedirectToAction(nameof(Index), new { codigoTorneo });
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Vuelve a mostrar la pantalla de preparación con un mensaje de error, conservando lo que el
    /// organizador había escrito (posiciones y casilla de ida y vuelta).
    /// </summary>
    private async Task<IActionResult> VolverAPrepararAsync(
        string codigoTorneo, IniciarCalendarioFormViewModel formulario, string? mensaje)
    {
        var resultado = await _calendario.PrepararAsync(IdUsuarioActual, codigoTorneo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito)
        {
            // El torneo cambió de estado mientras tanto: ya no hay nada que preparar.
            TempData["Error"] = mensaje ?? resultado.Mensaje;
            return RedirectToAction("Details", "Torneos", new { id = codigoTorneo });
        }

        var modelo = PreparacionCalendarioViewModel.Desde(resultado.Valor!, formulario.IdaYVuelta);

        // Se conservan las posiciones enviadas (solo las de equipos que de verdad existen en el torneo).
        var enviadas = formulario.Posiciones
            .GroupBy(p => p.IdEquipo)
            .ToDictionary(g => g.Key, g => g.First().Posicion);

        foreach (var equipo in modelo.Equipos)
        {
            if (enviadas.TryGetValue(equipo.IdEquipo, out var posicion))
            {
                equipo.Posicion = posicion;
            }
        }

        modelo.Equipos = modelo.Equipos
            .OrderBy(e => e.Posicion)
            .ThenBy(e => e.Nombre)
            .ToList();

        ModelState.AddModelError(string.Empty, mensaje ?? "No se pudo generar el calendario.");

        return View("Preparar", modelo);
    }
}
