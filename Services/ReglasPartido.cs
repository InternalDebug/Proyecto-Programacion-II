using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

/// <summary>
/// Reglas de los partidos que comparten el servicio, el controlador y las vistas.
/// Tenerlas en un solo lugar evita que la pantalla ofrezca un botón que el servicio luego rechaza
/// (igual que ReglasTorneo y ReglasEquipo en los pasos anteriores).
///
/// Hay DOS capas de reglas para cambiar el estado de un partido:
///   1) Regla de DATOS   -> Partido.TransicionPermitida (espejo del trigger trg_partido_transicion).
///   2) Regla de PERMISOS -> PuedeCambiar (esta clase): quién puede hacer cada transición.
/// Una transición debe pasar las dos.
/// </summary>
public static class ReglasPartido
{
    // ------------------------------------------------------------------
    // Consulta base y roles
    // ------------------------------------------------------------------

    /// <summary>
    /// Consulta base: el partido con ese id SOLO si pertenece al torneo con ese código.
    /// Así un partido nunca se alcanza "de lado" con el código de otro torneo.
    /// Ya trae Ronda -> Fase -> Torneo cargados (Include), que es lo que hace falta para saber
    /// quién es el organizador. Es la misma idea que ReglasEquipo.EquipoDeTorneo.
    /// </summary>
    public static IQueryable<Partido> PartidoDeTorneo(this FairPlayContext contexto, string? codigoTorneo, int idPartido)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);

        return contexto.Partidos
            .Include(p => p.Ronda).ThenInclude(r => r!.Fase).ThenInclude(f => f!.Torneo)
            .Where(p => p.IdPartido == idPartido && p.Ronda!.Fase!.Torneo!.Codigo == codigo);
    }

    /// <summary>Torneo del partido. Requiere que Ronda, Fase y Torneo estén cargados (PartidoDeTorneo lo hace).</summary>
    public static Torneo TorneoDe(Partido partido) => partido.Ronda!.Fase!.Torneo!;

    /// <summary>true si el usuario (null = visitante sin sesión) es el organizador del torneo del partido.</summary>
    public static bool EsOrganizador(Partido partido, int? idUsuario) =>
        idUsuario.HasValue && TorneoDe(partido).IdOrganizador == idUsuario.Value;

    /// <summary>true si el usuario (null = visitante sin sesión) es el árbitro designado de ESTE partido.</summary>
    public static bool EsArbitro(Partido partido, int? idUsuario) =>
        idUsuario.HasValue && partido.IdArbitro == idUsuario.Value;

    // ------------------------------------------------------------------
    // Permisos sobre el estado y los datos del partido
    // ------------------------------------------------------------------

    /// <summary>
    /// REGLA DE PERMISOS de las transiciones de estado.
    ///
    ///   Organizador -> todas las que permite la máquina de estados (incluida reabrir un Finalizado
    ///                  y volver a programar un Suspendido).
    ///   Árbitro     -> solo Programado -> EnVivo, EnVivo -> Finalizado y EnVivo -> Suspendido.
    ///                  NO puede retroceder de Finalizado a EnVivo (reabrir) ni reprogramar un Suspendido.
    ///   Cualquier otro usuario -> ninguna.
    /// </summary>
    public static bool PuedeCambiar(EstadoPartido actual, EstadoPartido nuevo, bool esOrganizador, bool esArbitro)
    {
        // Primero la regla de DATOS: si ni siquiera la BD lo permitiría, no se ofrece.
        if (actual == nuevo || !Partido.TransicionPermitida(actual, nuevo))
        {
            return false;
        }

        if (esOrganizador)
        {
            return true;
        }

        if (!esArbitro)
        {
            return false;
        }

        return (actual == EstadoPartido.Programado && nuevo == EstadoPartido.EnVivo)
            || (actual == EstadoPartido.EnVivo && (nuevo == EstadoPartido.Finalizado || nuevo == EstadoPartido.Suspendido));
    }

    /// <summary>
    /// Estados a los que este usuario puede pasar el partido AHORA (lista vacía = no hay botones).
    /// Un descanso (sin visitante) no tiene estados que cambiar, y con el torneo Finalizado nada se modifica
    /// (salvo reabrir la final de una copa, ver ReglasEliminatoria.PuedeReabrirFinal).
    /// </summary>
    public static IReadOnlyList<EstadoPartido> TransicionesDisponibles(
        Partido partido, EstadoTorneo estadoTorneo, bool esOrganizador, bool esArbitro)
    {
        if (partido.EsDescanso)
        {
            return Array.Empty<EstadoPartido>();
        }

        if (estadoTorneo == EstadoTorneo.Finalizado)
        {
            // Única excepción (Paso 6, entrega C): el organizador puede reabrir la final de una copa para
            // corregirla; el torneo vuelve entonces a estar En curso. Necesita el Cruce del partido cargado.
            return esOrganizador && ReglasEliminatoria.PuedeReabrirFinal(partido)
                ? new[] { EstadoPartido.EnVivo }
                : Array.Empty<EstadoPartido>();
        }

        return Enum.GetValues<EstadoPartido>()
            .Where(nuevo => PuedeCambiar(partido.Estado, nuevo, esOrganizador, esArbitro))
            .ToList();
    }

    /// <summary>La fecha y hora solo se cambian mientras el partido no se esté jugando ni haya terminado.</summary>
    public static bool PuedeEditarFecha(Partido partido, EstadoTorneo estadoTorneo) =>
        !partido.EsDescanso
        && estadoTorneo != EstadoTorneo.Finalizado
        && (partido.Estado == EstadoPartido.Programado || partido.Estado == EstadoPartido.Suspendido);

    /// <summary>El árbitro se puede designar o cambiar en cualquier partido con rival, salvo con el torneo Finalizado.</summary>
    public static bool PuedeAsignarArbitro(Partido partido, EstadoTorneo estadoTorneo) =>
        !partido.EsDescanso && estadoTorneo != EstadoTorneo.Finalizado;

    // ------------------------------------------------------------------
    // Textos y utilidades para mostrar
    // ------------------------------------------------------------------

    /// <summary>Nombre legible del estado, para los mensajes y las insignias.</summary>
    public static string NombreEstado(EstadoPartido estado) => estado switch
    {
        EstadoPartido.EnVivo => "En vivo",
        _ => estado.ToString()
    };

    /// <summary>Texto del botón que lleva del estado actual al nuevo.</summary>
    public static string TextoAccion(EstadoPartido actual, EstadoPartido nuevo) => (actual, nuevo) switch
    {
        (EstadoPartido.Programado, EstadoPartido.EnVivo) => "Iniciar partido",
        (EstadoPartido.EnVivo, EstadoPartido.Finalizado) => "Finalizar partido",
        (_, EstadoPartido.Suspendido) => "Suspender partido",
        (EstadoPartido.Suspendido, EstadoPartido.Programado) => "Volver a programar",
        (EstadoPartido.Finalizado, EstadoPartido.EnVivo) => "Reabrir partido",
        _ => NombreEstado(nuevo)
    };

    /// <summary>Marcador para mostrar: "2 - 1", "vs" si todavía no hay marcador, o "Descansa".</summary>
    public static string Marcador(Partido partido)
    {
        if (partido.EsDescanso)
        {
            return "Descansa";
        }

        return partido.GolesLocal.HasValue && partido.GolesVisitante.HasValue
            ? $"{partido.GolesLocal} - {partido.GolesVisitante}"
            : "vs";
    }

    /// <summary>Orden en "Mis partidos": primero lo que se está jugando, al final lo terminado.</summary>
    public static int OrdenEstado(EstadoPartido estado) => estado switch
    {
        EstadoPartido.EnVivo => 0,
        EstadoPartido.Programado => 1,
        EstadoPartido.Suspendido => 2,
        _ => 3
    };
}
