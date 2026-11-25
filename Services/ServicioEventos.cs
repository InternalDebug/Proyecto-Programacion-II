using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioEventos : IServicioEventos
{
    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioEventos> _logger;
    private readonly IServicioEliminatoria _eliminatoria;

    // ASP.NET Core entrega los tres objetos solos (inyección de dependencias).
    // _eliminatoria (Paso 6, entrega C): protege la llave de una copa cuando se corrige un partido ya decidido.
    public ServicioEventos(FairPlayContext contexto, ILogger<ServicioEventos> logger, IServicioEliminatoria eliminatoria)
    {
        _contexto = contexto;
        _logger = logger;
        _eliminatoria = eliminatoria;
    }

    // ------------------------------------------------------------------
    // REGISTRAR UN EVENTO
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<ResultadoEvento>> RegistrarAsync(
        int idUsuario, string? codigoTorneo, int idPartido, EventoFormViewModel datos)
    {
        // Sin AsNoTracking: EF debe seguir el partido para guardar el marcador junto con el evento.
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<ResultadoEvento>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);
        bool esOrganizador = torneo.IdOrganizador == idUsuario;
        bool esArbitro = partido.IdArbitro == idUsuario;

        // 1) QUIÉN: solo el organizador o el árbitro de ESTE partido (el resto recibe 403).
        if (!esOrganizador && !esArbitro)
        {
            return ResultadoServicio<ResultadoEvento>.SinPermiso();
        }

        // 2) CUÁNDO: el árbitro solo con el partido EnVivo; el organizador también con el partido Finalizado.
        var bloqueo = ReglasEvento.MotivoBloqueo(partido, torneo.Estado, esOrganizador);
        if (bloqueo is not null)
        {
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, bloqueo);
        }

        // 2b) ELIMINATORIA (Paso 6, entrega C): si el ganador de este cruce ya avanzó, un gol podría cambiar
        //     quién avanza. Hay que reabrir el partido primero (eso retira el avance si todavía es posible).
        var bloqueoCruce = await _eliminatoria.MotivoBloqueoEventosAsync(partido);
        if (bloqueoCruce is not null)
        {
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, bloqueoCruce);
        }

        // 3) DATOS DEL FORMULARIO
        if (datos.Tipo is null || !Enum.IsDefined(datos.Tipo.Value))
        {
            return ResultadoServicio<ResultadoEvento>.Invalido("tipo", "Elige el tipo de evento.");
        }

        var tipo = datos.Tipo.Value;

        if (datos.Minuto is < 0 or > 130)
        {
            return ResultadoServicio<ResultadoEvento>.Invalido("minuto", "El minuto debe estar entre 0 y 130 (o déjalo vacío).");
        }

        // 4) JUGADOR Y EQUIPO. El equipo del evento es SIEMPRE el del jugador; solo un Gol sin jugador
        //    usa el equipo indicado en el formulario.
        Jugador? jugador = null;
        int idEquipo;

        if (datos.IdJugador.HasValue)
        {
            // Se carga con tracking a propósito: así EF ya conoce al jugador al guardar el evento.
            jugador = await _contexto.Jugadores.FirstOrDefaultAsync(j => j.IdJugador == datos.IdJugador.Value);

            // El jugador debe pertenecer a uno de los dos equipos del partido (el trigger de la BD
            // también lo exige, pero así el usuario recibe un mensaje claro y no un error de la BD).
            if (jugador is null || (jugador.IdEquipo != partido.IdLocal && jugador.IdEquipo != partido.IdVisitante))
            {
                return ResultadoServicio<ResultadoEvento>.Invalido(
                    "idJugador", "El jugador elegido no pertenece a ninguno de los equipos de este partido.");
            }

            idEquipo = jugador.IdEquipo;
        }
        else
        {
            if (tipo != TipoEvento.Gol)
            {
                return ResultadoServicio<ResultadoEvento>.Invalido(
                    "idJugador", $"Elige al jugador: {ReglasEvento.NombreTipo(tipo).ToLowerInvariant()} necesita un jugador.");
            }

            if (!datos.IdEquipo.HasValue
                || (datos.IdEquipo.Value != partido.IdLocal && datos.IdEquipo.Value != partido.IdVisitante))
            {
                return ResultadoServicio<ResultadoEvento>.Invalido(
                    "idEquipo", "Elige el equipo al que se acredita el gol.");
            }

            idEquipo = datos.IdEquipo.Value;
        }

        // 5) DISCIPLINA (solo si hay jugador): tarjetas que ya tiene en ESTE partido.
        bool generaRojaAutomatica = false;

        if (jugador is not null)
        {
            var tarjetas = await _contexto.EventosPartido
                .Where(e => e.IdPartido == partido.IdPartido
                         && e.IdJugador == jugador.IdJugador
                         && (e.Tipo == TipoEvento.Amarilla || e.Tipo == TipoEvento.Roja))
                .ToListAsync();

            var roja = tarjetas.FirstOrDefault(e => e.Tipo == TipoEvento.Roja);
            int amarillas = tarjetas.Count(e => e.Tipo == TipoEvento.Amarilla);

            switch (tipo)
            {
                case TipoEvento.Roja when roja is not null:
                    return ResultadoServicio<ResultadoEvento>.Invalido(
                        "tipo", $"{jugador.NombreCompleto} ya tiene tarjeta roja en este partido.");

                case TipoEvento.Amarilla when roja is not null:
                    return ResultadoServicio<ResultadoEvento>.Invalido(
                        "tipo", $"{jugador.NombreCompleto} ya fue expulsado en este partido.");

                case TipoEvento.Amarilla when amarillas >= ReglasEvento.AmarillasParaExpulsion:
                    return ResultadoServicio<ResultadoEvento>.Invalido(
                        "tipo", $"{jugador.NombreCompleto} ya tiene dos amarillas en este partido.");

                case TipoEvento.Gol or TipoEvento.Autogol when ReglasEvento.EstaExpulsado(roja, datos.Minuto):
                    return ResultadoServicio<ResultadoEvento>.Invalido(
                        "idJugador", $"{jugador.NombreCompleto} ya fue expulsado y no puede participar en más jugadas.");
            }

            // La segunda amarilla (sin roja previa) expulsa al jugador: se agrega la roja sola.
            generaRojaAutomatica = tipo == TipoEvento.Amarilla
                && amarillas + 1 >= ReglasEvento.AmarillasParaExpulsion;
        }

        // 6) EFECTOS. El evento, la roja automática y el marcador se guardan JUNTOS en un solo SaveChanges
        //    (una transacción): o se guarda todo o no se guarda nada.
        var evento = new EventoPartido
        {
            IdPartido = partido.IdPartido,
            IdEquipo = idEquipo,
            IdJugador = jugador?.IdJugador,
            IdRegistradoPor = idUsuario,
            Tipo = tipo,
            Minuto = datos.Minuto
        };

        _contexto.EventosPartido.Add(evento);

        if (generaRojaAutomatica)
        {
            _contexto.EventosPartido.Add(new EventoPartido
            {
                IdPartido = partido.IdPartido,
                IdEquipo = idEquipo,
                IdJugador = jugador!.IdJugador,
                IdRegistradoPor = idUsuario,
                Tipo = TipoEvento.Roja,
                Minuto = datos.Minuto // la roja cae en el mismo minuto que la segunda amarilla
            });
        }

        var (golesLocal, golesVisitante) = ReglasEvento.AporteAlMarcador(tipo, idEquipo, partido.IdLocal);
        AplicarAlMarcador(partido, golesLocal, golesVisitante);

        // Eliminatoria: si el marcador del cruce dejó de estar empatado, los penales guardados ya no hacen falta.
        await _eliminatoria.LimpiarPenalesObsoletosAsync(partido);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo registrar el evento del partido {IdPartido}.", idPartido);
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, "No se pudo registrar el evento. Inténtalo de nuevo.");
        }

        evento.Jugador = jugador; // para que el controlador pueda nombrar al jugador en el mensaje

        return ResultadoServicio<ResultadoEvento>.Correcto(
            new ResultadoEvento(evento, generaRojaAutomatica, ReglasPartido.Marcador(partido)));
    }

    // ------------------------------------------------------------------
    // ELIMINAR UN EVENTO
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<ResultadoEvento>> EliminarAsync(
        int idUsuario, string? codigoTorneo, int idPartido, int idEvento)
    {
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<ResultadoEvento>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);
        bool esOrganizador = torneo.IdOrganizador == idUsuario;
        bool esArbitro = partido.IdArbitro == idUsuario;

        if (!esOrganizador && !esArbitro)
        {
            return ResultadoServicio<ResultadoEvento>.SinPermiso();
        }

        var bloqueo = ReglasEvento.MotivoBloqueo(partido, torneo.Estado, esOrganizador);
        if (bloqueo is not null)
        {
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, bloqueo);
        }

        // Eliminatoria (Paso 6, entrega C): con el cruce ya decidido hay que reabrir el partido antes de corregirlo.
        var bloqueoCruce = await _eliminatoria.MotivoBloqueoEventosAsync(partido);
        if (bloqueoCruce is not null)
        {
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, bloqueoCruce);
        }

        // El evento debe ser de ESTE partido: un id de otro partido se trata como "no existe".
        var evento = await _contexto.EventosPartido
            .Include(e => e.Jugador)
            .FirstOrDefaultAsync(e => e.IdEvento == idEvento && e.IdPartido == partido.IdPartido);

        if (evento is null)
        {
            return ResultadoServicio<ResultadoEvento>.NoEncontrado();
        }

        // DISCIPLINA al eliminar. Se mantiene esta regla: un jugador con 2 amarillas siempre tiene
        // una roja que viene de ellas.
        bool rojaAutomaticaEliminada = false;
        EventoPartido? rojaDeLaDoble = null;

        if (evento.IdJugador.HasValue && (evento.Tipo == TipoEvento.Amarilla || evento.Tipo == TipoEvento.Roja))
        {
            var tarjetas = await _contexto.EventosPartido
                .Where(e => e.IdPartido == partido.IdPartido
                         && e.IdJugador == evento.IdJugador
                         && (e.Tipo == TipoEvento.Amarilla || e.Tipo == TipoEvento.Roja))
                .ToListAsync();

            int amarillas = tarjetas.Count(e => e.Tipo == TipoEvento.Amarilla);
            var roja = tarjetas.FirstOrDefault(e => e.Tipo == TipoEvento.Roja);

            if (evento.Tipo == TipoEvento.Roja && amarillas >= ReglasEvento.AmarillasParaExpulsion)
            {
                // Quitar solo la roja dejaría dos amarillas sin expulsión.
                return ResultadoServicio<ResultadoEvento>.Invalido(
                    string.Empty,
                    "Esa roja viene de la doble amarilla. Elimina una de las dos amarillas y la roja se quitará sola.");
            }

            if (evento.Tipo == TipoEvento.Amarilla && amarillas >= ReglasEvento.AmarillasParaExpulsion && roja is not null)
            {
                rojaDeLaDoble = roja;
                rojaAutomaticaEliminada = true;
            }
        }

        // EFECTOS: se quita el evento (y la roja de la doble amarilla) y se resta su aporte al marcador.
        _contexto.EventosPartido.Remove(evento);

        if (rojaDeLaDoble is not null)
        {
            _contexto.EventosPartido.Remove(rojaDeLaDoble);
        }

        var (golesLocal, golesVisitante) = ReglasEvento.AporteAlMarcador(evento.Tipo, evento.IdEquipo, partido.IdLocal);
        AplicarAlMarcador(partido, -golesLocal, -golesVisitante);

        // Eliminatoria: si el marcador del cruce dejó de estar empatado, los penales guardados ya no hacen falta.
        await _eliminatoria.LimpiarPenalesObsoletosAsync(partido);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el evento {IdEvento} del partido {IdPartido}.", idEvento, idPartido);
            return ResultadoServicio<ResultadoEvento>.Invalido(string.Empty, "No se pudo eliminar el evento. Inténtalo de nuevo.");
        }

        return ResultadoServicio<ResultadoEvento>.Correcto(
            new ResultadoEvento(evento, rojaAutomaticaEliminada, ReglasPartido.Marcador(partido)));
    }

    // ------------------------------------------------------------------
    // CONSULTAS PARA LA PANTALLA
    // ------------------------------------------------------------------

    public async Task<PlantillasPartido> ListarPlantillasAsync(int idLocal, int? idVisitante)
    {
        var local = await _contexto.Jugadores
            .AsNoTracking()
            .Where(j => j.IdEquipo == idLocal)
            .OrderBy(j => j.Dorsal).ThenBy(j => j.Apellido).ThenBy(j => j.Nombre)
            .ToListAsync();

        var visitante = idVisitante.HasValue
            ? await _contexto.Jugadores
                .AsNoTracking()
                .Where(j => j.IdEquipo == idVisitante.Value)
                .OrderBy(j => j.Dorsal).ThenBy(j => j.Apellido).ThenBy(j => j.Nombre)
                .ToListAsync()
            : new List<Jugador>();

        return new PlantillasPartido(local, visitante);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Suma (o resta, si los valores son negativos) goles al marcador del partido.
    /// El marcador se modifica de forma INCREMENTAL y no se recalcula contando eventos: así un marcador
    /// que ya existía (por ejemplo, el de los datos de prueba) no se pierde. Nunca baja de 0.
    /// </summary>
    private static void AplicarAlMarcador(Partido partido, int golesLocal, int golesVisitante)
    {
        if (golesLocal == 0 && golesVisitante == 0)
        {
            return; // las tarjetas no tocan el marcador
        }

        partido.GolesLocal = Math.Max(0, (partido.GolesLocal ?? 0) + golesLocal);
        partido.GolesVisitante = Math.Max(0, (partido.GolesVisitante ?? 0) + golesVisitante);
    }
}
