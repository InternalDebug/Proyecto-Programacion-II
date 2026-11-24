namespace FairPlay.ViewModels;

/// <summary>
/// Formulario de INSCRIPCIÓN de un equipo: los datos del equipo (heredados) más los datos del
/// PRIMER JUGADOR.
///
/// ¿Por qué el primer jugador va aquí? Porque un equipo debe tener al menos un jugador desde
/// que se inscribe. Equipo y jugador se guardan juntos, en una sola operación: o se crean los
/// dos o no se crea ninguno. (En modo Automática el equipo queda aprobado al instante, por lo que
/// no se puede "agregar el jugador después".)
/// </summary>
public class InscribirEquipoViewModel : EquipoFormViewModel
{
    /// <summary>Se inicializa vacío para que el formulario muestre sus campos.</summary>
    public JugadorFormViewModel PrimerJugador { get; set; } = new();
}
