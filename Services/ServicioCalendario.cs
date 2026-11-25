using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioCalendario : IServicioCalendario
{
    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioCalendario> _logger;

    // ASP.NET Core entrega los dos objetos solos (inyección de dependencias).
    public ServicioCalendario(FairPlayContext contexto, ILogger<ServicioCalendario> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // PREPARAR Y SORTEAR (no guardan nada)
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<PreparacionCalendario>> PrepararAsync(int idUsuario, string? codigoTorneo)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);
        var torneo = await _contexto.Torneos.AsNoTracking().FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<PreparacionCalendario>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el organizador prepara el calendario.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<PreparacionCalendario>.SinPermiso();
        }

        // REGLA DE ESTADO Y FORMATO.
        var (errorEstado, esRegeneracion) = await ComprobarEstadoAsync(torneo);

        if (errorEstado is not null)
        {
            return ResultadoServicio<PreparacionCalendario>.Invalido(string.Empty, errorEstado);
        }

        var aprobados = await _contexto.Equipos
            .AsNoTracking()
            .Include(e => e.Jugadores)
            .Where(e => e.IdTorneo == torneo.IdTorneo && e.EstadoInscripcion == EstadoInscripcion.Aprobado)
            .OrderBy(e => e.Nombre)
            .ToListAsync();

        int pendientes = await _contexto.Equipos
            .CountAsync(e => e.IdTorneo == torneo.IdTorneo && e.EstadoInscripcion == EstadoInscripcion.Pendiente);

        // Al regenerar se propone la misma modalidad (ida o ida y vuelta) que tenía el calendario actual.
        bool idaYVuelta = false;

        if (esRegeneracion)
        {
            idaYVuelta = await _contexto.Fases
                .Where(f => f.IdTorneo == torneo.IdTorneo)
                .OrderBy(f => f.Orden)
                .Select(f => (bool?)f.IdaYVuelta)
                .FirstOrDefaultAsync() ?? false;
        }

        // Posiciones iniciales: orden alfabético (1, 2, 3...). El organizador puede cambiarlas.
        var filas = new List<EquipoPreparacion>();
        int posicion = 1;

        foreach (var equipo in aprobados)
        {
            filas.Add(new EquipoPreparacion(
                equipo.IdEquipo,
                equipo.Nombre,
                equipo.Jugadores.Count,
                posicion++,
                ReglasCalendario.ProblemasDeEquipo(equipo)));
        }

        var generales = new List<string>();

        if (aprobados.Count < ReglasCalendario.MinEquiposLiga)
        {
            generales.Add($"Se necesitan al menos {ReglasCalendario.MinEquiposLiga} equipos aprobados (hay {aprobados.Count}).");
        }

        return ResultadoServicio<PreparacionCalendario>.Correcto(
            new PreparacionCalendario(torneo, esRegeneracion, idaYVuelta, filas, pendientes, generales));
    }

    public async Task<ResultadoServicio<PreparacionCalendario>> SortearAsync(int idUsuario, string? codigoTorneo)
    {
        var resultado = await PrepararAsync(idUsuario, codigoTorneo);

        if (!resultado.Exito)
        {
            return resultado;
        }

        var preparacion = resultado.Valor!;

        // Mezcla de Fisher-Yates (Random.Shared.Shuffle): todas las ordenaciones son igual de probables.
        var mezclados = preparacion.Equipos.ToArray();
        Random.Shared.Shuffle(mezclados);

        var conPosicion = mezclados
            .Select((e, indice) => e with { Posicion = indice + 1 })
            .ToList();

        return ResultadoServicio<PreparacionCalendario>.Correcto(preparacion with { Equipos = conPosicion });
    }

    // ------------------------------------------------------------------
    // INICIAR EL TORNEO / REGENERAR EL CALENDARIO
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<ResumenInicio>> IniciarAsync(
        int idUsuario, string? codigoTorneo, bool idaYVuelta, IReadOnlyList<PosicionEquipo> posiciones)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);

        // Con seguimiento (tracking): vamos a modificar el torneo.
        var torneo = await _contexto.Torneos.FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<ResumenInicio>.NoEncontrado();
        }

        // 1) PERMISO
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<ResumenInicio>.SinPermiso();
        }

        // 2) ESTADO Y FORMATO
        var (errorEstado, esRegeneracion) = await ComprobarEstadoAsync(torneo);

        if (errorEstado is not null)
        {
            return ResultadoServicio<ResumenInicio>.Invalido(string.Empty, errorEstado);
        }

        // 3) VALIDACIÓN DE DATOS: equipos aprobados completos y sorteo correcto.
        var aprobados = await _contexto.Equipos
            .Include(e => e.Jugadores)
            .Where(e => e.IdTorneo == torneo.IdTorneo && e.EstadoInscripcion == EstadoInscripcion.Aprobado)
            .ToListAsync();

        if (aprobados.Count < ReglasCalendario.MinEquiposLiga)
        {
            return ResultadoServicio<ResumenInicio>.Invalido(
                string.Empty,
                $"Se necesitan al menos {ReglasCalendario.MinEquiposLiga} equipos aprobados (hay {aprobados.Count}).");
        }

        var incompletos = aprobados
            .Select(e => (Nombre: e.Nombre, Problemas: ReglasCalendario.ProblemasDeEquipo(e)))
            .Where(x => x.Problemas.Count > 0)
            .ToList();

        if (incompletos.Count > 0)
        {
            var detalle = string.Join(" ", incompletos.Select(x => $"{x.Nombre}: {string.Join(" ", x.Problemas)}"));

            return ResultadoServicio<ResumenInicio>.Invalido(
                string.Empty,
                "No se puede iniciar el torneo porque hay equipos incompletos. " + detalle);
        }

        if (!ReglasCalendario.PosicionesValidas(posiciones, aprobados.Select(e => e.IdEquipo).ToList()))
        {
            return ResultadoServicio<ResumenInicio>.Invalido(
                string.Empty,
                $"El sorteo no es válido: cada equipo aprobado debe tener una posición distinta del 1 al {aprobados.Count}.");
        }

        // 4) EFECTOS: se arma todo en memoria y se guarda de una vez.
        var orden = posiciones.OrderBy(p => p.Posicion).Select(p => p.IdEquipo).ToList();
        var jornadas = GeneradorLiga.Generar(orden, idaYVuelta);

        var pendientes = await _contexto.Equipos
            .Where(e => e.IdTorneo == torneo.IdTorneo && e.EstadoInscripcion == EstadoInscripcion.Pendiente)
            .ToListAsync();

        // Las solicitudes pendientes se rechazan automáticamente al iniciar el torneo.
        foreach (var pendiente in pendientes)
        {
            pendiente.EstadoInscripcion = EstadoInscripcion.Rechazado;
        }

        torneo.Estado = EstadoTorneo.EnCurso;

        var fase = new Fase
        {
            IdTorneo = torneo.IdTorneo,
            Orden = 1,
            Nombre = "Liga",
            Tipo = TipoFase.Liga,
            IdaYVuelta = idaYVuelta,
            Estado = EstadoFase.EnCurso
        };

        // La semilla guarda la posición del sorteo.
        foreach (var posicion in posiciones)
        {
            fase.Participantes.Add(new ParticipanteFase { IdEquipo = posicion.IdEquipo, Semilla = posicion.Posicion });
        }

        foreach (var jornada in jornadas)
        {
            var ronda = new Ronda { Numero = jornada.Numero, Nombre = $"Jornada {jornada.Numero}" };

            foreach (var emparejamiento in jornada.Partidos)
            {
                bool descansa = emparejamiento.IdVisitante is null;

                // Un descanso se guarda 'Finalizado' y sin marcador (así lo exige el modelo de datos).
                ronda.Partidos.Add(new Partido
                {
                    IdLocal = emparejamiento.IdLocal,
                    IdVisitante = emparejamiento.IdVisitante,
                    Estado = descansa ? EstadoPartido.Finalizado : EstadoPartido.Programado
                });
            }

            fase.Rondas.Add(ronda);
        }

        _contexto.Fases.Add(fase);

        // Se usa una transacción explícita porque al regenerar hay DOS pasos: borrar la fase anterior
        // (con todas sus jornadas y partidos, en cascada) y crear la nueva. Si algo falla, no queda nada a medias.
        // (Borrar y crear en el mismo SaveChanges podría chocar con UNIQUE (id_torneo, orden).)
        await using var transaccion = await _contexto.Database.BeginTransactionAsync();

        try
        {
            if (esRegeneracion)
            {
                await _contexto.Fases.Where(f => f.IdTorneo == torneo.IdTorneo).ExecuteDeleteAsync();
            }

            await _contexto.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqliteException)
        {
            _logger.LogError(ex, "No se pudo generar el calendario del torneo {Codigo}.", torneo.Codigo);

            return ResultadoServicio<ResumenInicio>.Invalido(
                string.Empty,
                "No se pudo generar el calendario. No se hizo ningún cambio; inténtalo de nuevo.");
        }

        int partidos = jornadas.Sum(j => j.Partidos.Count(p => p.IdVisitante is not null));
        int descansos = jornadas.Sum(j => j.Partidos.Count(p => p.IdVisitante is null));

        return ResultadoServicio<ResumenInicio>.Correcto(
            new ResumenInicio(torneo, jornadas.Count, partidos, descansos, pendientes.Count));
    }

    // ------------------------------------------------------------------
    // PANEL DE GESTIÓN Y EDICIÓN DE JORNADAS
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<CalendarioGestion>> ObtenerGestionAsync(int idUsuario, string? codigoTorneo)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);
        var torneo = await _contexto.Torneos.AsNoTracking().FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<CalendarioGestion>.NoEncontrado();
        }

        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<CalendarioGestion>.SinPermiso();
        }

        var rondas = await _contexto.Rondas
            .AsNoTracking()
            .Where(r => r.Fase!.IdTorneo == torneo.IdTorneo)
            .OrderBy(r => r.Fase!.Orden)
            .ThenBy(r => r.IdGrupo)
            .ThenBy(r => r.Numero)
            .Select(r => new RondaGestion(
                r.IdRonda,
                r.Numero,
                r.Nombre,
                r.FechaInicio,
                r.Fase!.Nombre,
                r.Partidos.Count(p => p.IdVisitante != null),
                r.Partidos.Count(p => p.IdVisitante == null)))
            .ToListAsync();

        // ¿Se puede regenerar? Solo en curso, con un formato disponible y sin partidos jugados.
        string? motivo;
        bool puedeRegenerar = false;

        if (torneo.Estado != EstadoTorneo.EnCurso)
        {
            motivo = "El calendario solo se puede regenerar mientras el torneo está en curso.";
        }
        else if (!ReglasCalendario.FormatoDisponible(torneo.Formato))
        {
            motivo = "La regeneración de este formato todavía no está disponible.";
        }
        else
        {
            motivo = await MotivoNoRegenerableAsync(torneo.IdTorneo);
            puedeRegenerar = motivo is null;
        }

        return ResultadoServicio<CalendarioGestion>.Correcto(new CalendarioGestion(torneo, rondas, puedeRegenerar, motivo));
    }

    public async Task<ResultadoServicio<Ronda>> EditarRondaAsync(
        int idUsuario, string? codigoTorneo, int idRonda, string? nombre, DateOnly? fechaInicio)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);

        var ronda = await _contexto.Rondas
            .Include(r => r.Fase).ThenInclude(f => f!.Torneo)
            .FirstOrDefaultAsync(r => r.IdRonda == idRonda && r.Fase!.Torneo!.Codigo == codigo);

        if (ronda is null)
        {
            return ResultadoServicio<Ronda>.NoEncontrado();
        }

        var torneo = ronda.Fase!.Torneo!;

        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Ronda>.SinPermiso();
        }

        if (torneo.Estado == EstadoTorneo.Finalizado)
        {
            return ResultadoServicio<Ronda>.Invalido(string.Empty, "El torneo ya finalizó: no se pueden editar sus jornadas.");
        }

        var nombreLimpio = ReglasEquipo.LimpiarTexto(nombre);

        // Mismo límite que el CHECK de la BD: entre 1 y 60 caracteres.
        if (nombreLimpio is null || nombreLimpio.Length > 60)
        {
            return ResultadoServicio<Ronda>.Invalido("nombre", "El nombre de la jornada es obligatorio y puede tener hasta 60 caracteres.");
        }

        // Evita fechas absurdas por un formulario manipulado.
        if (fechaInicio.HasValue && (fechaInicio.Value.Year < 2000 || fechaInicio.Value.Year > 2100))
        {
            return ResultadoServicio<Ronda>.Invalido("fechaInicio", "La fecha de la jornada no es válida.");
        }

        ronda.Nombre = nombreLimpio;
        ronda.FechaInicio = fechaInicio; // opcional: null = sin fecha

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar la jornada {IdRonda}.", idRonda);
            return ResultadoServicio<Ronda>.Invalido(string.Empty, "No se pudieron guardar los cambios. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Ronda>.Correcto(ronda);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Decide si el torneo está en un estado desde el que se puede preparar/generar el calendario.
    /// Devuelve (mensaje de error o null, true si es una REGENERACIÓN de un torneo ya en curso).
    /// </summary>
    private async Task<(string? Error, bool EsRegeneracion)> ComprobarEstadoAsync(Torneo torneo)
    {
        if (!ReglasCalendario.FormatoDisponible(torneo.Formato))
        {
            return ($"El calendario del formato \"{ReglasCalendario.NombreFormato(torneo.Formato)}\" todavía no está disponible. "
                    + "Por ahora solo se puede iniciar un torneo de tipo Liga.", false);
        }

        switch (torneo.Estado)
        {
            case EstadoTorneo.Inscripcion:
                return (null, false);

            case EstadoTorneo.EnCurso:
            {
                var motivo = await MotivoNoRegenerableAsync(torneo.IdTorneo);
                return (motivo, motivo is null);
            }

            case EstadoTorneo.Borrador:
                return ("Primero abre las inscripciones del torneo: solo se puede iniciar desde el estado Inscripción.", false);

            default:
                return ("El torneo ya finalizó: no se puede generar su calendario.", false);
        }
    }

    /// <summary>
    /// Motivo por el que el calendario ya NO se puede regenerar (null = sí se puede): basta con que algún partido
    /// real esté iniciado, jugado o suspendido, tenga marcador, historial de estados o eventos.
    /// Los descansos (sin visitante) no cuentan: nacen 'Finalizado'.
    /// </summary>
    private async Task<string?> MotivoNoRegenerableAsync(int idTorneo)
    {
        bool hayActividad = await _contexto.Partidos.AnyAsync(p =>
            p.Ronda!.Fase!.IdTorneo == idTorneo
            && p.IdVisitante != null
            && (p.Estado != EstadoPartido.Programado
                || p.GolesLocal != null
                || p.CambiosEstado.Any()
                || p.Eventos.Any()));

        return hayActividad
            ? "Ya hay partidos iniciados, jugados o suspendidos: el calendario ya no se puede regenerar."
            : null;
    }
}
