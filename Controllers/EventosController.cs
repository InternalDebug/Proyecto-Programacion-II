using FairPlay.Models.Enums;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Eventos de un partido (goles y tarjetas): registrarlos y eliminarlos.
/// Controlador "delgado": llama al servicio y traduce el resultado a una respuesta HTTP.
/// No tiene pantallas propias: el formulario y la lista de eventos viven en el detalle del partido
/// (PartidosController.Details), y cada acción termina volviendo a esa pantalla.
///
/// Rutas (el torneo se identifica por su CÓDIGO, como en el resto del proyecto):
///   POST /Torneos/{código}/Partidos/{idPartido}/Eventos                        -> Registrar un evento
///   POST /Torneos/{código}/Partidos/{idPartido}/Eventos/{idEvento}/Eliminar    -> Eliminar un evento
///
/// Pueden usarlas el organizador y el árbitro del partido (el servicio revisa quién, cuándo y qué).
/// </summary>
[Authorize]
public class EventosController : ControladorBase
{
    private readonly IServicioEventos _eventos;

    public EventosController(IServicioEventos eventos)
    {
        _eventos = eventos;
    }

    // ------------------------------------------------------------------
    // REGISTRAR
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Eventos
    // El formulario envía Tipo, IdJugador, IdEquipo y Minuto (ver EventoFormViewModel).
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Eventos")]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> Registrar(string codigoTorneo, int idPartido, EventoFormViewModel datos)
    {
        var resultado = await _eventos.RegistrarAsync(IdUsuarioActual, codigoTorneo, idPartido, datos);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: falta el jugador, está expulsado, el partido no está En vivo...
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            var r = resultado.Valor!;
            var evento = r.Evento;

            // Ejemplo: "Gol de Juan Pérez (min. 23). Marcador: 1 - 0."
            var texto = evento.Tipo switch
            {
                TipoEvento.Gol when evento.Jugador is null => "Gol registrado",
                TipoEvento.Gol => $"Gol de {evento.Jugador!.NombreCompleto}",
                TipoEvento.Autogol => $"Autogol de {evento.Jugador!.NombreCompleto}",
                TipoEvento.Amarilla => $"Tarjeta amarilla para {evento.Jugador!.NombreCompleto}",
                _ => $"Tarjeta roja para {evento.Jugador!.NombreCompleto}"
            };

            if (evento.Minuto.HasValue)
            {
                texto += $" (min. {evento.Minuto})";
            }

            texto += ".";

            if (r.RojaAutomatica)
            {
                texto += " Es su segunda amarilla: queda expulsado (roja automática).";
            }

            TempData["Mensaje"] = $"{texto} Marcador: {r.Marcador}.";
        }

        // Es un formulario sin pantalla propia: siempre se vuelve al detalle del partido.
        return RedirectToAction("Details", "Partidos", new { codigoTorneo, idPartido });
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Eventos/15/Eliminar
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Eventos/{idEvento:int}/Eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(string codigoTorneo, int idPartido, int idEvento)
    {
        var resultado = await _eventos.EliminarAsync(IdUsuarioActual, codigoTorneo, idPartido, idEvento);

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
            var r = resultado.Valor!;

            var texto = $"Se eliminó el evento: {ReglasEvento.NombreTipo(r.Evento.Tipo).ToLowerInvariant()}.";

            if (r.RojaAutomatica)
            {
                texto += " También se quitó la roja automática de la doble amarilla.";
            }

            TempData["Mensaje"] = $"{texto} Marcador: {r.Marcador}.";
        }

        return RedirectToAction("Details", "Partidos", new { codigoTorneo, idPartido });
    }
}
