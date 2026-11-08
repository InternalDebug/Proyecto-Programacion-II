namespace FairPlay.Models.Enums;

/// <summary>
/// Estado de una fase.
/// SQL: fase.estado  CHECK (estado IN ('Pendiente', 'EnCurso', 'Cerrada'))  DEFAULT 'Pendiente'.
/// </summary>
public enum EstadoFasec:\Users\Pachacuti\Desktop\Models\Enums\EstadoInscripcion.cs
{
    /// <summary>Aún no empieza (valor por defecto).</summary>
    Pendiente,

    /// <summary>Se están jugando sus partidos.</summary>
    EnCurso,

    /// <summary>Todos sus partidos terminaron.</summary>
    Cerrada
}
