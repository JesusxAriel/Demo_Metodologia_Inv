using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;
using Sistema_De_Gestion_Sereci_Cbba.Services;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class ResultadosModel(ApplicationDbContext context) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "escenario")]
    public int? EscenarioId { get; set; }

    public IReadOnlyList<EscenarioSimulacion> Escenarios { get; private set; } = [];
    public EscenarioSimulacion? Escenario { get; private set; }
    public ComparativaSimulacion? Comparativa { get; private set; }

    /// <summary>Minutos de atención requeridos entre la capacidad de las ventanillas.</summary>
    public double CargaSobreCapacidad { get; private set; }

    public async Task OnGetAsync()
    {
        Escenarios = await context.EscenariosSimulacion
            .AsNoTracking()
            .OrderByDescending(item => item.FechaCreacion)
            .ThenByDescending(item => item.Id)
            .ToListAsync();

        Escenario = EscenarioId.HasValue
            ? Escenarios.FirstOrDefault(item => item.Id == EscenarioId.Value)
            : Escenarios.FirstOrDefault();

        if (Escenario is null)
        {
            return;
        }

        var tramites = await context.Tramites
            .AsNoTracking()
            .Where(item => item.Estado == "Activo")
            .OrderBy(item => item.Id)
            .Select(item => new EntradaTramite(
                item.Id, item.TiempoEstimadoMinutos, item.NivelDemanda))
            .ToListAsync();

        var puntos = await context.PuntosAtencion
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(Escenario.PuntosActivos)
            .Select(item => new { item.Id, item.Nombre })
            .ToListAsync();

        var relaciones = await context.TramitesPuntosAtencion
            .AsNoTracking()
            .Select(item => new { item.PuntoAtencionId, item.TramiteId })
            .ToListAsync();

        var ventanillas = puntos
            .Select(punto => new EntradaVentanilla(
                punto.Id,
                punto.Nombre,
                relaciones
                    .Where(item => item.PuntoAtencionId == punto.Id)
                    .Select(item => item.TramiteId)
                    .ToHashSet()))
            .ToList();

        var registros = await context.RegistrosDemanda
            .AsNoTracking()
            .Select(item => new { item.FranjaHoraria, item.TramiteId, item.CantidadUsuarios })
            .ToListAsync();

        var demandaPorHora = registros
            .Where(item => item.FranjaHoraria.Length >= 2 &&
                           int.TryParse(item.FranjaHoraria[..2], out _))
            .GroupBy(item => int.Parse(item.FranjaHoraria[..2]))
            .ToDictionary(grupo => grupo.Key, grupo => (double)grupo.Sum(item => item.CantidadUsuarios));
        var demandaPorTramite = registros
            .GroupBy(item => item.TramiteId)
            .ToDictionary(grupo => grupo.Key, grupo => (double)grupo.Sum(item => item.CantidadUsuarios));

        Comparativa = MotorSimulacion.Ejecutar(
            Escenario, tramites, ventanillas, demandaPorHora, demandaPorTramite);

        var capacidad = Math.Max(1, (Escenario.HoraFin - Escenario.HoraInicio).TotalMinutes)
                        * Math.Max(1, ventanillas.Count);
        CargaSobreCapacidad = Math.Round(
            Comparativa.Dinamica.Ventanillas.Sum(item => item.MinutosOcupada) * 100.0 / capacidad);
    }

    public static string Metodo(string valor) =>
        valor == MotorSimulacion.Dinamica ? "Asignación dinámica" : "Distribución convencional";
}
