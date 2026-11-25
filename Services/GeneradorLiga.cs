namespace FairPlay.Services;

/// <summary>Un partido de una jornada: el visitante es null cuando el equipo local DESCANSA.</summary>
public record EmparejamientoLiga(int IdLocal, int? IdVisitante);

/// <summary>Una jornada de la liga con sus partidos.</summary>
public record JornadaLiga(int Numero, IReadOnlyList<EmparejamientoLiga> Partidos);

/// <summary>
/// Genera el calendario de una liga "todos contra todos" con el MÉTODO DEL CÍRCULO.
/// Es una clase pura (no usa la base de datos): recibe los ids de los equipos y devuelve las jornadas.
///
/// Cómo funciona el método del círculo:
///   - Si el número de equipos es impar se agrega un "equipo fantasma" (null): quien le toque descansa.
///   - El equipo de la posición 0 queda fijo y los demás giran una posición en cada jornada.
///   - En cada jornada se empareja el primero con el último, el segundo con el penúltimo, etc.
///   - Con N equipos (par) hay N-1 jornadas y todos juegan contra todos exactamente una vez.
///
/// Local y visitante: el equipo fijo alterna una jornada local y otra visitante; en el resto de las
/// mesas la localía depende de la mesa (i). Con esta regla la diferencia entre partidos de local y
/// de visitante de un mismo equipo nunca pasa de 2 (se comprobó hasta 20 equipos).
///
/// Ida y vuelta: después de las jornadas de ida se repiten las mismas jornadas con la localía invertida.
/// </summary>
public static class GeneradorLiga
{
    public static List<JornadaLiga> Generar(IReadOnlyList<int> idsEnOrden, bool idaYVuelta)
    {
        // int? permite representar al "equipo fantasma" (null) de los torneos con número impar de equipos.
        var lista = idsEnOrden.Select(id => (int?)id).ToList();

        if (lista.Count % 2 == 1)
        {
            lista.Add(null);
        }

        int n = lista.Count;
        int mesas = n / 2;
        var ida = new List<List<EmparejamientoLiga>>();

        for (int ronda = 0; ronda < n - 1; ronda++)
        {
            var partidos = new List<EmparejamientoLiga>();

            for (int i = 0; i < mesas; i++)
            {
                int? a = lista[i];
                int? b = lista[n - 1 - i];

                // Si uno de los dos es el fantasma, el otro descansa.
                if (a is null || b is null)
                {
                    int quienDescansa = a.HasValue ? a.Value : b!.Value;
                    partidos.Add(new EmparejamientoLiga(quienDescansa, null));
                    continue;
                }

                // Mesa 0 (equipo fijo): alterna por jornada. Resto de mesas: según la posición de la mesa.
                bool invertir = i == 0 ? ronda % 2 == 1 : i % 2 == 1;

                partidos.Add(invertir
                    ? new EmparejamientoLiga(b.Value, a.Value)
                    : new EmparejamientoLiga(a.Value, b.Value));
            }

            ida.Add(partidos);

            // Giro: el último pasa a la posición 1 (la posición 0 queda fija).
            var ultimo = lista[n - 1];
            lista.RemoveAt(n - 1);
            lista.Insert(1, ultimo);
        }

        var jornadas = new List<JornadaLiga>();
        int numero = 1;

        foreach (var partidos in ida)
        {
            jornadas.Add(new JornadaLiga(numero++, partidos));
        }

        if (idaYVuelta)
        {
            foreach (var partidos in ida)
            {
                // Vuelta: se invierte la localía; el descanso se mantiene.
                var vuelta = partidos
                    .Select(p => p.IdVisitante is null
                        ? p
                        : new EmparejamientoLiga(p.IdVisitante.Value, p.IdLocal))
                    .ToList();

                jornadas.Add(new JornadaLiga(numero++, vuelta));
            }
        }

        return jornadas;
    }
}
