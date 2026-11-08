using System.ComponentModel.DataAnnotations;

namespace FairPlay.Models.Validaciones;

/// <summary>
/// Valida la longitud de un texto IGNORANDO los espacios del inicio y del final.
///
/// ¿Por qué existe? Muchos CHECK del script SQL usan  length(trim(columna)) BETWEEN min AND max
/// (por ejemplo torneo.nombre BETWEEN 3 AND 100). El atributo estándar [StringLength] cuenta los
/// espacios, así que "   a" pasaría la validación de C# pero la base de datos lo rechazaría.
/// Con este atributo la validación de C# es idéntica a la del CHECK.
///
/// Uso:  [LongitudRecortada(3, 100)]
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class LongitudRecortadaAttribute : ValidationAttribute
{
    /// <summary>Longitud mínima (después de recortar espacios).</summary>
    public int Minimo { get; }

    /// <summary>Longitud máxima (después de recortar espacios).</summary>
    public int Maximo { get; }

    public LongitudRecortadaAttribute(int minimo, int maximo)
        : base("El campo {0} debe tener entre {1} y {2} caracteres (sin contar espacios al inicio y al final).")
    {
        Minimo = minimo;
        Maximo = maximo;
    }

    /// <summary>Rellena {0} = nombre del campo, {1} = mínimo, {2} = máximo.</summary>
    public override string FormatErrorMessage(string name) =>
        string.Format(ErrorMessageString, name, Minimo, Maximo);

    public override bool IsValid(object? value)
    {
        // Un valor nulo lo atrapa [Required] (o el "no nulo" implícito de MVC), no este atributo.
        if (value is null)
        {
            return true;
        }

        int largo = (value.ToString() ?? string.Empty).Trim().Length;
        return largo >= Minimo && largo <= Maximo;
    }
}
