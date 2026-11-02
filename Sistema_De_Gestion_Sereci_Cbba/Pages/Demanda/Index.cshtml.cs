using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class DemandaModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<RegistroDemanda> Registros { get; private set; } = [];
    public IReadOnlyList<Tramite> Tramites { get; private set; } = [];

    public int DemandaTotal { get; private set; }
    public double PromedioDemanda { get; private set; }
    public string FranjaMayorDemanda { get; private set; } = "-";
    public List<string> FranjasGrafico { get; private set; } = [];
    public List<int> UsuariosGrafico { get; private set; } = [];
    // Filtros
    [BindProperty(SupportsGet = true)]
    public DateOnly? FechaFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? TramiteFiltroId { get; set; }

    // Formulario de nuevo registro
    [BindProperty]
    public RegistroDemanda Formulario { get; set; } = new()
    {
        Fecha = DateOnly.FromDateTime(DateTime.Today)
    };

    [TempData]
    public string? Mensaje { get; set; }

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        ValidarFormulario();

        if (!ModelState.IsValid)
        {
            await CargarDatosAsync();
            return Page();
        }

        var tramiteExiste = await context.Tramites
            .AnyAsync(t =>
                t.Id == Formulario.TramiteId &&
                t.Estado == "Activo");

        if (!tramiteExiste)
        {
            ModelState.AddModelError(
                "Formulario.TramiteId",
                "Seleccione un trámite activo válido.");

            await CargarDatosAsync();
            return Page();
        }

        var registroDuplicado = await context.RegistrosDemanda
            .AnyAsync(r =>
                r.Fecha == Formulario.Fecha &&
                r.FranjaHoraria == Formulario.FranjaHoraria &&
                r.TramiteId == Formulario.TramiteId);

        if (registroDuplicado)
        {
            ModelState.AddModelError(
                string.Empty,
                "Ya existe un registro para ese trámite, fecha y franja horaria.");

            await CargarDatosAsync();
            return Page();
        }

        var nuevoRegistro = new RegistroDemanda
        {
            Fecha = Formulario.Fecha,
            FranjaHoraria = Formulario.FranjaHoraria.Trim(),
            TramiteId = Formulario.TramiteId,
            CantidadUsuarios = Formulario.CantidadUsuarios
        };

        context.RegistrosDemanda.Add(nuevoRegistro);
        await context.SaveChangesAsync();

        Mensaje = "El registro de demanda se creó correctamente.";

        return RedirectToPage();
    }

    private async Task CargarDatosAsync()
    {
        Tramites = await context.Tramites
            .AsNoTracking()
            .Where(t => t.Estado == "Activo")
            .OrderBy(t => t.Nombre)
            .ToListAsync();

        var consulta = context.RegistrosDemanda
            .AsNoTracking()
            .Include(r => r.Tramite)
            .AsQueryable();

        if (FechaFiltro.HasValue)
        {
            consulta = consulta.Where(r =>
                r.Fecha == FechaFiltro.Value);
        }

        if (TramiteFiltroId.HasValue)
        {
            consulta = consulta.Where(r =>
                r.TramiteId == TramiteFiltroId.Value);
        }

        Registros = await consulta
            .OrderByDescending(r => r.Fecha)
            .ThenBy(r => r.FranjaHoraria)
            .ThenBy(r => r.Tramite.Nombre)
            .ToListAsync();

        CalcularIndicadores();
    }

    private void CalcularIndicadores()
    {
        if (Registros.Count == 0)
        {
            DemandaTotal = 0;
            PromedioDemanda = 0;
            FranjaMayorDemanda = "-";

            FranjasGrafico = [];
            UsuariosGrafico = [];

            return;
        }

        DemandaTotal = Registros.Sum(r =>
            r.CantidadUsuarios);

        PromedioDemanda = Registros.Average(r =>
            r.CantidadUsuarios);
        var datosGrafico = Registros
        .GroupBy(r => r.FranjaHoraria)
        .Select(grupo => new
        {
        Franja = grupo.Key,
        Total = grupo.Sum(r => r.CantidadUsuarios)
        })
        .OrderBy(x => x.Franja)
        .ToList();

        FranjasGrafico = datosGrafico.Select(x => x.Franja).ToList();

        UsuariosGrafico = datosGrafico
            .Select(x => x.Total)
            .ToList();
        var mayorDemanda = datosGrafico
        .OrderByDescending(x => x.Total)
        .First();
        FranjaMayorDemanda = mayorDemanda.Franja;
    }

    private void ValidarFormulario()
    {
        if (string.IsNullOrWhiteSpace(Formulario.FranjaHoraria))
        {
            ModelState.AddModelError(
                "Formulario.FranjaHoraria",
                "La franja horaria es obligatoria.");
        }

        if (Formulario.TramiteId <= 0)
        {
            ModelState.AddModelError(
                "Formulario.TramiteId",
                "Seleccione un trámite.");
        }

        if (Formulario.CantidadUsuarios < 0)
        {
            ModelState.AddModelError(
                "Formulario.CantidadUsuarios",
                "La cantidad de usuarios no puede ser negativa.");
        }
    }
}