using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Enums;

/// <summary>
/// Estado de un partido.
/// SQL: partido.estado, cambio_estado_partido.estado_anterior y estado_nuevo
///      CHECK (... IN ('Programado', 'EnVivo', 'Finalizado', 'Suspendido')).
///
/// El trigger trg_partido_transicion de la BD solo permite estas transiciones
/// (ver Partido.TransicionPermitida):
///   Programado -> EnVivo | Suspendido
///   EnVivo     -> Finalizado | Suspendido
///   Finalizado -> EnVivo            (reabrir)
///   Suspendido -> Programado
/// </summary>
public enum EstadoPartido
{
    /// <summary>Todavía no se juega (valor por defecto). Los goles son NULL.</summary>
    Programado,

    /// <summary>Se está jugando. Aquí el árbitro puede registrar goles y tarjetas.</summary>
    [Display(Name = "En vivo")]
    EnVivo,

    /// <summary>Terminó. Solo los partidos Finalizado cuentan en la tabla de posiciones.</summary>
    Finalizado,

    /// <summary>Suspendido; se puede volver a Programado.</summary>
    Suspendido
}
