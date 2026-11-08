using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Enums;

/// <summary>
/// Posición de un jugador en la cancha.
/// SQL: jugador.posicion  CHECK (posicion IN ('Arquero', 'Defensa', 'Medio campo', 'Delantero')).
///
/// CASO ESPECIAL: en la base de datos el texto es "Medio campo" (con espacio), pero en C# un
/// miembro de enum no puede tener espacios. Por eso este enum NO usa la conversión automática
/// a string: usa el conversor PosicionJugadorConverter (Data/Conversores.cs), que traduce
/// MedioCampo &lt;-&gt; "Medio campo".
/// </summary>
public enum PosicionJugador
{
    Arquero,
    Defensa,

    /// <summary>Se guarda en la BD como "Medio campo".</summary>
    [Display(Name = "Medio campo")]
    MedioCampo,

    Delantero
}
