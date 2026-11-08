namespace FairPlay.Models.Enums;

/// <summary>
/// Estado de la inscripción de un equipo en un torneo.
/// SQL: equipo.estado_inscripcion  CHECK (estado_inscripcion IN ('Pendiente', 'Aprobado', 'Rechazado'))
///      DEFAULT 'Pendiente'.
/// </summary>
public enum EstadoInscripcion
{
    /// <summary>Esperando la decisión del organizador (valor por defecto).</summary>
    Pendiente,

    /// <summary>Aceptado en el torneo.</summary>
    Aprobado,

    /// <summary>Rechazado por el organizador.</summary>
    Rechazado
}
