using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioJugadores : IServicioJugadores
{
    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioJugadores> _logger;

    public ServicioJugadores(FairPlayContext contexto, ILogger<ServicioJugadores> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // AGREGAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Equipo>> ObtenerEquipoParaAgregarAsync(int idUsuario, string? codigoTorneo, int idEquipo)
    {
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo).AsNoTracking().FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        if (!ReglasEquipo.PuedeGestionar(equipo, idUsuario))
        {
            return ReglasEquipo.Denegar<Equipo>(equipo, idUsuario);
        }

        var bloqueo = MotivoTorneoBloqueado(equipo.Torneo!);

        return bloqueo is null
            ? ResultadoServicio<Equipo>.Correcto(equipo)
            : ResultadoServicio<Equipo>.Invalido(string.Empty, bloqueo);
    }

    public async Task<ResultadoServicio<Jugador>> AgregarAsync(int idUsuario, string? codigoTorneo, int idEquipo, JugadorFormViewModel datos)
    {
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo).AsNoTracking().FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Jugador>.NoEncontrado();
        }

        // REGLA DE PERMISOS (se comprueba otra vez: el POST puede enviarse a mano, sin pasar por el GET).
        if (!ReglasEquipo.PuedeGestionar(equipo, idUsuario))
        {
            return ReglasEquipo.Denegar<Jugador>(equipo, idUsuario);
        }

        var bloqueo = MotivoTorneoBloqueado(equipo.Torneo!);
        if (bloqueo is not null)
        {
            return ResultadoServicio<Jugador>.Invalido(string.Empty, bloqueo);
        }

        if (ReglasEquipo.JugadorIncompleto(datos))
        {
            return ResultadoServicio<Jugador>.Invalido(string.Empty, "Faltan datos obligatorios del jugador.");
        }

        // REGLA: el dorsal no se repite dentro del equipo (UNIQUE (id_equipo, dorsal) del SQL).
        int dorsal = datos.Dorsal!.Value;

        if (await _contexto.Jugadores.AnyAsync(j => j.IdEquipo == equipo.IdEquipo && j.Dorsal == dorsal))
        {
            return ResultadoServicio<Jugador>.Invalido(
                nameof(JugadorFormViewModel.Dorsal),
                $"El dorsal {dorsal} ya lo usa otro jugador del equipo.");
        }

        var jugador = new Jugador { IdEquipo = equipo.IdEquipo };
        ReglasEquipo.AplicarDatosJugador(jugador, datos);

        try
        {
            await GuardarCuidandoCapitanAsync(jugador, esNuevo: true);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo agregar un jugador al equipo {IdEquipo}.", idEquipo);
            return ResultadoServicio<Jugador>.Invalido(
                EsDorsalDuplicado(ex) ? nameof(JugadorFormViewModel.Dorsal) : string.Empty,
                EsDorsalDuplicado(ex)
                    ? $"El dorsal {dorsal} ya lo usa otro jugador del equipo."
                    : "No se pudo agregar el jugador. Revisa los datos e inténtalo de nuevo.");
        }

        return ResultadoServicio<Jugador>.Correcto(jugador);
    }

    // ------------------------------------------------------------------
    // EDITAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Jugador>> ObtenerParaGestionAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador)
    {
        var jugador = await BuscarJugadorAsync(codigoTorneo, idEquipo, idJugador, seguimiento: false);

        if (jugador is null)
        {
            return ResultadoServicio<Jugador>.NoEncontrado();
        }

        if (!ReglasEquipo.PuedeGestionar(jugador.Equipo!, idUsuario))
        {
            return ReglasEquipo.Denegar<Jugador>(jugador.Equipo!, idUsuario);
        }

        return ResultadoServicio<Jugador>.Correcto(jugador);
    }

    public async Task<ResultadoServicio<Jugador>> EditarAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador, JugadorFormViewModel datos)
    {
        // Con seguimiento: EF guarda los cambios que hagamos sobre este objeto.
        var jugador = await BuscarJugadorAsync(codigoTorneo, idEquipo, idJugador, seguimiento: true);

        if (jugador is null)
        {
            return ResultadoServicio<Jugador>.NoEncontrado();
        }

        if (!ReglasEquipo.PuedeGestionar(jugador.Equipo!, idUsuario))
        {
            return ReglasEquipo.Denegar<Jugador>(jugador.Equipo!, idUsuario);
        }

        var bloqueo = MotivoTorneoBloqueado(jugador.Equipo!.Torneo!);
        if (bloqueo is not null)
        {
            return ResultadoServicio<Jugador>.Invalido(string.Empty, bloqueo);
        }

        if (ReglasEquipo.JugadorIncompleto(datos))
        {
            return ResultadoServicio<Jugador>.Invalido(string.Empty, "Faltan datos obligatorios del jugador.");
        }

        // El dorsal no se repite, sin contar al propio jugador.
        int dorsal = datos.Dorsal!.Value;

        if (await _contexto.Jugadores.AnyAsync(j =>
                j.IdEquipo == jugador.IdEquipo && j.IdJugador != jugador.IdJugador && j.Dorsal == dorsal))
        {
            return ResultadoServicio<Jugador>.Invalido(
                nameof(JugadorFormViewModel.Dorsal),
                $"El dorsal {dorsal} ya lo usa otro jugador del equipo.");
        }

        ReglasEquipo.AplicarDatosJugador(jugador, datos);

        try
        {
            await GuardarCuidandoCapitanAsync(jugador, esNuevo: false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo editar el jugador {IdJugador}.", idJugador);
            return ResultadoServicio<Jugador>.Invalido(
                EsDorsalDuplicado(ex) ? nameof(JugadorFormViewModel.Dorsal) : string.Empty,
                EsDorsalDuplicado(ex)
                    ? $"El dorsal {dorsal} ya lo usa otro jugador del equipo."
                    : "No se pudieron guardar los cambios. Revisa los datos e inténtalo de nuevo.");
        }

        return ResultadoServicio<Jugador>.Correcto(jugador);
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Jugador>> EliminarAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador)
    {
        var jugador = await BuscarJugadorAsync(codigoTorneo, idEquipo, idJugador, seguimiento: true);

        if (jugador is null)
        {
            return ResultadoServicio<Jugador>.NoEncontrado();
        }

        if (!ReglasEquipo.PuedeGestionar(jugador.Equipo!, idUsuario))
        {
            return ReglasEquipo.Denegar<Jugador>(jugador.Equipo!, idUsuario);
        }

        var bloqueo = MotivoTorneoBloqueado(jugador.Equipo!.Torneo!);
        if (bloqueo is not null)
        {
            return ResultadoServicio<Jugador>.Invalido(string.Empty, bloqueo);
        }

        // REGLA: un equipo debe conservar al menos un jugador.
        int totalJugadores = await _contexto.Jugadores.CountAsync(j => j.IdEquipo == jugador.IdEquipo);

        if (totalJugadores <= 1)
        {
            return ResultadoServicio<Jugador>.Invalido(
                string.Empty,
                "Un equipo debe tener al menos un jugador. Agrega otro antes de eliminar a este, o elimina el equipo completo.");
        }

        _contexto.Jugadores.Remove(jugador);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // evento_partido.id_jugador es NO ACTION: un jugador con goles o tarjetas registrados no se puede borrar.
            _logger.LogError(ex, "No se pudo eliminar el jugador {IdJugador}.", idJugador);
            return ResultadoServicio<Jugador>.Invalido(
                string.Empty,
                "No se pudo eliminar al jugador porque tiene eventos de partido registrados (goles o tarjetas).");
        }

        return ResultadoServicio<Jugador>.Correcto(jugador);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Busca el jugador SOLO si pertenece al equipo indicado Y ese equipo al torneo con ese código.
    /// Trae también el Equipo y su Torneo (los necesitan las reglas de permisos).
    /// </summary>
    private async Task<Jugador?> BuscarJugadorAsync(string? codigoTorneo, int idEquipo, int idJugador, bool seguimiento)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);

        IQueryable<Jugador> consulta = _contexto.Jugadores
            .Include(j => j.Equipo)
            .ThenInclude(e => e!.Torneo)
            .Where(j => j.IdJugador == idJugador
                        && j.IdEquipo == idEquipo
                        && j.Equipo!.Torneo!.Codigo == codigo);

        if (!seguimiento)
        {
            consulta = consulta.AsNoTracking();
        }

        return await consulta.FirstOrDefaultAsync();
    }

    /// <summary>Con el torneo finalizado ya no se tocan los planteles.</summary>
    private static string? MotivoTorneoBloqueado(Torneo torneo) =>
        torneo.Estado == EstadoTorneo.Finalizado
            ? "El torneo ya finalizó; no se pueden modificar los jugadores."
            : null;

    /// <summary>
    /// Guarda el jugador asegurando UN SOLO capitán por equipo.
    /// El SQL tiene un índice único parcial (uq_jugador_capitan): si hubiera dos capitanes a la vez, la BD
    /// rechazaría el guardado. Por eso, si este jugador es capitán, PRIMERO se desmarca al anterior y
    /// después se guarda este, todo dentro de una transacción (si algo falla, se deshace todo).
    /// </summary>
    private async Task GuardarCuidandoCapitanAsync(Jugador jugador, bool esNuevo)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync();

        if (jugador.EsCapitan)
        {
            // Un jugador nuevo tiene IdJugador = 0, así que "distinto de 0" desmarca a todos los demás.
            await _contexto.Jugadores
                .Where(j => j.IdEquipo == jugador.IdEquipo && j.EsCapitan && j.IdJugador != jugador.IdJugador)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(j => j.EsCapitan, false));
        }

        if (esNuevo)
        {
            _contexto.Jugadores.Add(jugador);
        }

        await _contexto.SaveChangesAsync();
        await transaccion.CommitAsync();
        // Si SaveChanges lanza una excepción, la transacción se descarta sola al salir (rollback).
    }

    /// <summary>¿El error fue "UNIQUE constraint failed: jugador.id_equipo, jugador.dorsal"?</summary>
    private static bool EsDorsalDuplicado(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite &&
        sqlite.Message.Contains("jugador.dorsal", StringComparison.OrdinalIgnoreCase);
}
