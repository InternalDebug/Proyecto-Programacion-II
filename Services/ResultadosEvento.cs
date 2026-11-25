using FairPlay.Models;

namespace FairPlay.Services;

/// <summary>
/// Resultado de registrar o eliminar un evento: el evento afectado, si intervino la regla de la
/// doble amarilla y cómo quedó el marcador (para el mensaje al usuario).
/// </summary>
/// <param name="Evento">El evento registrado (o el que se eliminó).</param>
/// <param name="RojaAutomatica">
/// Al REGISTRAR: true si la segunda amarilla creó además una roja automática.
/// Al ELIMINAR: true si, al quitar la segunda amarilla, se quitó también esa roja automática.
/// </param>
/// <param name="Marcador">Marcador del partido después del cambio, por ejemplo "2 - 1".</param>
public record ResultadoEvento(EventoPartido Evento, bool RojaAutomatica, string Marcador);

/// <summary>Jugadores de cada equipo del partido, para armar el formulario de eventos (ordenados por dorsal).</summary>
public record PlantillasPartido(List<Jugador> Local, List<Jugador> Visitante);
