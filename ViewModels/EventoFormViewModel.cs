using FairPlay.Models.Enums;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario "Registrar evento" del detalle del partido.
/// Todas las propiedades son opcionales a nivel de formulario porque las reglas dependen del tipo de
/// evento y las valida el servicio (ServicioEventos), que es quien devuelve un mensaje claro.
/// </summary>
public class EventoFormViewModel
{
    /// <summary>Gol, Autogol, Amarilla o Roja.</summary>
    public TipoEvento? Tipo { get; set; }

    /// <summary>
    /// Jugador protagonista. Es obligatorio salvo en un Gol sin jugador (solo suma al marcador).
    /// Si se indica, el equipo del evento sale del jugador y IdEquipo se ignora.
    /// </summary>
    public int? IdJugador { get; set; }

    /// <summary>Equipo al que se acredita el gol. Solo se usa en un Gol SIN jugador.</summary>
    public int? IdEquipo { get; set; }

    /// <summary>Minuto del partido (0 a 130). Opcional.</summary>
    public int? Minuto { get; set; }
}
