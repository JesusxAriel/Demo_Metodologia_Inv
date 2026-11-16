using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Services;

public sealed record EntradaTramite(int Id, int Minutos, string NivelDemanda);

public sealed record EntradaVentanilla(
    int Id,
    string Nombre,
    IReadOnlySet<int> TramitesCompatibles);

/// Simulación determinista de una jornada.
public static class MotorSimulacion
{
    public const string Convencional = "Convencional";
    public const string Dinamica = "Dinamica";

    private static readonly int[] LimitesEspera = [5, 15, 30];

    private readonly record struct Llegada(double Minuto, int TramiteId, double Servicio, int Hora);

    public static ComparativaSimulacion Ejecutar(
        EscenarioSimulacion escenario,
        IReadOnlyList<EntradaTramite> tramites,
        IReadOnlyList<EntradaVentanilla> ventanillas,
        IReadOnlyDictionary<int, double> demandaPorHora,
        IReadOnlyDictionary<int, double> demandaPorTramite)
    {
        var duracion = Math.Max(1, (int)(escenario.HoraFin - escenario.HoraInicio).TotalMinutes);
        var llegadas = GenerarLlegadas(
            escenario, duracion, tramites, demandaPorHora, demandaPorTramite);

        return new ComparativaSimulacion(
            Simular(Convencional, llegadas, ventanillas, duracion),
            Simular(Dinamica, llegadas, ventanillas, duracion));
    }

    private static List<Llegada> GenerarLlegadas(
        EscenarioSimulacion escenario,
        int duracion,
        IReadOnlyList<EntradaTramite> tramites,
        IReadOnlyDictionary<int, double> demandaPorHora,
        IReadOnlyDictionary<int, double> demandaPorTramite)
    {
        var llegadas = new List<Llegada>();
        if (tramites.Count == 0 || escenario.CantidadUsuariosEstimados <= 0)
        {
            return llegadas;
        }

        var rng = new Random(escenario.Id * 7919 + 17);

        // Peso de cada minuto según la demanda histórica de su hora.
        var pesoBase = demandaPorHora.Count == 0 ? 1 : demandaPorHora.Values.Average();
        var acumuladoMinutos = new double[duracion];
        var total = 0.0;
        for (var minuto = 0; minuto < duracion; minuto++)
        {
            var hora = escenario.HoraInicio.AddMinutes(minuto).Hour;
            total += demandaPorHora.TryGetValue(hora, out var peso) && peso > 0
                ? peso
                : pesoBase * 0.5;
            acumuladoMinutos[minuto] = total;
        }

        // Peso de cada trámite: demanda registrada o, si falta, su nivel de demanda.
        var acumuladoTramites = new double[tramites.Count];
        var totalTramites = 0.0;
        for (var i = 0; i < tramites.Count; i++)
        {
            totalTramites += demandaPorTramite.TryGetValue(tramites[i].Id, out var peso) && peso > 0
                ? peso
                : tramites[i].NivelDemanda switch { "Alta" => 3, "Media" => 2, _ => 1 };
            acumuladoTramites[i] = totalTramites;
        }

        for (var usuario = 0; usuario < escenario.CantidadUsuariosEstimados; usuario++)
        {
            var minuto = BuscarIndice(acumuladoMinutos, rng.NextDouble() * total);
            var llegada = minuto + rng.NextDouble();
            var tramite = tramites[BuscarIndice(acumuladoTramites, rng.NextDouble() * totalTramites)];
            var servicio = tramite.Minutos * (0.8 + 0.4 * rng.NextDouble());
            var hora = escenario.HoraInicio.AddMinutes(minuto).Hour;
            llegadas.Add(new Llegada(llegada, tramite.Id, servicio, hora));
        }

        return llegadas.OrderBy(item => item.Minuto).ToList();
    }

    private static int BuscarIndice(double[] acumulado, double valor)
    {
        var indice = Array.BinarySearch(acumulado, valor);
        if (indice < 0)
        {
            indice = ~indice;
        }

        return Math.Min(indice, acumulado.Length - 1);
    }

