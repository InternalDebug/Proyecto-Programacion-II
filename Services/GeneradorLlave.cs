namespace FairPlay.Services;

/// <summary>
/// Un cruce de la llave recién generada. PasaDirecto = true cuando el equipo A no tiene rival en esta ronda
/// (bye): avanza solo a la ronda siguiente. IdEquipoA / IdEquipoB son null cuando el equipo aún está por definir
/// (el ganador de un cruce anterior todavía no se conoce).
/// </summary>
public record CruceGenerado(int Posicion, int? IdEquipoA, int? IdEquipoB, bool PasaDirecto);

/// <summary>Una ronda de la llave (octavos, cuartos, semifinales, final...) con sus cruces en orden de posición.</summary>
public record RondaGenerada(int Numero, string Nombre, IReadOnlyList<CruceGenerado> Cruces);

/// <summary>
/// Genera la llave (cuadro) de una eliminación directa. Es una clase pura (no usa la base de datos):
/// recibe los ids de los equipos ordenados por semilla (el primero es la semilla 1) y devuelve las rondas.
///
/// Cómo funciona:
///   1) Se busca la potencia de 2 más cercana por arriba (el "tamaño" de la llave): 6 equipos -> 8, 4 -> 4.
///   2) Las semillas se colocan con la SIEMBRA ESTÁNDAR: la semilla 1 y la 2 quedan en mitades opuestas
///      (solo pueden verse en la final), la 1 enfrenta a la última, la 2 a la penúltima, etc.
///   3) Las "semillas" que sobran (tamaño - equipos) no existen: sus rivales pasan DIRECTO a la siguiente
///      ronda (bye). Siempre son las mejores semillas, y nunca hay un cruce con dos huecos.
///   4) Cada cruce de la posición p avanza al cruce de la posición (p + 1) / 2 de la ronda siguiente:
///      si p es impar ocupa el lugar A de ese cruce, si es par el lugar B.
///
/// Los byes solo existen en la primera ronda. Sus equipos ya se colocan en la segunda ronda aquí mismo.
/// </summary>
public static class GeneradorLlave
{
    /// <summary>Equipos mínimos que admite el generador (el torneo Copa pide más; ver ReglasCalendario).</summary>
    public const int MinEquipos = 2;

    public static List<RondaGenerada> Generar(IReadOnlyList<int> idsPorSemilla)
    {
        int n = idsPorSemilla.Count;

        if (n < MinEquipos)
        {
            throw new ArgumentException($"Una llave necesita al menos {MinEquipos} equipos.", nameof(idsPorSemilla));
        }

        // Tamaño de la llave: la menor potencia de 2 que alcanza para todos los equipos.
        int tamano = 2;

        while (tamano < n)
        {
            tamano *= 2;
        }

        var ordenSemillas = OrdenDeSemillas(tamano);
        int cantidadRondas = 0;

        for (int t = tamano; t > 1; t /= 2)
        {
            cantidadRondas++;
        }

        // ---------------- Ronda 1 ----------------
        var primera = new List<CruceGenerado>();

        for (int i = 0; i < tamano / 2; i++)
        {
            int semillaA = ordenSemillas[2 * i];
            int semillaB = ordenSemillas[2 * i + 1];

            // Una semilla mayor que n no existe: ese lugar queda vacío (bye para el otro equipo).
            // semillaA siempre es menor que semillaB, así que si alguien falta es el B.
            int? idA = semillaA <= n ? idsPorSemilla[semillaA - 1] : null;
            int? idB = semillaB <= n ? idsPorSemilla[semillaB - 1] : null;

            primera.Add(new CruceGenerado(i + 1, idA, idB, PasaDirecto: idB is null));
        }

        var rondas = new List<RondaGenerada>
        {
            new RondaGenerada(1, NombreRonda(primera.Count), primera)
        };

        // ---------------- Rondas 2..final ----------------
        var anterior = primera;

        for (int numero = 2; numero <= cantidadRondas; numero++)
        {
            var cruces = new List<CruceGenerado>();

            for (int posicion = 1; posicion <= anterior.Count / 2; posicion++)
            {
                // Los dos cruces de la ronda anterior que alimentan a este: (2q - 1) -> lugar A, (2q) -> lugar B.
                var alimentaA = anterior[2 * posicion - 2];
                var alimentaB = anterior[2 * posicion - 1];

                // Solo la primera ronda tiene byes: si el cruce que alimenta "pasa directo", su equipo ya está.
                int? idA = alimentaA.PasaDirecto ? alimentaA.IdEquipoA : null;
                int? idB = alimentaB.PasaDirecto ? alimentaB.IdEquipoA : null;

                cruces.Add(new CruceGenerado(posicion, idA, idB, PasaDirecto: false));
            }

            rondas.Add(new RondaGenerada(numero, NombreRonda(cruces.Count), cruces));
            anterior = cruces;
        }

        return rondas;
    }

    /// <summary>
    /// Orden en que se colocan las semillas en la primera ronda, de dos en dos (cada par es un cruce).
    /// Para 8: 1-8, 4-5, 2-7, 3-6. Se arma duplicando: por cada semilla s se agrega s y (tamaño + 1 - s).
    /// </summary>
    public static List<int> OrdenDeSemillas(int tamano)
    {
        var orden = new List<int> { 1 };

        for (int actual = 2; actual <= tamano; actual *= 2)
        {
            var siguiente = new List<int>();

            foreach (int semilla in orden)
            {
                siguiente.Add(semilla);
                siguiente.Add(actual + 1 - semilla);
            }

            orden = siguiente;
        }

        return orden;
    }

    /// <summary>Nombre de una ronda según cuántos cruces tiene: 1 = Final, 2 = Semifinales, 4 = Cuartos...</summary>
    public static string NombreRonda(int crucesEnLaRonda) => crucesEnLaRonda switch
    {
        1 => "Final",
        2 => "Semifinales",
        4 => "Cuartos de final",
        8 => "Octavos de final",
        16 => "Dieciseisavos de final",
        _ => $"Ronda de {crucesEnLaRonda * 2}"
    };
}
