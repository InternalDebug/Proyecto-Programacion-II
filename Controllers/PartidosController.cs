using FairPlay.Models.Enums;
using FairPlay.Seguridad;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// Partidos de un torneo: verlos, designar árbitro, fijar la fecha y cambiar el estado.
/// Controlador "delgado": llama a los servicios y traduce el resultado a una respuesta HTTP.
///
/// Rutas (el torneo se identifica por su CÓDIGO, como en equipos y jugadores):
///   GET  /Torneos/{código}/Partidos                       -> Lista de partidos del torneo (pública)
///   GET  /Torneos/{código}/Partidos/{idPartido}           -> Detalle del partido (público)
///   POST /Torneos/{código}/Partidos/{idPartido}/Arbitro   -> Designar o quitar árbitro (solo el organizador)
///   POST /Torneos/{código}/Partidos/{idPartido}/Fecha     -> Fijar la fecha y hora (solo el organizador)
///   POST /Torneos/{código}/Partidos/{idPartido}/Estado    -> Cambiar el estado (organizador o árbitro del partido)
///   (Los eventos del partido —goles y tarjetas— se registran en EventosController, pero se muestran en el Details de aquí.)
///   GET  /Partidos/Arbitrados                             -> "Mis partidos" (los que arbitro)
///
/// A diferencia de EquiposController, aquí NO hay un [Route] a nivel de clase porque hay dos prefijos
/// distintos (Torneos/{código}/Partidos y Partidos/Arbitrados): cada acción trae su ruta completa.
/// Los nombres de los parámetros (codigoTorneo, idPartido) deben coincidir con las llaves {...} de las rutas.
///
/// [Authorize] a nivel de clase = todas las acciones exigen sesión, salvo las marcadas [AllowAnonymous].
/// </summary>
[Authorize]
public class PartidosController : ControladorBase
{
    private readonly IServicioPartidos _partidos;
    private readonly IServicioTorneos _torneos;
    private readonly IServicioEventos _eventos;
    private readonly IServicioEliminatoria _eliminatoria;

    public PartidosController(
        IServicioPartidos partidos, IServicioTorneos torneos, IServicioEventos eventos, IServicioEliminatoria eliminatoria)
    {
        _partidos = partidos;
        _torneos = torneos;
        _eventos = eventos;
        _eliminatoria = eliminatoria;
    }

    // ------------------------------------------------------------------
    // LISTA DE PARTIDOS DEL TORNEO
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Partidos
    // Pública: conocer el código del torneo es la "llave" para ver sus partidos.
    [AllowAnonymous]
    [HttpGet("Torneos/{codigoTorneo}/Partidos")]
    public async Task<IActionResult> Index(string codigoTorneo)
    {
        var torneo = await _torneos.ObtenerPorCodigoAsync(codigoTorneo);

        if (torneo is null)
        {
            return NotFound();
        }

        var idUsuario = User.ObtenerIdUsuario(); // null si es un visitante sin sesión

        var modelo = new PartidosTorneoViewModel
        {
            Torneo = torneo,
            Partidos = await _partidos.ListarDeTorneoAsync(torneo.IdTorneo),
            EsOrganizador = idUsuario == torneo.IdOrganizador
        };

        return View(modelo);
    }

    // ------------------------------------------------------------------
    // DETALLE DEL PARTIDO
    // ------------------------------------------------------------------

    // GET: /Torneos/ABCD2345/Partidos/7
    [AllowAnonymous]
    [HttpGet("Torneos/{codigoTorneo}/Partidos/{idPartido:int}")]
    public async Task<IActionResult> Details(string codigoTorneo, int idPartido)
    {
        var idUsuario = User.ObtenerIdUsuario();

        var resultado = await _partidos.ObtenerParaVerAsync(idUsuario, codigoTorneo, idPartido);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var partido = resultado.Valor!;
        var torneo = ReglasPartido.TorneoDe(partido);

        bool esOrganizador = ReglasPartido.EsOrganizador(partido, idUsuario);
        bool esArbitro = ReglasPartido.EsArbitro(partido, idUsuario);

        var modelo = new PartidoDetalleViewModel
        {
            Partido = partido,
            Torneo = torneo,
            EsOrganizador = esOrganizador,
            EsArbitro = esArbitro,
            Transiciones = ReglasPartido.TransicionesDisponibles(partido, torneo.Estado, esOrganizador, esArbitro),
            PuedeEditarFecha = esOrganizador && ReglasPartido.PuedeEditarFecha(partido, torneo.Estado),
            PuedeAsignarArbitro = esOrganizador && ReglasPartido.PuedeAsignarArbitro(partido, torneo.Estado),
            Eventos = partido.Eventos.ToList(),
            PuedeModificarEventos = ReglasEvento.PuedeModificar(partido, torneo.Estado, esOrganizador, esArbitro),
            // El servicio solo carga el historial para el organizador; para los demás la lista llega vacía.
            Historial = partido.CambiosEstado.ToList()
        };

        // Eliminatoria (Paso 6, entrega C): si el partido es de un cruce de copa, se agregan el global, el
        // ganador y el formulario de penales. Con el cruce ya decidido no se tocan los eventos: hay que reabrir
        // el partido primero (el servicio también lo comprueba).
        modelo.Eliminatoria = await _eliminatoria.ObtenerInfoAsync(idUsuario, partido);

        if (modelo.Eliminatoria?.CruceDecidido == true)
        {
            modelo.PuedeModificarEventos = false;
        }

        // Los jugadores solo hacen falta para el formulario de eventos: se consultan únicamente si este
        // usuario puede usarlo.
        if (modelo.PuedeModificarEventos)
        {
            var plantillas = await _eventos.ListarPlantillasAsync(partido.IdLocal, partido.IdVisitante);
            modelo.JugadoresLocal = plantillas.Local;
            modelo.JugadoresVisitante = plantillas.Visitante;
        }

        return View(modelo);
    }

