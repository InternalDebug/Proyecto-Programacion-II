using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// Reglas del estado de un torneo que comparten el servicio y el controlador.
/// Tenerlas en un solo lugar evita que la pantalla ofrezca un botón que el servicio luego rechaza.
/// </summary>
public static class ReglasTorneo
{
    /// <summary>
    /// Estado al que el organizador puede pasar AHORA desde el estado actual (o null si no hay).
    ///   Borrador    -> Inscripcion  (abrir inscripciones)
    ///   Inscripcion -> Borrador     (cerrar inscripciones)
    /// EnCurso y Finalizado no se cambian a mano: lo hará la generación del calendario (paso 6).
    /// </summary>
    public static EstadoTorneo? EstadoSiguiente(EstadoTorneo actual) => actual switch
    {
        EstadoTorneo.Borrador => EstadoTorneo.Inscripcion,
        EstadoTorneo.Inscripcion => EstadoTorneo.Borrador,
        _ => null
    };

    /// <summary>Nombre legible del estado, para los mensajes al usuario.</summary>
    public static string NombreEstado(EstadoTorneo estado) => estado switch
    {
        EstadoTorneo.Borrador => "Borrador",
        EstadoTorneo.Inscripcion => "Inscripción",
        EstadoTorneo.EnCurso => "En curso",
        EstadoTorneo.Finalizado => "Finalizado",
        _ => estado.ToString()
    };
}
