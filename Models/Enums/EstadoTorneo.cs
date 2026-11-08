using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Enums;

/// <summary>
/// Ciclo de vida de un torneo.
/// SQL: torneo.estado  CHECK (estado IN ('Borrador', 'Inscripcion', 'EnCurso', 'Finalizado'))
///      DEFAULT 'Borrador'.
/// El primer miembro (Borrador) es el valor por defecto de C#, igual que el DEFAULT del SQL.
/// </summary>
public enum EstadoTorneo
{
    /// <summary>El organizador todavía lo está configurando (valor por defecto).</summary>
    Borrador,

    /// <summary>Se aceptan inscripciones de equipos.</summary>
    [Display(Name = "Inscripción")]
    Inscripcion,

    /// <summary>Ya se están jugando partidos.</summary>
    [Display(Name = "En curso")]
    EnCurso,

    /// <summary>Terminó el torneo.</summary>
    Finalizado
}
