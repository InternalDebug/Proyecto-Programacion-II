namespace FairPlay.Models.Enums;

/// <summary>
/// Tipo de una fase del torneo.
/// SQL: fase.tipo  CHECK (tipo IN ('Liga', 'Eliminatoria', 'Suizo')).
///
/// Cómo se combinan según el formato del torneo:
///   Liga       -> 1 fase Liga
///   Copa       -> 1 fase Eliminatoria
///   GruposCopa -> fase 1 Liga (con grupos) + fase 2 Eliminatoria
///   Suizo      -> 1 fase Suizo
/// </summary>
public enum TipoFase
{
    /// <summary>Todos contra todos (puede tener grupos).</summary>
    Liga,

    /// <summary>Eliminación directa con cruces.</summary>
    Eliminatoria,

    /// <summary>Sistema suizo (usa num_rondas y nunca es ida y vuelta).</summary>
    Suizo
}