    private static ResultadoMetodo Simular(
        string metodo,
        IReadOnlyList<Llegada> llegadas,
        IReadOnlyList<EntradaVentanilla> ventanillas,
        int duracion)
    {
        var cantidad = ventanillas.Count;
        var libreEn = new double[cantidad];
        var ocupado = new double[cantidad];
        var atenciones = new int[cantidad];
        var esperas = new List<(double Espera, int Hora)>();
        var sinVentanilla = 0;
        var puntero = 0;

        foreach (var llegada in llegadas)
        {
            var elegida = -1;
            if (metodo == Dinamica)
            {
                // Menor espera proyectada entre las ventanillas compatibles.
                var mejor = double.MaxValue;
                for (var i = 0; i < cantidad; i++)
                {
                    if (!ventanillas[i].TramitesCompatibles.Contains(llegada.TramiteId))
                    {
                        continue;
                    }

                    var proyectada = Math.Max(0, libreEn[i] - llegada.Minuto);
                    if (proyectada < mejor)
                    {
                        mejor = proyectada;
                        elegida = i;
                    }
                }
            }
            else
            {
                // Rotación secuencial por orden de llegada, sin mirar la carga.
                for (var paso = 0; paso < cantidad; paso++)
                {
                    var i = (puntero + paso) % cantidad;
                    if (ventanillas[i].TramitesCompatibles.Contains(llegada.TramiteId))
                    {
                        elegida = i;
                        puntero = (i + 1) % cantidad;
                        break;
                    }
                }
            }

            if (elegida < 0)
            {
                sinVentanilla++;
                continue;
            }

            var inicio = Math.Max(llegada.Minuto, libreEn[elegida]);
            esperas.Add((inicio - llegada.Minuto, llegada.Hora));
            libreEn[elegida] = inicio + llegada.Servicio;
            ocupado[elegida] += llegada.Servicio;
            atenciones[elegida]++;
        }

        var ocupaciones = ocupado.Select(minutos => minutos * 100.0 / duracion).ToList();
        var detalleVentanillas = ventanillas
            .Select((ventanilla, i) => new MetricaVentanilla(
                ventanilla.Nombre,
                atenciones[i],
                (int)Math.Round(ocupado[i]),
                (int)Math.Min(100, Math.Round(ocupaciones[i]))))
            .ToList();

        var dispersion = 0.0;
        if (ocupaciones.Count > 0)
        {
            var media = ocupaciones.Average();
            dispersion = Math.Sqrt(ocupaciones.Average(valor => Math.Pow(valor - media, 2)));
        }

        var ordenadas = esperas.Select(item => item.Espera).OrderBy(valor => valor).ToList();
        var distribucion = new int[LimitesEspera.Length + 1];
        foreach (var espera in ordenadas)
        {
            var tramo = Array.FindIndex(LimitesEspera, limite => espera <= limite);
            distribucion[tramo < 0 ? LimitesEspera.Length : tramo]++;
        }

        var porHora = esperas
            .GroupBy(item => item.Hora)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new MetricaHora(
                $"{grupo.Key:00}:00",
                Math.Round(grupo.Average(item => item.Espera), 1)))
            .ToList();

        return new ResultadoMetodo(
            metodo,
            ordenadas.Count,
            sinVentanilla,
            ordenadas.Count == 0 ? 0 : Math.Round(ordenadas.Average(), 1),
            ordenadas.Count == 0 ? 0 : (int)Math.Ceiling(ordenadas[^1]),
            ordenadas.Count == 0
                ? 0
                : (int)Math.Ceiling(ordenadas[Math.Max(0, (int)Math.Ceiling(ordenadas.Count * 0.9) - 1)]),
            cantidad == 0 ? 0 : Math.Max(0, (int)Math.Ceiling(libreEn.Max() - duracion)),
            Math.Round(dispersion, 1),
            distribucion,
            detalleVentanillas,
            porHora);
    }
}