    // ------------------------------------------------------------------
    // MIS PARTIDOS (los que arbitro)
    // ------------------------------------------------------------------

    // GET: /Partidos/Arbitrados
    [HttpGet("Partidos/Arbitrados")]
    public async Task<IActionResult> Arbitrados()
    {
        var partidos = await _partidos.ListarArbitradosAsync(IdUsuarioActual);
        return View(partidos);
    }

    // ------------------------------------------------------------------
    // DESIGNAR ÁRBITRO (solo el organizador)
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Arbitro
    // "nombreUsuario" vacío = quitar al árbitro actual.
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Arbitro")]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> AsignarArbitro(string codigoTorneo, int idPartido, string? nombreUsuario)
    {
        var resultado = await _partidos.AsignarArbitroAsync(IdUsuarioActual, codigoTorneo, idPartido, nombreUsuario);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: no existe el usuario, es responsable de un equipo del torneo, etc.
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            var arbitro = resultado.Valor!.Arbitro;

            TempData["Mensaje"] = arbitro is null
                ? "Se quitó el árbitro del partido."
                : $"{arbitro.NombreUsuario} fue designado árbitro del partido.";
        }

        // Es un formulario sin pantalla propia: siempre se vuelve al detalle del partido.
        return RedirectToAction(nameof(Details), new { codigoTorneo, idPartido });
    }

    // ------------------------------------------------------------------
    // FECHA Y HORA (solo el organizador)
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Fecha
    // "fechaHora" vacío = quitar la fecha. El input datetime-local envía "2026-11-25T15:30".
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Fecha")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProgramarFecha(string codigoTorneo, int idPartido, DateTime? fechaHora)
    {
        var resultado = await _partidos.ProgramarFechaAsync(IdUsuarioActual, codigoTorneo, idPartido, fechaHora);

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
            TempData["Mensaje"] = fechaHora is null
                ? "Se quitó la fecha del partido."
                : "La fecha y hora del partido se guardaron correctamente.";
        }

        return RedirectToAction(nameof(Details), new { codigoTorneo, idPartido });
    }

    // ------------------------------------------------------------------
    // CAMBIAR EL ESTADO (organizador o árbitro del partido)
    // ------------------------------------------------------------------

    // POST: /Torneos/ABCD2345/Partidos/7/Estado   (el formulario envía nuevoEstado = EnVivo, Finalizado...)
    [HttpPost("Torneos/{codigoTorneo}/Partidos/{idPartido:int}/Estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string codigoTorneo, int idPartido, EstadoPartido? nuevoEstado)
    {
        // Si el formulario no trae el estado destino, la petición está mal formada.
        if (nuevoEstado is null)
        {
            return BadRequest();
        }

        var resultado = await _partidos.CambiarEstadoAsync(IdUsuarioActual, codigoTorneo, idPartido, nuevoEstado.Value);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: transición no permitida, torneo finalizado, el estado ya cambió...
        {
            TempData["Error"] = resultado.Mensaje;
        }
        else
        {
            TempData["Mensaje"] = nuevoEstado.Value switch
            {
                EstadoPartido.EnVivo => "El partido está en vivo.",
                EstadoPartido.Finalizado => "El partido finalizó.",
                EstadoPartido.Suspendido => "El partido fue suspendido.",
                _ => "El partido volvió a estar programado."
            };
        }

        return RedirectToAction(nameof(Details), new { codigoTorneo, idPartido });
    }
}
