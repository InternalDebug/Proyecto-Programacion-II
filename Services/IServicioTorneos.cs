using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de los torneos. Aquí viven las reglas; el controlador solo traduce
/// el resultado a una respuesta HTTP.
///
/// IMPORTANTE: el torneo se identifica siempre por su CÓDIGO de 8 caracteres, nunca por su
/// id numérico. Como el id es 1, 2, 3... cualquiera podría recorrerlos y ver todos los
/// torneos, y el diseño dice que un torneo solo se ve si se conoce su código.
/// </summary>
public interface IServicioTorneos
{
    /// <summary>Torneos que organiza el usuario, del más nuevo al más antiguo.</summary>
    Task<List<Torneo>> ObtenerDeOrganizadorAsync(int idOrganizador);

    /// <summary>
    /// Busca un torneo por su código (ignora mayúsculas y espacios). Devuelve null si no existe.
    /// Es pública: no comprueba permisos, porque conocer el código es la "llave" para verlo.
    /// </summary>
    Task<Torneo?> ObtenerPorCodigoAsync(string? codigo);

    /// <summary>
    /// Igual que la anterior pero para EDITAR o ELIMINAR: además comprueba que el usuario sea
    /// el organizador. Devuelve NoEncontrado, SinPermiso o Correcto (con el torneo y sus equipos y fases).
    /// </summary>
    Task<ResultadoServicio<Torneo>> ObtenerParaGestionAsync(int idUsuario, string? codigo);

    /// <summary>Crea un torneo nuevo; el organizador es el usuario y el código lo genera el servidor.</summary>
    Task<ResultadoServicio<Torneo>> CrearAsync(int idUsuario, TorneoFormViewModel datos);

    /// <summary>
    /// Abre o cierra las inscripciones: Borrador -> Inscripcion o Inscripcion -> Borrador.
    /// Solo su organizador. Los demás estados (EnCurso, Finalizado) los manejará el calendario (paso 6).
    /// </summary>
    Task<ResultadoServicio<Torneo>> CambiarEstadoAsync(int idUsuario, string? codigo, EstadoTorneo nuevoEstado);

    /// <summary>Modifica un torneo. Solo su organizador puede hacerlo.</summary>
    Task<ResultadoServicio<Torneo>> EditarAsync(int idUsuario, string? codigo, TorneoFormViewModel datos);

    /// <summary>Elimina un torneo (y, en cascada, sus fases, equipos y partidos). Solo su organizador.</summary>
    Task<ResultadoServicio<Torneo>> EliminarAsync(int idUsuario, string? codigo);
}
