using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Services;

/// <summary>
/// Motor de asignación dinámica y cálculo de indicadores operativos.
/// </summary>
public sealed class DynamicAssignmentServicio(ApplicationDbContext context)
    : IDynamicAssignmentServicio
{
    private const string EstadoActivo = "Activo";
    private const string EstadoActiva = "Activa";
    private const string EstadoFinalizando = "Finalizando turno";
    private const string EstadoEnDescanso = "En descanso";
    private const string EstadoEnEspera = "En espera";
    private const string EstadoAsignado = "Asignado";

    /// <summary>
    /// Obtiene el estado calculado de cada ventanilla operativa.
    /// La cola y la espera se derivan de TurnosSimulados pendientes.
    /// Las ventanillas finalizando conservan sus indicadores, pero no pueden
    /// recibir nuevas asignaciones.
    /// Los turnos en atención no se suman porque el esquema no registra
    /// duración restante ni hora de inicio suficiente para calcularla.
    /// </summary>
    public async Task<IReadOnlyList<EstadoVentanillaDto>> ObtenerEstadosVentanillasAsync(
        int? tramiteId = null)
    {
        var ventanillas = await context.PuntosAtencion
            .AsNoTracking()
            .Where(punto =>
                punto.Estado == EstadoActiva ||
                punto.Estado == EstadoFinalizando ||
                punto.Estado == EstadoEnDescanso)
            .OrderBy(punto => punto.Id)
            .Select(punto => new
            {
                punto.Id,
                punto.Nombre,
                punto.Estado
            })
            .ToListAsync();

        var compatibles = tramiteId.HasValue
            ? await context.TramitesPuntosAtencion
                .AsNoTracking()
                .Where(relation => relation.TramiteId == tramiteId.Value)
                .Select(relation => relation.PuntoAtencionId)
                .ToListAsync()
            : [];

        var turnosPendientes = await context.TurnosSimulados
            .AsNoTracking()
            .Where(turno =>
                turno.PuntoAtencionId.HasValue &&
                (turno.PuntoAtencion!.Estado == EstadoActiva ||
                 turno.PuntoAtencion.Estado == EstadoFinalizando ||
                 turno.PuntoAtencion.Estado == EstadoEnDescanso) &&
                (turno.Estado == EstadoEnEspera || turno.Estado == EstadoAsignado))
            .OrderBy(turno => turno.FechaHoraSolicitud)
            .Select(turno => new
            {
                PuntoAtencionId = turno.PuntoAtencionId!.Value,
                TiempoServicioMinutos = turno.Tramite.TiempoEstimadoMinutos
            })
            .ToListAsync();

        var estadoPorPunto = turnosPendientes
            .GroupBy(turno => turno.PuntoAtencionId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => new
                {
                    UsuariosEnCola = grupo.Count(),
                    TiempoEspera = grupo.Sum(turno => turno.TiempoServicioMinutos)
                });

        var comparables = ventanillas
            .Where(punto =>
                punto.Estado == EstadoActiva &&
                (!tramiteId.HasValue || compatibles.Contains(punto.Id)))
            .ToList();
        var esperasComparables = comparables
            .Select(punto => estadoPorPunto.TryGetValue(punto.Id, out var estado)
                ? estado.TiempoEspera
                : 0)
            .ToList();
        var esperaMinima = esperasComparables.Count == 0 ? 0 : esperasComparables.Min();
        var esperaMaxima = esperasComparables.Count == 0 ? 0 : esperasComparables.Max();

        return ventanillas
            .Select(punto =>
            {
                estadoPorPunto.TryGetValue(punto.Id, out var estado);
                var espera = estado?.TiempoEspera ?? 0;
                var esCompatible = !tramiteId.HasValue || compatibles.Contains(punto.Id);
                var puedeRecibir = punto.Estado == EstadoActiva && esCompatible;
                return new EstadoVentanillaDto(
                    punto.Id,
                    punto.Nombre,
                    estado?.UsuariosEnCola ?? 0,
                    espera,
                    punto.Estado,
                    esCompatible,
                    ClasificarCongestion(
                        puedeRecibir,
                        espera,
                        esperaMinima,
                        esperaMaxima),
                    puedeRecibir);
            })
            .ToList();
    }

    public async Task<ResultadoAsignacionDto> CalcularAsignacionAsync(int tramiteId)
    {
        var tramite = await context.Tramites
            .AsNoTracking()
            .Where(item => item.Id == tramiteId && item.Estado == EstadoActivo)
            .Select(item => new { item.Id, item.TiempoEstimadoMinutos })
            .SingleOrDefaultAsync();

        if (tramite is null)
        {
            throw new KeyNotFoundException(
                $"No se encontró un trámite activo con identificador {tramiteId}.");
        }

        var candidatos = (await ObtenerEstadosVentanillasAsync(tramiteId))
            .Where(ventanilla =>
                ventanilla.EsCompatible &&
                ventanilla.PuedeRecibirAsignaciones)
            .OrderBy(ventanilla => ventanilla.TiempoEsperaEstimadoMinutos)
            .ThenBy(ventanilla => ventanilla.UsuariosEnCola)
            .ThenBy(ventanilla => ventanilla.Id)
            .ToList();

        var recomendada = candidatos.FirstOrDefault();
        if (recomendada is null)
        {
            return new ResultadoAsignacionDto(
                tramite.Id,
                null,
                null,
                0,
                0,
                "No",
                "No existe una ventanilla activa compatible con el trámite.",
                "Informativo");
        }

        return new ResultadoAsignacionDto(
            tramite.Id,
            recomendada.Id,
            recomendada.Nombre,
            recomendada.UsuariosEnCola,
            recomendada.TiempoEsperaEstimadoMinutos,
            "Sí",
            "Seleccionada por menor espera proyectada; luego se usa la menor cola y el menor identificador.",
            recomendada.NivelCongestion);
    }

    public async Task<ReasignacionResultadoDto> ReasignarTurnosPendientesAsync(
        int puntoAtencionOrigenId,
        string motivo)
    {
        var pendientes = (await context.TurnosSimulados
            .Include(turno => turno.Tramite)
            .Where(turno =>
                turno.PuntoAtencionId == puntoAtencionOrigenId &&
                (turno.Estado == EstadoEnEspera || turno.Estado == EstadoAsignado))
            .OrderBy(turno => turno.FechaHoraSolicitud)
            .ThenBy(turno => turno.Id)
            .ToListAsync());

        var enAtencion = await context.TurnosSimulados
            .AsNoTracking()
            .AnyAsync(turno =>
                turno.PuntoAtencionId == puntoAtencionOrigenId &&
                turno.Estado == "En atencion");

        var destinos = await context.PuntosAtencion
            .AsNoTracking()
            .Where(punto =>
                punto.Estado == EstadoActiva &&
                punto.Id != puntoAtencionOrigenId)
            .OrderBy(punto => punto.Id)
            .Select(punto => new
            {
                punto.Id,
                punto.Nombre
            })
            .ToListAsync();
        var destinoIds = destinos.Select(destino => destino.Id).ToList();

        var compatibilidades = await context.TramitesPuntosAtencion
            .AsNoTracking()
            .Select(relation => new
            {
                relation.TramiteId,
                relation.PuntoAtencionId
            })
            .ToListAsync();

        var turnosDestino = await context.TurnosSimulados
            .AsNoTracking()
            .Where(turno =>
                turno.PuntoAtencionId.HasValue &&
                (turno.Estado == EstadoEnEspera || turno.Estado == EstadoAsignado))
            .Select(turno => new
            {
                PuntoAtencionId = turno.PuntoAtencionId!.Value,
                Duracion = turno.Tramite.TiempoEstimadoMinutos
            })
            .ToListAsync();
        turnosDestino = turnosDestino
            .Where(turno => destinoIds.Contains(turno.PuntoAtencionId))
            .ToList();

        var cargaPorDestino = destinos.ToDictionary(
            destino => destino.Id,
            destino =>
            {
                var cola = turnosDestino
                    .Where(turno => turno.PuntoAtencionId == destino.Id)
                    .ToList();
                return new CargaDestino(cola.Count, cola.Sum(item => item.Duracion));
            });
        var bloqueados = new List<string>();
        var ahora = DateTime.Now;

        foreach (var turno in pendientes)
        {
            var candidatos = destinos
                .Where(destino => compatibilidades.Any(relation =>
                    relation.TramiteId == turno.TramiteId &&
                    relation.PuntoAtencionId == destino.Id))
                .OrderBy(destino => cargaPorDestino[destino.Id].TiempoEspera)
                .ThenBy(destino => cargaPorDestino[destino.Id].UsuariosEnCola)
                .ThenBy(destino => destino.Id)
                .ToList();

            var destinoSeleccionado = candidatos.FirstOrDefault();
            if (destinoSeleccionado is null)
            {
                bloqueados.Add(turno.CodigoTurno);
                continue;
            }

            var carga = cargaPorDestino[destinoSeleccionado.Id];
            turno.PuntoAtencionId = destinoSeleccionado.Id;
            turno.TiempoEsperaEstimadoMinutos = carga.TiempoEspera;
            context.TurnosAsignacionesHistorial.Add(new TurnoAsignacionHistorial
            {
                TurnoSimuladoId = turno.Id,
                PuntoAtencionOrigenId = puntoAtencionOrigenId,
                PuntoAtencionDestinoId = destinoSeleccionado.Id,
                FechaHoraMovimiento = ahora,
                TipoMovimiento = "Reasignacion",
                Motivo = motivo
            });
            cargaPorDestino[destinoSeleccionado.Id] = new CargaDestino(
                carga.UsuariosEnCola + 1,
                carga.TiempoEspera + turno.Tramite.TiempoEstimadoMinutos);
        }

        return new ReasignacionResultadoDto(
            pendientes.Count(turno => turno.PuntoAtencionId != puntoAtencionOrigenId),
            bloqueados,
            enAtencion);
    }

    private sealed record CargaDestino(int UsuariosEnCola, int TiempoEspera);

    private static string ClasificarCongestion(
        bool comparable,
        int espera,
        int esperaMinima,
        int esperaMaxima)
    {
        if (!comparable)
        {
            return "Informativo";
        }

        if (esperaMinima == esperaMaxima || espera == esperaMinima)
        {
            return "Verde";
        }

        return espera == esperaMaxima ? "Rojo" : "Amarillo";
    }
}
