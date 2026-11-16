using System.Globalization;
using FairPlay.Models.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FairPlay.Data;

// ============================================================================================
// CONVERSORES DE VALORES
// SQLite solo tiene 5 tipos (NULL, INTEGER, REAL, TEXT, BLOB). Un conversor le dice a EF Core
// cómo traducir un tipo de C# al texto exacto que espera el script SQL y viceversa.
// Se usan desde Data/FairPlayContext.cs.
// ============================================================================================

/// <summary>
/// PosicionJugador &lt;-&gt; texto de la columna jugador.posicion.
/// El CHECK del SQL permite 'Arquero', 'Defensa', 'Medio campo' y 'Delantero'. Tres de ellos
/// coinciden con el nombre del miembro del enum, pero "Medio campo" lleva un espacio y en C#
/// el miembro se llama MedioCampo; por eso este enum necesita un conversor propio.
/// </summary>
public class PosicionJugadorConverter : ValueConverter<PosicionJugador, string>
{
    /// <summary>Texto que guarda la BD para "Medio campo".</summary>
    public const string TextoMedioCampo = "Medio campo";

    public PosicionJugadorConverter()
        : base(
            posicion => PosicionJugadorConverter.ATexto(posicion),
            texto => PosicionJugadorConverter.APosicion(texto))
    {
    }

    /// <summary>Enum de C# -> texto que acepta el CHECK.</summary>
    public static string ATexto(PosicionJugador posicion) =>
        posicion == PosicionJugador.MedioCampo ? TextoMedioCampo : posicion.ToString();

    /// <summary>Texto de la BD -> enum de C#.</summary>
    public static PosicionJugador APosicion(string texto) =>
        texto == TextoMedioCampo ? PosicionJugador.MedioCampo : Enum.Parse<PosicionJugador>(texto);
}

/// <summary>
/// DateTime &lt;-&gt; texto ISO-8601 con 'T', por ejemplo "2026-10-26T15:30:00".
/// Es la convención del script para fechas con hora (se usa en partido.fecha_hora).
/// Al leer acepta también el formato con espacio ("2026-10-26 15:30:00").
/// </summary>
public class FechaHoraIsoConverter : ValueConverter<DateTime, string>
{
    public FechaHoraIsoConverter()
        : base(
            fecha => fecha.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            texto => DateTime.Parse(texto, CultureInfo.InvariantCulture))
    {
    }
}

/// <summary>
/// DateTime &lt;-&gt; texto "yyyy-MM-dd HH:mm:ss", el MISMO formato que produce datetime('now') de
/// SQLite (UTC). Se usa en las columnas con DEFAULT (datetime('now')): usuario.fecha_registro y
/// cambio_estado_partido.fecha_hora. Así los registros insertados por el script SQL y los
/// insertados por la aplicación se ordenan correctamente como texto.
/// </summary>
public class FechaHoraSqliteConverter : ValueConverter<DateTime, string>
{
    public FechaHoraSqliteConverter()
        : base(
            fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            texto => DateTime.Parse(texto, CultureInfo.InvariantCulture))
    {
    }
}
