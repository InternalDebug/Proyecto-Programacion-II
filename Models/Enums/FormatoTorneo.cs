using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Enums;

/// <summary>
/// Formato general del torneo.
/// SQL: torneo.formato  CHECK (formato IN ('Liga', 'Copa', 'GruposCopa', 'Suizo')).
///
/// REGLA IMPORTANTE (se repite en todos los enums): el nombre de cada miembro debe ser
/// IGUAL al texto que permite el CHECK de SQLite, porque EF Core guarda el enum como
/// texto (HasConversion&lt;string&gt;, ver Data/FairPlayContext.cs).
/// </summary>
public enum FormatoTorneo
{
    /// <summary>Todos contra todos: el torneo tiene una sola fase de tipo Liga.</summary>
    Liga,

    /// <summary>Eliminación directa: una sola fase de tipo Eliminatoria.</summary>
    Copa,

    /// <summary>Fase de grupos (Liga con grupos) + fase eliminatoria.</summary>
    [Display(Name = "Grupos + Copa")]
    GruposCopa,

    /// <summary>Sistema suizo: una sola fase de tipo Suizo con número fijo de rondas.</summary>
    Suizo
}
