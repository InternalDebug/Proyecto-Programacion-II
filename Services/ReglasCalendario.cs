using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// Reglas del calendario que comparten el servicio y las vistas (igual que ReglasTorneo o ReglasPartido):
/// así la pantalla no ofrece lo que el servicio rechazaría. El servicio SIEMPRE vuelve a comprobarlas,
/// porque un POST puede enviarse a mano.
/// </summary>
public static class ReglasCalendario
{
    /// <summary>Mínimo de jugadores que debe tener cada equipo para iniciar el torneo.</summary>
    public const int MinJugadoresPorEquipo = 7;

    /// <summary>Mínimo de equipos aprobados para generar una liga.</summary>
    public const int MinEquiposLiga = 2;

    /// <summary>Mínimo de equipos aprobados para generar una copa (eliminación directa): semifinales y final.</summary>
    public const int MinEquiposCopa = 4;

    /// <summary>
    /// Formatos cuyo calendario ya se puede generar: Liga y Copa. Grupos + Copa y Suizo llegan en las
    /// siguientes entregas del Paso 6.
    /// </summary>
    public static bool FormatoDisponible(FormatoTorneo formato) =>
        formato == FormatoTorneo.Liga || formato == FormatoTorneo.Copa;

    /// <summary>Mínimo de equipos aprobados que exige el formato para poder iniciar el torneo.</summary>
    public static int MinEquipos(FormatoTorneo formato) =>
        formato == FormatoTorneo.Copa ? MinEquiposCopa : MinEquiposLiga;

    public static string NombreFormato(FormatoTorneo formato) => formato switch
    {
        FormatoTorneo.GruposCopa => "Grupos + Copa",
        _ => formato.ToString()
    };

    /// <summary>
    /// Problemas que impiden iniciar el torneo con este equipo (lista vacía = equipo completo).
    /// El equipo debe venir con sus Jugadores cargados (Include).
    /// </summary>
    public static List<string> ProblemasDeEquipo(Equipo equipo)
    {
        var problemas = new List<string>();
        int cantidad = equipo.Jugadores.Count;

        if (cantidad < MinJugadoresPorEquipo)
        {
            problemas.Add($"Tiene {cantidad} jugador(es); el mínimo es {MinJugadoresPorEquipo}.");
        }

        if (!equipo.Jugadores.Any(j => j.EsCapitan))
        {
            problemas.Add("Falta designar al capitán.");
        }

        if (!equipo.Jugadores.Any(j => j.Posicion == PosicionJugador.Arquero))
        {
            problemas.Add("Falta al menos un portero (arquero).");
        }

        return problemas;
    }

    /// <summary>
    /// true si las posiciones del sorteo son correctas: una por cada equipo aprobado (ni más ni menos,
    /// sin repetir equipos) y los números son exactamente 1, 2, 3... N sin repetir ni saltarse ninguno.
    /// </summary>
    public static bool PosicionesValidas(IReadOnlyList<PosicionEquipo> posiciones, IReadOnlyCollection<int> idsAprobados)
    {
        if (posiciones.Count != idsAprobados.Count)
        {
            return false;
        }

        var idsRecibidos = posiciones.Select(p => p.IdEquipo).ToHashSet();

        if (idsRecibidos.Count != posiciones.Count || !idsRecibidos.SetEquals(idsAprobados))
        {
            return false;
        }

        // Los números ordenados deben ser exactamente 1..N.
        return posiciones
            .Select(p => p.Posicion)
            .Order()
            .SequenceEqual(Enumerable.Range(1, posiciones.Count));
    }
}
