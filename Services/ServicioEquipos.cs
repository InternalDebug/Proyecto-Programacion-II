using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioEquipos : IServicioEquipos
{
    private readonly FairPlayContext _contexto;
    private readonly IAlmacenEscudos _escudos;
    private readonly ILogger<ServicioEquipos> _logger;

    // ASP.NET Core entrega los tres objetos solos (inyección de dependencias).
    public ServicioEquipos(FairPlayContext contexto, IAlmacenEscudos escudos, ILogger<ServicioEquipos> logger)
    {
        _contexto = contexto;
        _escudos = escudos;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // CONSULTAS
    // ------------------------------------------------------------------

    public async Task<List<Equipo>> ListarVisiblesAsync(int idTorneo, int? idUsuario, bool esOrganizador)
    {
        var consulta = _contexto.Equipos
            .AsNoTracking()
            .Include(e => e.Responsable)
            .Include(e => e.Jugadores)
            .Where(e => e.IdTorneo == idTorneo);

        // El organizador ve todos. Los demás: los Aprobados y los equipos que ellos mismos inscribieron.
        if (!esOrganizador)
        {
            consulta = consulta.Where(e =>
                e.EstadoInscripcion == EstadoInscripcion.Aprobado || e.IdResponsable == idUsuario);
        }

        return await consulta.OrderBy(e => e.Nombre).ToListAsync();
    }

    public async Task<ResultadoServicio<Equipo>> ObtenerParaVerAsync(int? idUsuario, string? codigoTorneo, int idEquipo)
    {
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo)
            .Include(e => e.Responsable)
            .Include(e => e.Jugadores)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        bool esOrganizador = idUsuario.HasValue && equipo.Torneo!.IdOrganizador == idUsuario.Value;
        bool esResponsable = idUsuario.HasValue && equipo.IdResponsable == idUsuario.Value;

        // Un equipo Pendiente o Rechazado solo lo ve su responsable o el organizador. Para el resto: 404.
        if (equipo.EstadoInscripcion != EstadoInscripcion.Aprobado && !esOrganizador && !esResponsable)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    public async Task<ResultadoServicio<Equipo>> ObtenerParaGestionAsync(int idUsuario, string? codigoTorneo, int idEquipo)
    {
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo)
            .Include(e => e.Jugadores) // la pantalla de eliminar avisa cuántos jugadores se borrarán
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el responsable del equipo o el organizador del torneo.
        if (!ReglasEquipo.PuedeGestionar(equipo, idUsuario))
        {
            return ReglasEquipo.Denegar<Equipo>(equipo, idUsuario);
        }

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    public async Task<List<Equipo>> ListarSolicitudesAsync(int idOrganizador)
    {
        return await _contexto.Equipos
            .AsNoTracking()
            .Include(e => e.Torneo)
            .Include(e => e.Responsable)
            .Include(e => e.Jugadores)
            .Where(e => e.Torneo!.IdOrganizador == idOrganizador
                        && e.EstadoInscripcion != EstadoInscripcion.Aprobado)
            .OrderBy(e => e.IdTorneo)
            .ThenBy(e => e.EstadoInscripcion) // "Pendiente" va antes que "Rechazado"
            .ThenBy(e => e.Nombre)
            .ToListAsync();
    }

    public async Task<ResumenSolicitudes> ObtenerResumenSolicitudesAsync(int idUsuario)
    {
        int pendientes = await _contexto.Equipos.CountAsync(e =>
            e.Torneo!.IdOrganizador == idUsuario && e.EstadoInscripcion == EstadoInscripcion.Pendiente);

        // El enlace aparece si hay solicitudes por resolver, o si el usuario tiene un torneo
        // abierto a inscripciones con aprobación (aunque todavía no haya llegado ninguna).
        bool mostrar = pendientes > 0 || await _contexto.Torneos.AnyAsync(t =>
            t.IdOrganizador == idUsuario
            && t.ModoInscripcion == ModoInscripcion.ConAprobacion
            && t.Estado == EstadoTorneo.Inscripcion);

        return new ResumenSolicitudes(mostrar, pendientes);
    }

    // ------------------------------------------------------------------
    // INSCRIBIR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Torneo>> ObtenerTorneoParaInscribirAsync(int idUsuario, string? codigoTorneo)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);
        var torneo = await _contexto.Torneos.AsNoTracking().FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        var motivo = await MotivoNoPuedeInscribirAsync(torneo, idUsuario);

        return motivo is null
            ? ResultadoServicio<Torneo>.Correcto(torneo)
            : ResultadoServicio<Torneo>.Invalido(string.Empty, motivo);
    }

    public async Task<ResultadoServicio<Equipo>> InscribirAsync(int idUsuario, string? codigoTorneo, InscribirEquipoViewModel datos)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);
        var torneo = await _contexto.Torneos.AsNoTracking().FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        // 1) REGLAS DE INSCRIPCIÓN: modo (Cerrada / ConAprobación / Automática), estado y cupo.
        //    Se comprueban otra vez aquí aunque el controlador ya lo hizo al mostrar el formulario,
        //    porque alguien podría enviar el POST directamente.
        var motivo = await MotivoNoPuedeInscribirAsync(torneo, idUsuario);
        if (motivo is not null)
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, motivo);
        }

        // 2) Datos del primer jugador completos (el formulario ya lo exige; esto cubre un POST manipulado).
        if (ReglasEquipo.JugadorIncompleto(datos.PrimerJugador))
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "Debes indicar los datos completos del primer jugador.");
        }

        // 3) Nombre único dentro del torneo (la BD usa COLLATE NOCASE: "Rojos" y "rojos" son el mismo).
        var nombre = datos.Nombre.Trim();

        if (await _contexto.Equipos.AnyAsync(e => e.IdTorneo == torneo.IdTorneo && e.Nombre == nombre))
        {
            return ResultadoServicio<Equipo>.Invalido(
                nameof(EquipoFormViewModel.Nombre),
                "Ya existe un equipo con ese nombre en este torneo.");
        }

        // 4) Escudo (opcional). Se guarda en el disco ANTES de la BD; si la BD falla, se borra.
        string? rutaEscudo = null;

        if (datos.Escudo is { Length: > 0 })
        {
            var (ruta, error) = await _escudos.GuardarAsync(datos.Escudo);

            if (error is not null)
            {
                return ResultadoServicio<Equipo>.Invalido(nameof(EquipoFormViewModel.Escudo), error);
            }

            rutaEscudo = ruta;
        }

        // 5) ESTADO DE LA INSCRIPCIÓN según el modo del torneo:
        //    - Organizador: queda Aprobado (él decide quién entra).
        //    - Automática:  queda Aprobado.
        //    - ConAprobación: queda Pendiente hasta que el organizador decida.
        //    (Cerrada con un usuario que no es el organizador ya se rechazó en el paso 1.)
        bool esOrganizador = torneo.IdOrganizador == idUsuario;
        var estado = esOrganizador || torneo.ModoInscripcion == ModoInscripcion.Automatica
            ? EstadoInscripcion.Aprobado
            : EstadoInscripcion.Pendiente;

        var equipo = new Equipo
        {
            IdTorneo = torneo.IdTorneo,
            IdResponsable = idUsuario, // quien inscribe es el responsable del equipo
            Nombre = nombre,
            Ciudad = ReglasEquipo.LimpiarTexto(datos.Ciudad),
            LogoEscudo = rutaEscudo,
            EstadoInscripcion = estado
        };

        var primerJugador = new Jugador();
        ReglasEquipo.AplicarDatosJugador(primerJugador, datos.PrimerJugador);
        equipo.Jugadores.Add(primerJugador);

        // Al agregar el equipo, EF agrega también su jugador, y un solo SaveChanges los guarda
        // dentro de una misma transacción: o se guardan los dos o ninguno.
        _contexto.Equipos.Add(equipo);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (EsNombreDuplicado(ex))
        {
            // Dos personas usaron el mismo nombre a la vez y el UNIQUE de la BD rechazó el segundo.
            _escudos.Eliminar(rutaEscudo);
            return ResultadoServicio<Equipo>.Invalido(
                nameof(EquipoFormViewModel.Nombre),
                "Ya existe un equipo con ese nombre en este torneo.");
        }
        catch (DbUpdateException ex)
        {
            _escudos.Eliminar(rutaEscudo);
            _logger.LogError(ex, "No se pudo inscribir el equipo {Nombre} del usuario {IdUsuario}.", nombre, idUsuario);
            return ResultadoServicio<Equipo>.Invalido(
                string.Empty,
                "No se pudo inscribir el equipo. Revisa los datos e inténtalo de nuevo.");
        }

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    // ------------------------------------------------------------------
    // EDITAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Equipo>> EditarAsync(int idUsuario, string? codigoTorneo, int idEquipo, EquipoFormViewModel datos)
    {
        // Aquí SÍ queremos que EF siga el objeto (sin AsNoTracking) para que guarde los cambios.
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo).FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        // REGLA DE PERMISOS (se comprueba otra vez aunque el GET ya lo hizo: el POST puede enviarse a mano).
        if (!ReglasEquipo.PuedeGestionar(equipo, idUsuario))
        {
            return ReglasEquipo.Denegar<Equipo>(equipo, idUsuario);
        }

        if (equipo.Torneo!.Estado == EstadoTorneo.Finalizado)
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "El torneo ya finalizó; no se pueden modificar sus equipos.");
        }

        // Nombre único en el torneo, sin contar al propio equipo.
        var nombre = datos.Nombre.Trim();

        if (await _contexto.Equipos.AnyAsync(e =>
                e.IdTorneo == equipo.IdTorneo && e.IdEquipo != equipo.IdEquipo && e.Nombre == nombre))
        {
            return ResultadoServicio<Equipo>.Invalido(
                nameof(EquipoFormViewModel.Nombre),
                "Ya existe otro equipo con ese nombre en este torneo.");
        }

        // Escudo: subir uno nuevo, quitar el actual, o dejarlo como está.
        string? rutaAnterior = equipo.LogoEscudo;
        string? rutaNueva = rutaAnterior;
        bool escudoCambia = false;

        if (datos.Escudo is { Length: > 0 })
        {
            var (ruta, error) = await _escudos.GuardarAsync(datos.Escudo);

            if (error is not null)
            {
                return ResultadoServicio<Equipo>.Invalido(nameof(EquipoFormViewModel.Escudo), error);
            }

            rutaNueva = ruta;
            escudoCambia = true;
        }
        else if (datos.QuitarEscudo && rutaAnterior is not null)
        {
            rutaNueva = null;
            escudoCambia = true;
        }

        // NO se toca IdTorneo, IdResponsable ni EstadoInscripcion: eso no lo decide el formulario.
        equipo.Nombre = nombre;
        equipo.Ciudad = ReglasEquipo.LimpiarTexto(datos.Ciudad);
        equipo.LogoEscudo = rutaNueva;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Si ya habíamos guardado un escudo nuevo en el disco, lo borramos: quedaría huérfano.
            if (escudoCambia && rutaNueva is not null)
            {
                _escudos.Eliminar(rutaNueva);
            }

            _logger.LogError(ex, "No se pudo editar el equipo {IdEquipo}.", idEquipo);

            return EsNombreDuplicado(ex)
                ? ResultadoServicio<Equipo>.Invalido(nameof(EquipoFormViewModel.Nombre), "Ya existe otro equipo con ese nombre en este torneo.")
                : ResultadoServicio<Equipo>.Invalido(string.Empty, "No se pudieron guardar los cambios. Revisa los datos e inténtalo de nuevo.");
        }

        // El escudo anterior solo se borra del disco cuando el cambio YA quedó guardado en la BD.
        if (escudoCambia)
        {
            _escudos.Eliminar(rutaAnterior);
        }

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Equipo>> EliminarAsync(int idUsuario, string? codigoTorneo, int idEquipo)
    {
        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo).FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        if (!ReglasEquipo.PuedeGestionar(equipo, idUsuario))
        {
            return ReglasEquipo.Denegar<Equipo>(equipo, idUsuario);
        }

        // Con el torneo en juego el equipo ya tiene calendario y partidos: no se puede quitar.
        if (ReglasEquipo.TorneoEnJuego(equipo.Torneo!.Estado))
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "El torneo ya empezó; no se pueden eliminar equipos.");
        }

        var rutaEscudo = equipo.LogoEscudo;

        // La BD borra en cascada sus jugadores (ON DELETE CASCADE del script SQL).
        _contexto.Equipos.Remove(equipo);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el equipo {IdEquipo}.", idEquipo);
            return ResultadoServicio<Equipo>.Invalido(
                string.Empty,
                "No se pudo eliminar el equipo porque tiene datos relacionados (por ejemplo, partidos).");
        }

        _escudos.Eliminar(rutaEscudo); // ya no hace falta su imagen

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    // ------------------------------------------------------------------
    // APROBAR / RECHAZAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Equipo>> DecidirInscripcionAsync(
        int idUsuario, string? codigoTorneo, int idEquipo, EstadoInscripcion decision)
    {
        // Solo existen dos decisiones; "Pendiente" no es una decisión.
        if (decision != EstadoInscripcion.Aprobado && decision != EstadoInscripcion.Rechazado)
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "La decisión no es válida.");
        }

        var equipo = await _contexto.EquipoDeTorneo(codigoTorneo, idEquipo).FirstOrDefaultAsync();

        if (equipo is null)
        {
            return ResultadoServicio<Equipo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el ORGANIZADOR decide (ni siquiera el responsable del equipo).
        if (equipo.Torneo!.IdOrganizador != idUsuario)
        {
            return ReglasEquipo.Denegar<Equipo>(equipo, idUsuario);
        }

        if (ReglasEquipo.TorneoEnJuego(equipo.Torneo.Estado))
        {
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "El torneo ya empezó; ya no se pueden cambiar las inscripciones.");
        }

        if (equipo.EstadoInscripcion == decision)
        {
            return ResultadoServicio<Equipo>.Invalido(
                string.Empty,
                decision == EstadoInscripcion.Aprobado ? "Este equipo ya está aprobado." : "Este equipo ya está rechazado.");
        }

        if (decision == EstadoInscripcion.Aprobado)
        {
            // REGLA: un equipo debe tener al menos un jugador para ser aprobado.
            if (!await _contexto.Jugadores.AnyAsync(j => j.IdEquipo == equipo.IdEquipo))
            {
                return ResultadoServicio<Equipo>.Invalido(string.Empty, "El equipo no tiene jugadores: necesita al menos uno para ser aprobado.");
            }

            // REGLA: el máximo de equipos solo cuenta los APROBADOS (sin contar a este).
            if (equipo.Torneo.MaxEquipos.HasValue)
            {
                int aprobados = await ContarAprobadosAsync(equipo.IdTorneo, excluirIdEquipo: equipo.IdEquipo);

                if (aprobados >= equipo.Torneo.MaxEquipos.Value)
                {
                    return ResultadoServicio<Equipo>.Invalido(
                        string.Empty,
                        $"El torneo ya tiene {aprobados} equipos aprobados (el máximo es {equipo.Torneo.MaxEquipos.Value}).");
                }
            }
        }

        equipo.EstadoInscripcion = decision;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo cambiar la inscripción del equipo {IdEquipo}.", idEquipo);
            return ResultadoServicio<Equipo>.Invalido(string.Empty, "No se pudo guardar la decisión. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Equipo>.Correcto(equipo);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Reúne TODAS las reglas de "¿puede este usuario inscribir un equipo ahora?".
    /// Devuelve el motivo (para mostrárselo al usuario) o null si sí puede.
    /// </summary>
    private async Task<string?> MotivoNoPuedeInscribirAsync(Torneo torneo, int idUsuario)
    {
        bool esOrganizador = torneo.IdOrganizador == idUsuario;

        switch (torneo.Estado)
        {
            case EstadoTorneo.Borrador when !esOrganizador:
                // El organizador sí puede ir preparando el torneo mientras es borrador.
                return "El organizador todavía no abrió las inscripciones de este torneo.";

            case EstadoTorneo.Inscripcion when torneo.ModoInscripcion == ModoInscripcion.Cerrada && !esOrganizador:
                return "La inscripción de este torneo es cerrada: solo el organizador puede inscribir equipos.";

            case EstadoTorneo.EnCurso or EstadoTorneo.Finalizado:
                return "El torneo ya no admite nuevos equipos.";
        }

        // REGLA: max_equipos no existe en la BD, se aplica aquí. Solo cuentan los equipos APROBADOS.
        if (torneo.MaxEquipos.HasValue)
        {
            int aprobados = await ContarAprobadosAsync(torneo.IdTorneo);

            if (aprobados >= torneo.MaxEquipos.Value)
            {
                return $"El torneo ya alcanzó su máximo de {torneo.MaxEquipos.Value} equipos.";
            }
        }

        return null;
    }

    /// <summary>Cuenta los equipos Aprobados del torneo (opcionalmente sin contar uno).</summary>
    private async Task<int> ContarAprobadosAsync(int idTorneo, int excluirIdEquipo = 0)
    {
        // Los ids empiezan en 1, así que excluir el 0 equivale a "no excluir a nadie".
        return await _contexto.Equipos.CountAsync(e =>
            e.IdTorneo == idTorneo
            && e.EstadoInscripcion == EstadoInscripcion.Aprobado
            && e.IdEquipo != excluirIdEquipo);
    }

    /// <summary>¿El error fue "UNIQUE constraint failed: equipo.id_torneo, equipo.nombre"? (código 19 = violación de restricción).</summary>
    private static bool EsNombreDuplicado(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite &&
        sqlite.Message.Contains("equipo.nombre", StringComparison.OrdinalIgnoreCase);
}
