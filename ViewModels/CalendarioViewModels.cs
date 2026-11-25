using FairPlay.Models;
using FairPlay.Services;

namespace FairPlay.ViewModels;

/// <summary>Una fila de la pantalla "Preparar calendario": un equipo aprobado con su posición en el sorteo.</summary>
public class EquipoPreparacionViewModel
{
    public int IdEquipo { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public int Jugadores { get; set; }

    /// <summary>Posición (semilla) en el sorteo: 1 = primero. El organizador puede editarla.</summary>
    public int Posicion { get; set; }

    /// <summary>Lo que le falta al equipo para poder iniciar el torneo (vacía = equipo completo).</summary>
    public List<string> Problemas { get; set; } = new();
}

/// <summary>Datos de la pantalla "Preparar calendario" (iniciar el torneo o regenerar el calendario).</summary>
public class PreparacionCalendarioViewModel
{
    public Torneo Torneo { get; set; } = null!;

    /// <summary>true si el torneo ya está en curso y se está rehaciendo el calendario.</summary>
    public bool EsRegeneracion { get; set; }

    public bool IdaYVuelta { get; set; }

    /// <summary>Solicitudes pendientes que se rechazarán automáticamente al iniciar.</summary>
    public int PendientesARechazar { get; set; }

    /// <summary>Problemas que no son de un equipo en particular (por ejemplo, faltan equipos).</summary>
    public List<string> ProblemasGenerales { get; set; } = new();

    public List<EquipoPreparacionViewModel> Equipos { get; set; } = new();

    /// <summary>
    /// El botón "Iniciar" solo se ofrece si no hay ningún problema.
    /// (El servicio vuelve a comprobarlo: un POST puede enviarse a mano.)
    /// </summary>
    public bool PuedeIniciar => ProblemasGenerales.Count == 0 && Equipos.All(e => e.Problemas.Count == 0);

    /// <summary>
    /// Convierte el resultado del servicio en el modelo de la vista.
    /// Si se indica idaYVuelta se usa ese valor (lo que el organizador marcó) en lugar del actual.
    /// </summary>
    public static PreparacionCalendarioViewModel Desde(PreparacionCalendario datos, bool? idaYVuelta = null) => new()
    {
        Torneo = datos.Torneo,
        EsRegeneracion = datos.EsRegeneracion,
        IdaYVuelta = idaYVuelta ?? datos.IdaYVueltaActual,
        PendientesARechazar = datos.PendientesARechazar,
        ProblemasGenerales = datos.ProblemasGenerales.ToList(),
        Equipos = datos.Equipos
            .Select(e => new EquipoPreparacionViewModel
            {
                IdEquipo = e.IdEquipo,
                Nombre = e.Nombre,
                Jugadores = e.Jugadores,
                Posicion = e.Posicion,
                Problemas = e.Problemas.ToList()
            })
            .ToList()
    };
}

/// <summary>Una posición enviada por el formulario (campos Posiciones[0].IdEquipo y Posiciones[0].Posicion).</summary>
public class PosicionEquipoFormViewModel
{
    public int IdEquipo { get; set; }

    public int Posicion { get; set; }
}

/// <summary>Lo que envía el formulario de la pantalla "Preparar calendario".</summary>
public class IniciarCalendarioFormViewModel
{
    public bool IdaYVuelta { get; set; }

    public List<PosicionEquipoFormViewModel> Posiciones { get; set; } = new();
}

/// <summary>Datos del panel "Gestionar calendario" (solo organizador).</summary>
public class GestionCalendarioViewModel
{
    public Torneo Torneo { get; set; } = null!;

    public List<RondaGestion> Rondas { get; set; } = new();

    public bool PuedeRegenerar { get; set; }

    /// <summary>Por qué no se puede regenerar (null si sí se puede).</summary>
    public string? MotivoNoRegenerar { get; set; }
}

/// <summary>Lo que envía el formulario de edición de una jornada. La fecha es opcional.</summary>
public class EditarRondaFormViewModel
{
    public string? Nombre { get; set; }

    public DateOnly? FechaInicio { get; set; }
}
