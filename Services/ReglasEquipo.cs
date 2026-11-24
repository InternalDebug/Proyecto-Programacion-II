using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

/// <summary>
/// Reglas y utilidades que comparten ServicioEquipos y ServicioJugadores.
/// (internal: solo se usa dentro del proyecto, no forma parte de la "API" de los servicios.)
/// </summary>
internal static class ReglasEquipo
{
    /// <summary>El código se guarda en MAYÚSCULAS: "abcd2345 " debe encontrar "ABCD2345".</summary>
    public static string NormalizarCodigo(string? codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Texto vacío o solo espacios -> null (columna opcional); si no, el texto recortado.</summary>
    public static string? LimpiarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// REGLA DE PERMISOS: solo el responsable del equipo (quien lo inscribió) o el organizador
    /// del torneo pueden editar o eliminar el equipo y sus jugadores.
    /// Requiere que equipo.Torneo esté cargado (Include).
    /// </summary>
    public static bool PuedeGestionar(Equipo equipo, int idUsuario) =>
        equipo.IdResponsable == idUsuario || equipo.Torneo!.IdOrganizador == idUsuario;

    /// <summary>true si el torneo ya empezó o terminó (hay calendario y partidos).</summary>
    public static bool TorneoEnJuego(EstadoTorneo estado) =>
        estado == EstadoTorneo.EnCurso || estado == EstadoTorneo.Finalizado;

    /// <summary>
    /// Respuesta cuando el usuario NO tiene permiso sobre un equipo.
    /// Si el equipo es visible para él (está Aprobado, o es su responsable u organizador) -> 403.
    /// Si no lo es (un equipo Pendiente o Rechazado ajeno) -> 404, para no revelar que existe.
    /// </summary>
    public static ResultadoServicio<T> Denegar<T>(Equipo equipo, int idUsuario) where T : class
    {
        bool puedeVerlo = equipo.EstadoInscripcion == EstadoInscripcion.Aprobado
                          || equipo.IdResponsable == idUsuario
                          || equipo.Torneo!.IdOrganizador == idUsuario;

        return puedeVerlo ? ResultadoServicio<T>.SinPermiso() : ResultadoServicio<T>.NoEncontrado();
    }

    /// <summary>
    /// Consulta base: el equipo con ese id SOLO si pertenece al torneo con ese código.
    /// Así un equipo nunca se alcanza "de lado" con el código de otro torneo.
    /// Ya trae el Torneo cargado (Include).
    /// </summary>
    public static IQueryable<Equipo> EquipoDeTorneo(this FairPlayContext contexto, string? codigoTorneo, int idEquipo)
    {
        var codigo = NormalizarCodigo(codigoTorneo);

        return contexto.Equipos
            .Include(e => e.Torneo)
            .Where(e => e.IdEquipo == idEquipo && e.Torneo!.Codigo == codigo);
    }

    /// <summary>true si faltan datos que el formulario debería haber exigido (alguien se saltó la validación).</summary>
    public static bool JugadorIncompleto(JugadorFormViewModel datos) =>
        datos.FechaNacimiento is null || datos.Dorsal is null;

    /// <summary>
    /// Copia los datos del formulario al jugador (crear y editar lo tratan igual).
    /// Llamar solo después de comprobar JugadorIncompleto.
    /// </summary>
    public static void AplicarDatosJugador(Jugador jugador, JugadorFormViewModel datos)
    {
        // El CHECK del SQL mide length(trim(...)): guardamos ya recortado para que coincida.
        jugador.Nombre = datos.Nombre.Trim();
        jugador.Apellido = datos.Apellido.Trim();
        jugador.FechaNacimiento = datos.FechaNacimiento!.Value;
        jugador.Posicion = datos.Posicion;
        jugador.Dorsal = datos.Dorsal!.Value;
        jugador.EsCapitan = datos.EsCapitan;
    }
}
