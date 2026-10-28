using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class TramitesModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<Tramite> Tramites { get; private set; } = [];
    public int TotalTramites { get; private set; }
    public int TramitesActivos { get; private set; }
    public double TiempoPromedioEstimado { get; private set; }

    [BindProperty]
    public Tramite Formulario { get; set; } = new();

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

        try
        {
            if (Formulario.Id == 0)
            {
                var tramiteNuevo = new Tramite();
                CopiarDatosEditables(tramiteNuevo);
                await context.Tramites.AddAsync(tramiteNuevo);
                Mensaje = "El trámite se creó correctamente.";
            }
            else
            {
                var tramiteExistente = await context.Tramites
                    .SingleOrDefaultAsync(tramite => tramite.Id == Formulario.Id);

                if (tramiteExistente is null)
                {
                    return NotFound();
                }

                CopiarDatosEditables(tramiteExistente);
                Mensaje = "El trámite se actualizó correctamente.";
            }

            await context.SaveChangesAsync();
            return RedirectToPage();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                nameof(Formulario.Nombre),
                "No fue posible guardar el trámite. Verifique que el nombre no esté repetido.");
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id)
    {
        var tramite = await context.Tramites
            .SingleOrDefaultAsync(item => item.Id == id);

        if (tramite is null)
        {
            return NotFound();
        }

        tramite.Estado = tramite.Estado == "Activo" ? "Inactivo" : "Activo";
        await context.SaveChangesAsync();
        Mensaje = $"El trámite ahora está {tramite.Estado.ToLowerInvariant()}.";

        return RedirectToPage();
    }

    private async Task CargarDatosAsync()
    {
        Tramites = await context.Tramites
            .AsNoTracking()
            .OrderBy(tramite => tramite.Nombre)
            .ToListAsync();

        TotalTramites = Tramites.Count;
        TramitesActivos = Tramites.Count(tramite => tramite.Estado == "Activo");
        TiempoPromedioEstimado = Tramites.Count == 0
            ? 0
            : Tramites.Average(tramite => tramite.TiempoEstimadoMinutos);
    }

    private void ValidarFormulario()
    {
        if (string.IsNullOrWhiteSpace(Formulario.Nombre))
        {
            ModelState.AddModelError(
                "Formulario.Nombre", "El nombre del trámite es obligatorio.");
        }
        else if (Formulario.Nombre.Length > 100)
        {
            ModelState.AddModelError(
                "Formulario.Nombre", "El nombre no puede superar los 100 caracteres.");
        }

        if (Formulario.TiempoEstimadoMinutos <= 0)
        {
            ModelState.AddModelError(
                "Formulario.TiempoEstimadoMinutos",
                "El tiempo estimado debe ser mayor que cero.");
        }

        if (Formulario.NivelDemanda is not ("Alta" or "Media" or "Baja"))
        {
            ModelState.AddModelError(
                "Formulario.NivelDemanda", "Seleccione un nivel de demanda válido.");
        }

        if (Formulario.Estado is not ("Activo" or "Inactivo"))
        {
            ModelState.AddModelError(
                "Formulario.Estado", "Seleccione un estado válido.");
        }
    }

    private void CopiarDatosEditables(Tramite destino)
    {
        destino.Nombre = Formulario.Nombre.Trim();
        destino.Descripcion = string.IsNullOrWhiteSpace(Formulario.Descripcion)
            ? null
            : Formulario.Descripcion.Trim();
        destino.TiempoEstimadoMinutos = Formulario.TiempoEstimadoMinutos;
        destino.NivelDemanda = Formulario.NivelDemanda;
        destino.Estado = Formulario.Estado;
    }
}
