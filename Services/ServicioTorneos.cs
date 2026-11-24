using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioTorneos : IServicioTorneos
{
    /// <summary>Cuántas veces se intenta generar un código antes de rendirse (la colisión es rarísima).</summary>
    private const int MaxIntentosCodigo = 5;

    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioTorneos> _logger;

    // ASP.NET Core entrega ambos objetos solos (inyección de dependencias).
    public ServicioTorneos(FairPlayContext contexto, ILogger<ServicioTorneos> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // CONSULTAS
    // ------------------------------------------------------------------

    public async Task<List<Torneo>> ObtenerDeOrganizadorAsync(int idOrganizador)
    {
        return await _contexto.Torneos
            .AsNoTracking() // solo lectura: EF no necesita "seguir" estos objetos
            .Where(t => t.IdOrganizador == idOrganizador)
            .OrderByDescending(t => t.IdTorneo) // el más reciente primero
            .ToListAsync();
    }

    public async Task<Torneo?> ObtenerPorCodigoAsync(string? codigo)
    {
        var buscado = NormalizarCodigo(codigo);

        // Un código con otra longitud no puede existir: ahorramos la consulta a la base de datos.
        if (buscado.Length != Torneo.LongitudCodigo)
        {
            return null;
        }

        // Include(Organizador) trae al usuario para mostrar su nombre. Las vistas solo deben
        // mostrar Organizador.NombreUsuario (nunca el correo ni el hash).
        return await _contexto.Torneos
            .AsNoTracking()
            .Include(t => t.Organizador)
            .FirstOrDefaultAsync(t => t.Codigo == buscado);
    }

    public async Task<ResultadoServicio<Torneo>> ObtenerParaGestionAsync(int idUsuario, string? codigo)
    {
        var buscado = NormalizarCodigo(codigo);

        if (buscado.Length != Torneo.LongitudCodigo)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        // Se traen los equipos y las fases para que la pantalla de eliminar pueda avisar
        // cuántos datos se borrarán en cascada. AsSplitQuery evita multiplicar filas al unir
        // dos colecciones en una sola consulta.
        var torneo = await _contexto.Torneos
            .AsNoTracking()
            .Include(t => t.Equipos)
            .Include(t => t.Fases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Codigo == buscado);

        if (torneo is null)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el organizador gestiona su torneo.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Torneo>.SinPermiso();
        }

        return ResultadoServicio<Torneo>.Correcto(torneo);
    }

    // ------------------------------------------------------------------
    // CREAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Torneo>> CrearAsync(int idUsuario, TorneoFormViewModel datos)
    {
        for (int intento = 1; intento <= MaxIntentosCodigo; intento++)
        {
            // 1) El código lo genera el servidor, nunca el formulario.
            var codigo = Torneo.GenerarCodigo();

            // 2) Revisión previa para evitar casi siempre la colisión (hay 32^8 combinaciones).
            if (await _contexto.Torneos.AnyAsync(t => t.Codigo == codigo))
            {
                continue;
            }

            // 3) El organizador es quien creó el torneo; el estado inicial es Borrador.
            var torneo = new Torneo
            {
                IdOrganizador = idUsuario,
                Codigo = codigo,
                Estado = EstadoTorneo.Borrador
            };
            AplicarDatos(torneo, datos);

            _contexto.Torneos.Add(torneo);

            try
            {
                await _contexto.SaveChangesAsync();
                return ResultadoServicio<Torneo>.Correcto(torneo);
            }
            catch (DbUpdateException ex) when (EsColisionDeCodigo(ex))
            {
                // Dos personas generaron el mismo código a la vez y el UNIQUE de la BD rechazó el segundo.
                // Soltamos el objeto fallido y repetimos con un código nuevo.
                _contexto.Entry(torneo).State = EntityState.Detached;
                _logger.LogWarning("Colisión de código de torneo ({Codigo}); intento {Intento}.", codigo, intento);
            }
            catch (DbUpdateException ex)
            {
                // Cualquier otro rechazo de la BD (un CHECK, una clave foránea...).
                _contexto.Entry(torneo).State = EntityState.Detached;
                _logger.LogError(ex, "No se pudo crear el torneo del usuario {IdUsuario}.", idUsuario);
                return ResultadoServicio<Torneo>.Invalido(
                    string.Empty,
                    "No se pudo crear el torneo. Revisa los datos e inténtalo de nuevo.");
            }
        }

        return ResultadoServicio<Torneo>.Invalido(
            string.Empty,
            "No se pudo generar un código único para el torneo. Inténtalo de nuevo.");
    }

    // ------------------------------------------------------------------
    // CAMBIAR ESTADO (abrir / cerrar inscripciones)
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Torneo>> CambiarEstadoAsync(int idUsuario, string? codigo, EstadoTorneo nuevoEstado)
    {
        var buscado = NormalizarCodigo(codigo);

        var torneo = await _contexto.Torneos.FirstOrDefaultAsync(t => t.Codigo == buscado);

        if (torneo is null)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el organizador abre o cierra las inscripciones.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Torneo>.SinPermiso();
        }

        // REGLA DE TRANSICIÓN: solo Borrador <-> Inscripcion (ver ReglasTorneo.EstadoSiguiente).
        var permitido = ReglasTorneo.EstadoSiguiente(torneo.Estado);

        if (permitido is null)
        {
            return ResultadoServicio<Torneo>.Invalido(
                string.Empty,
                $"El torneo está en estado \"{ReglasTorneo.NombreEstado(torneo.Estado)}\": desde aquí ya no se puede abrir ni cerrar la inscripción.");
        }

        if (permitido.Value != nuevoEstado)
        {
            return ResultadoServicio<Torneo>.Invalido(
                string.Empty,
                $"Desde \"{ReglasTorneo.NombreEstado(torneo.Estado)}\" solo se puede pasar a \"{ReglasTorneo.NombreEstado(permitido.Value)}\".");
        }

        torneo.Estado = nuevoEstado;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo cambiar el estado del torneo {Codigo}.", buscado);
            return ResultadoServicio<Torneo>.Invalido(string.Empty, "No se pudo cambiar el estado del torneo. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Torneo>.Correcto(torneo);
    }

    // ------------------------------------------------------------------
    // EDITAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Torneo>> EditarAsync(int idUsuario, string? codigo, TorneoFormViewModel datos)
    {
        var buscado = NormalizarCodigo(codigo);

        // Aquí SÍ queremos que EF siga el objeto (sin AsNoTracking) para que guarde los cambios.
        var torneo = await _contexto.Torneos.FirstOrDefaultAsync(t => t.Codigo == buscado);

        if (torneo is null)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: se comprueba otra vez aquí aunque el controlador ya lo hizo al mostrar el formulario,
        // porque alguien podría enviar el POST directamente sin pasar por esa pantalla.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Torneo>.SinPermiso();
        }

        // REGLA: el formato solo se puede cambiar mientras el torneo es un Borrador
        // (después ya habrá fases, grupos y partidos generados según ese formato).
        if (datos.Formato != torneo.Formato && torneo.Estado != EstadoTorneo.Borrador)
        {
            return ResultadoServicio<Torneo>.Invalido(
                nameof(TorneoFormViewModel.Formato),
                "El formato solo se puede cambiar mientras el torneo está en borrador.");
        }

        // REGLA: max_equipos no existe como regla en la BD (ni en ningún trigger), así que la aplicamos aquí:
        // el nuevo máximo no puede ser menor que los equipos ya APROBADOS (los pendientes y rechazados no cuentan).
        if (datos.MaximoEquipos.HasValue)
        {
            int aprobados = await _contexto.Equipos.CountAsync(e =>
                e.IdTorneo == torneo.IdTorneo && e.EstadoInscripcion == EstadoInscripcion.Aprobado);

            if (datos.MaximoEquipos.Value < aprobados)
            {
                return ResultadoServicio<Torneo>.Invalido(
                    nameof(TorneoFormViewModel.MaximoEquipos),
                    $"Ya hay {aprobados} equipos aprobados; el máximo no puede ser menor.");
            }
        }

        AplicarDatos(torneo, datos);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo editar el torneo {Codigo}.", buscado);
            return ResultadoServicio<Torneo>.Invalido(
                string.Empty,
                "No se pudieron guardar los cambios. Revisa los datos e inténtalo de nuevo.");
        }

        return ResultadoServicio<Torneo>.Correcto(torneo);
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Torneo>> EliminarAsync(int idUsuario, string? codigo)
    {
        var buscado = NormalizarCodigo(codigo);

        var torneo = await _contexto.Torneos.FirstOrDefaultAsync(t => t.Codigo == buscado);

        if (torneo is null)
        {
            return ResultadoServicio<Torneo>.NoEncontrado();
        }

        // REGLA DE PERMISOS: solo el organizador elimina su torneo.
        if (torneo.IdOrganizador != idUsuario)
        {
            return ResultadoServicio<Torneo>.SinPermiso();
        }

        // Al borrar el torneo, la BD borra en cascada sus fases, equipos, jugadores, rondas y partidos
        // (ON DELETE CASCADE del script SQL).
        _contexto.Torneos.Remove(torneo);

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el torneo {Codigo}.", buscado);
            return ResultadoServicio<Torneo>.Invalido(
                string.Empty,
                "No se pudo eliminar el torneo porque tiene datos relacionados que lo impiden.");
        }

        return ResultadoServicio<Torneo>.Correcto(torneo);
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// El código se guarda siempre en MAYÚSCULAS, así que quien escriba "abcd2345 " debe encontrar "ABCD2345".
    /// </summary>
    private static string NormalizarCodigo(string? codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Copia los datos del formulario al torneo. Se usa al crear y al editar, así las dos
    /// operaciones tratan los datos exactamente igual. NO toca IdOrganizador, Codigo ni Estado.
    /// </summary>
    private static void AplicarDatos(Torneo torneo, TorneoFormViewModel datos)
    {
        // El CHECK del SQL mide length(trim(nombre)); guardamos ya recortado para que coincida.
        torneo.Nombre = datos.Nombre.Trim();

        // Descripción vacía o solo espacios -> NULL en la BD.
        torneo.Descripcion = string.IsNullOrWhiteSpace(datos.Descripcion) ? null : datos.Descripcion.Trim();

        torneo.Formato = datos.Formato;
        torneo.ModoInscripcion = datos.ModoInscripcion;
        torneo.MaxEquipos = datos.MaximoEquipos; // null = sin límite de equipos

        // Los puntajes son obligatorios en el formulario; los valores por defecto (3, 1, 0)
        // solo se usarían si alguien se saltara la validación.
        torneo.PuntosVictoria = datos.PuntosVictoria ?? 3;
        torneo.PuntosEmpate = datos.PuntosEmpate ?? 1;
        torneo.PuntosDerrota = datos.PuntosDerrota ?? 0;

        torneo.FechaInicio = datos.FechaInicio;
        torneo.FechaFin = datos.FechaFin;
    }

    /// <summary>
    /// ¿El error fue "UNIQUE constraint failed: torneo.codigo"?
    /// SQLite usa el código de error 19 para cualquier violación de restricciones.
    /// </summary>
    private static bool EsColisionDeCodigo(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite &&
        sqlite.Message.Contains("torneo.codigo", StringComparison.OrdinalIgnoreCase);
}
