using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Enums;

/// <summary>
/// Quién puede inscribir equipos y cómo se aprueban.
/// SQL: torneo.modo_inscripcion  CHECK (modo_inscripcion IN ('Cerrada', 'ConAprobacion', 'Automatica'))
///      DEFAULT 'Cerrada'.
/// </summary>
public enum ModoInscripcion
{
    /// <summary>Solo el organizador inscribe equipos y jugadores (valor por defecto; torneo "privado").</summary>
    Cerrada,

    /// <summary>Otros usuarios inscriben su equipo y el organizador debe aprobarlo.</summary>
    [Display(Name = "Con aprobación")]
    ConAprobacion,

    /// <summary>Otros usuarios inscriben su equipo y queda aprobado automáticamente.</summary>
    [Display(Name = "Automática")]
    Automatica
}
