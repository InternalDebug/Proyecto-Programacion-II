using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioPartidos : IServicioPartidos
{
    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioPartidos> _logger;

    // ASP.NET Core entrega los dos objetos solos (inyección de dependencias).
    public ServicioPartidos(FairPlayContext contexto, ILogger<ServicioPartidos> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // CONSULTAS
    // ------------------------------------------------------------------

    public async Task<List<Partido>> ListarDeTorneoAsync(int idTorneo)
    {
        // AsNoTracking: solo se lee, EF no necesita seguir los objetos para guardar cambios.
        // OJO: sin "tracking", cada partido trae su PROPIA copia de la ronda. Por eso las vistas
        // agrupan por IdRonda y no por el objeto Ronda (mismo detalle que en Solicitudes).
        return await _contexto.Partidos
            .AsNoTracking()
            .Include(p => p.Local)
            .Include(p => p.Visitante)
            .Include(p => p.Arbitro)
            .Include(p => p.Ronda).ThenInclude(r => r!.Fase)
            .Include(p => p.Ronda).ThenInclude(r => r!.Grupo)
            .Where(p => p.Ronda!.Fase!.IdTorneo == idTorneo)
            .OrderBy(p => p.Ronda!.Fase!.Orden)
            .ThenBy(p => p.Ronda!.Numero)
            .ThenBy(p => p.Ronda!.IdGrupo)
            .ThenBy(p => p.IdRonda)
            .ThenBy(p => p.IdPartido)
            .ToListAsync();
    }

    public async Task<ResultadoServicio<Partido>> ObtenerParaVerAsync(int? idUsuario, string? codigoTorneo, int idPartido)
    {
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido)
            .Include(p => p.Local)
            .Include(p => p.Visitante)
            .Include(p => p.Arbitro)
            .Include(p => p.Ronda).ThenInclude(r => r!.Grupo)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        // Eventos del partido (goles y tarjetas): son PÚBLICOS, como el marcador. Se cargan aparte para
        // controlar el orden: por minuto, y los que no tienen minuto al final (en el orden en que se registraron).
        // El orden se hace en memoria; OrderBy de LINQ es estable, así que el empate conserva el id.
        var eventos = await _contexto.EventosPartido
            .AsNoTracking()
            .Include(e => e.Jugador)
            .Where(e => e.IdPartido == partido.IdPartido)
            .OrderBy(e => e.IdEvento)
            .ToListAsync();

        partido.Eventos = eventos
            .OrderBy(e => e.Minuto.HasValue ? 0 : 1)
            .ThenBy(e => e.Minuto)
            .ToList();

        // REGLA DE VISIBILIDAD: el historial de cambios de estado es solo para el organizador.
        // Se carga aparte (y solo si hace falta) en vez de usar un Include, para que ningún otro
        // usuario pueda recibirlo ni por descuido de la vista.
        if (ReglasPartido.EsOrganizador(partido, idUsuario))
        {
            partido.CambiosEstado = await _contexto.CambiosEstadoPartido
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Where(c => c.IdPartido == partido.IdPartido)
                .OrderByDescending(c => c.IdCambio) // el id crece con el tiempo: el más reciente primero
                .ToListAsync();
        }

        return ResultadoServicio<Partido>.Correcto(partido);
    }

    public async Task<List<Partido>> ListarArbitradosAsync(int idUsuario)
    {
        var partidos = await _contexto.Partidos
            .AsNoTracking()
            .Include(p => p.Local)
            .Include(p => p.Visitante)
            .Include(p => p.Ronda).ThenInclude(r => r!.Fase).ThenInclude(f => f!.Torneo)
            .Where(p => p.IdArbitro == idUsuario)
            .ToListAsync();

        // El orden se hace en memoria porque depende de reglas de C# (ReglasPartido.OrdenEstado).
        return partidos
            .OrderBy(p => ReglasPartido.OrdenEstado(p.Estado))
            .ThenBy(p => p.FechaHora ?? DateTime.MaxValue) // los que no tienen fecha, al final de su grupo
            .ThenBy(p => p.IdPartido)
            .ToList();
    }

    public async Task<ResumenArbitraje> ObtenerResumenArbitrajeAsync(int idUsuario)
    {
        // Una sola consulta liviana: solo los estados de los partidos que arbitra este usuario.
        var estados = await _contexto.Partidos
            .Where(p => p.IdArbitro == idUsuario)
            .Select(p => p.Estado)
            .ToListAsync();

        int enVivo = estados.Count(e => e == EstadoPartido.EnVivo);
        int porJugar = estados.Count(e => e == EstadoPartido.Programado);

        return new ResumenArbitraje(estados.Count > 0, enVivo, porJugar);
    }

    // ------------------------------------------------------------------
    // ÁRBITRO
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Partido>> AsignarArbitroAsync(
        int idUsuario, string? codigoTorneo, int idPartido, string? nombreArbitro)
    {
        // Aquí SÍ queremos que EF siga el objeto (sin AsNoTracking) para que guarde los cambios.
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);

        // REGLA DE PERMISOS: solo el organizador designa árbitros. El partido es público, así que
        // quien no lo es recibe 403 (existe, pero no tiene permiso) y no 404.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Partido>.SinPermiso();
        }

        if (!ReglasPartido.PuedeAsignarArbitro(partido, torneo.Estado))
        {
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                partido.EsDescanso
                    ? "Un descanso no tiene árbitro."
                    : "El torneo ya finalizó; no se pueden cambiar los árbitros.");
        }

        // Texto vacío = "quitar al árbitro actual".
        var nombre = ReglasEquipo.LimpiarTexto(nombreArbitro);
        Usuario? arbitro = null;

        if (nombre is not null)
        {
            // La BD compara nombre_usuario con COLLATE NOCASE: "Pedro" y "pedro" son el mismo usuario.
            arbitro = await _contexto.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombre);

            if (arbitro is null)
            {
                return ResultadoServicio<Partido>.Invalido(
                    "nombreUsuario",
                    "No existe ningún usuario con ese nombre de usuario.");
            }

            // REGLA DE CONFLICTO DE INTERÉS: quien es responsable de un equipo de ESTE torneo no puede
            // arbitrar en él. En otros torneos no hay restricción. Los equipos Rechazados no cuentan
            // (ya no participan). El organizador queda exento: en un torneo de inscripción Cerrada él
            // inscribe todos los equipos y por eso figura como su "responsable".
            if (arbitro.IdUsuario != torneo.IdOrganizador
                && await _contexto.Equipos.AnyAsync(e =>
                    e.IdTorneo == torneo.IdTorneo
                    && e.IdResponsable == arbitro.IdUsuario
                    && e.EstadoInscripcion != EstadoInscripcion.Rechazado))
            {
                return ResultadoServicio<Partido>.Invalido(
                    "nombreUsuario",
                    $"{arbitro.NombreUsuario} es responsable de un equipo de este torneo y no puede ser árbitro en él.");
            }
        }

        if (partido.IdArbitro == arbitro?.IdUsuario)
        {
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                arbitro is null ? "El partido ya no tiene árbitro." : "Ese usuario ya es el árbitro de este partido.");
        }

        partido.IdArbitro = arbitro?.IdUsuario;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo designar al árbitro del partido {IdPartido}.", idPartido);
            return ResultadoServicio<Partido>.Invalido(string.Empty, "No se pudo guardar el árbitro. Inténtalo de nuevo.");
        }

        // Para que el controlador pueda mostrar el nombre del árbitro en el mensaje.
        partido.Arbitro = arbitro;

        return ResultadoServicio<Partido>.Correcto(partido);
    }

    // ------------------------------------------------------------------
    // FECHA Y HORA
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Partido>> ProgramarFechaAsync(
        int idUsuario, string? codigoTorneo, int idPartido, DateTime? fechaHora)
    {
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);

        // REGLA DE PERMISOS: solo el organizador programa los partidos.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Partido>.SinPermiso();
        }

        if (!ReglasPartido.PuedeEditarFecha(partido, torneo.Estado))
        {
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                "La fecha solo se puede cambiar en partidos Programados o Suspendidos de un torneo que no haya finalizado.");
        }

        // Evita fechas absurdas por un formulario manipulado (por ejemplo el año 0001).
        if (fechaHora.HasValue && (fechaHora.Value.Year < 2000 || fechaHora.Value.Year > 2100))
        {
            return ResultadoServicio<Partido>.Invalido("fechaHora", "La fecha del partido no es válida.");
        }

        partido.FechaHora = fechaHora;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar la fecha del partido {IdPartido}.", idPartido);
            return ResultadoServicio<Partido>.Invalido(string.Empty, "No se pudo guardar la fecha. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Partido>.Correcto(partido);
    }

    // ------------------------------------------------------------------
    // CAMBIO DE ESTADO
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Partido>> CambiarEstadoAsync(
        int idUsuario, string? codigoTorneo, int idPartido, EstadoPartido nuevoEstado)
    {
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);
        bool esOrganizador = torneo.IdOrganizador == idUsuario;
        bool esArbitro = partido.IdArbitro == idUsuario;

        // 1) QUIÉN: solo el organizador o el árbitro de ESTE partido. Quien ve el partido sin ser ninguno
        //    de los dos recibe 403. (Un árbitro de otro partido tampoco puede tocar este.)
        if (!esOrganizador && !esArbitro)
        {
            return ResultadoServicio<Partido>.SinPermiso();
        }

        // 2) CUÁNDO: un descanso no tiene estado que cambiar y con el torneo finalizado nada se modifica.
        //    (No se exige torneo EnCurso: con los datos de prueba del Paso 4 el torneo puede estar en
        //    otro estado. Cuando el Paso 6 genere los calendarios, los partidos solo existirán en EnCurso.)
        if (partido.EsDescanso)
        {
            return ResultadoServicio<Partido>.Invalido(string.Empty, "Un descanso no tiene estado que cambiar.");
        }

        if (torneo.Estado == EstadoTorneo.Finalizado)
        {
            return ResultadoServicio<Partido>.Invalido(string.Empty, "El torneo ya finalizó; no se pueden cambiar sus partidos.");
        }

        var estadoAnterior = partido.Estado;

        if (estadoAnterior == nuevoEstado)
        {
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                $"El partido ya está en estado \"{ReglasPartido.NombreEstado(nuevoEstado)}\".");
        }

        // 3) REGLA DE DATOS: la máquina de estados (copia en C# del trigger de la BD).
        if (!Partido.TransicionPermitida(estadoAnterior, nuevoEstado))
        {
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                $"Un partido \"{ReglasPartido.NombreEstado(estadoAnterior)}\" no puede pasar a \"{ReglasPartido.NombreEstado(nuevoEstado)}\".");
        }

        // 4) REGLA DE PERMISOS: lo que puede hacer cada rol (el árbitro no puede reabrir un Finalizado).
        if (!ReglasPartido.PuedeCambiar(estadoAnterior, nuevoEstado, esOrganizador, esArbitro))
        {
            return ResultadoServicio<Partido>.SinPermiso();
        }

        // 5) EFECTOS. Al pasar a EnVivo el CHECK de la BD exige marcador: si no lo tiene, empieza 0 - 0.
        //    Si ya lo tenía (venía de Suspendido o se reabrió un Finalizado) se conserva tal cual.
        partido.Estado = nuevoEstado;

        if (nuevoEstado == EstadoPartido.EnVivo && !partido.GolesLocal.HasValue)
        {
            partido.GolesLocal = 0;
            partido.GolesVisitante = 0;
        }

        // 6) HISTORIAL. Se agrega al MISMO contexto: SaveChanges guarda el cambio del partido y esta fila
        //    juntos, dentro de una sola transacción (o se guardan las dos cosas o ninguna).
        _contexto.CambiosEstadoPartido.Add(new CambioEstadoPartido
        {
            IdPartido = partido.IdPartido,
            IdUsuario = idUsuario,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = nuevoEstado,
            FechaHora = DateTime.UtcNow // el historial se guarda en UTC
        });

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (EsTransicionRechazada(ex))
        {
            // Alguien cambió el estado un instante antes (por ejemplo el organizador mientras el árbitro
            // tenía la pantalla abierta) y el trigger de la BD rechazó esta transición.
            return ResultadoServicio<Partido>.Invalido(
                string.Empty,
                "El estado del partido cambió mientras lo editabas. Actualiza la página e inténtalo de nuevo.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo cambiar el estado del partido {IdPartido}.", idPartido);
            return ResultadoServicio<Partido>.Invalido(string.Empty, "No se pudo cambiar el estado del partido. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Partido>.Correcto(partido);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>¿El error fue el RAISE(ABORT, 'Transicion de estado de partido no permitida') del trigger?</summary>
    private static bool EsTransicionRechazada(DbUpdateException ex) =>
        ex.InnerException is SqliteException sqlite &&
        sqlite.Message.Contains("Transicion de estado", StringComparison.OrdinalIgnoreCase);
}
