using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// Reglas de los eventos de partido (goles y tarjetas) que comparten el servicio, el controlador y la vista.
/// Igual que ReglasPartido: están en un solo lugar para que la pantalla no ofrezca algo que el servicio
/// luego rechaza (el servicio vuelve a comprobar todo, porque un POST puede enviarse a mano).
/// </summary>
public static class ReglasEvento
{
    /// <summary>A la segunda amarilla del mismo jugador en un partido se le agrega una roja automática.</summary>
    public const int AmarillasParaExpulsion = 2;

    // ------------------------------------------------------------------
    // PERMISOS: quién puede registrar o eliminar eventos, y cuándo
    // ------------------------------------------------------------------

    /// <summary>
    /// Registrar o eliminar eventos:
    ///   Árbitro del partido -> solo con el partido EnVivo.
    ///   Organizador         -> EnVivo y también Finalizado (para corregir errores).
    ///   Un descanso no tiene eventos, y con el torneo Finalizado nada se modifica.
    /// </summary>
    public static bool PuedeModificar(Partido partido, EstadoTorneo estadoTorneo, bool esOrganizador, bool esArbitro)
    {
        if (partido.EsDescanso || estadoTorneo == EstadoTorneo.Finalizado)
        {
            return false;
        }

        if (esOrganizador)
        {
            return partido.Estado == EstadoPartido.EnVivo || partido.Estado == EstadoPartido.Finalizado;
        }

        return esArbitro && partido.Estado == EstadoPartido.EnVivo;
    }

    /// <summary>
    /// Mensaje que explica por qué un usuario con rol (organizador o árbitro) no puede modificar
    /// los eventos ahora; null si sí puede. Lo usa el servicio para devolver un error entendible.
    /// </summary>
    public static string? MotivoBloqueo(Partido partido, EstadoTorneo estadoTorneo, bool esOrganizador)
    {
        if (partido.EsDescanso)
        {
            return "Un descanso no tiene eventos.";
        }

        if (estadoTorneo == EstadoTorneo.Finalizado)
        {
            return "El torneo ya finalizó; no se pueden modificar los eventos de sus partidos.";
        }

        if (partido.Estado == EstadoPartido.EnVivo)
        {
            return null;
        }

        if (esOrganizador && partido.Estado == EstadoPartido.Finalizado)
        {
            return null;
        }

        return esOrganizador
            ? "Los eventos solo se registran con el partido En vivo (o se corrigen si está Finalizado)."
            : "Los eventos solo se pueden registrar con el partido En vivo.";
    }

    // ------------------------------------------------------------------
    // MARCADOR AUTOMÁTICO
    // ------------------------------------------------------------------

    /// <summary>
    /// Cuánto aporta un evento al marcador, como (golesLocal, golesVisitante):
    ///   Gol     -> suma al equipo del evento.
    ///   Autogol -> el evento guarda el equipo del jugador, pero el gol se suma al RIVAL.
    ///   Tarjetas -> no cambian el marcador.
    /// El servicio suma este valor al registrar un evento y lo resta al eliminarlo.
    /// </summary>
    public static (int Local, int Visitante) AporteAlMarcador(TipoEvento tipo, int idEquipoDelEvento, int idLocal)
    {
        bool esDelLocal = idEquipoDelEvento == idLocal;

        return tipo switch
        {
            TipoEvento.Gol => esDelLocal ? (1, 0) : (0, 1),
            TipoEvento.Autogol => esDelLocal ? (0, 1) : (1, 0),
            _ => (0, 0)
        };
    }

    // ------------------------------------------------------------------
    // DISCIPLINA
    // ------------------------------------------------------------------

    /// <summary>
    /// ¿Ya está expulsado el jugador para un evento nuevo en el minuto indicado?
    /// Si la roja tiene minuto y el evento nuevo es de un minuto igual o anterior, SÍ se permite:
    /// así el organizador puede corregir un evento olvidado que ocurrió antes de la expulsión.
    /// Si falta algún minuto no se puede saber el orden, así que se bloquea.
    /// </summary>
    public static bool EstaExpulsado(EventoPartido? roja, int? minutoNuevo) =>
        roja is not null
        && (roja.Minuto is null || minutoNuevo is null || minutoNuevo > roja.Minuto);

    // ------------------------------------------------------------------
    // Textos para mostrar
    // ------------------------------------------------------------------

    public static string NombreTipo(TipoEvento tipo) => tipo switch
    {
        TipoEvento.Gol => "Gol",
        TipoEvento.Autogol => "Autogol",
        TipoEvento.Amarilla => "Tarjeta amarilla",
        TipoEvento.Roja => "Tarjeta roja",
        _ => tipo.ToString()
    };
}
