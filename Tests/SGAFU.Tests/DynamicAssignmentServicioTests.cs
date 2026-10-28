using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;
using Sistema_De_Gestion_Sereci_Cbba.Services;
using Xunit;

namespace SGAFU.Tests;

public sealed class DynamicAssignmentServicioTests
{
    [Fact]
    public async Task SeleccionaLaMenorEsperaAntesQueCualquierOtroCriterio()
    {
        await using var context = CrearContext();
        var tramite = CrearTramite(1, "Certificado", 10);
        var tramiteLargo = CrearTramite(2, "Rectificacion", 25);
        var puntoUno = CrearPunto(1, "Ventanilla 01", 80);
        var puntoDos = CrearPunto(2, "Ventanilla 02", 20);

        context.AddRange(
            tramite,
            tramiteLargo,
            puntoUno,
            puntoDos,
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 },
            CrearTurno(1, 2, 1, "Asignado"));
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context).CalcularAsignacionAsync(1);

        Assert.Equal(2, resultado.PuntoAtencionId);
        Assert.Equal(0, resultado.TiempoEsperaEstimadoMinutos);
    }

    [Fact]
    public async Task SumaLaDuracionDelTramiteDeCadaTurnoPendiente()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Certificado", 10),
            CrearTramite(2, "Rectificacion", 25),
            CrearPunto(1, "Ventanilla 01", 20),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            CrearTurno(1, 2, 1, "Asignado"),
            CrearTurno(2, 1, 1, "En espera"),
            CrearTurno(3, 1, 1, "En atencion"),
            CrearTurno(4, 1, 1, "Atendido"));
        await context.SaveChangesAsync();

        var estado = Assert.Single(
            await CrearServicio(context).ObtenerEstadosVentanillasAsync());

        Assert.Equal(2, estado.UsuariosEnCola);
        Assert.Equal(35, estado.TiempoEsperaEstimadoMinutos);
    }

    [Fact]
    public async Task ExcluyeVentanillasInactivasYNoCompatibles()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Certificado", 10),
            CrearPunto(1, "Activa incompatible", 1),
            CrearPunto(2, "Inactiva compatible", 1, "Inactiva"),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 });
        await context.SaveChangesAsync();

        var estados = await CrearServicio(context)
            .ObtenerEstadosVentanillasAsync(1);
        var resultado = await CrearServicio(context).CalcularAsignacionAsync(1);

        Assert.Single(estados);
        Assert.False(estados[0].EsCompatible);
        Assert.Equal("No", resultado.Compatibilidad);
        Assert.Null(resultado.PuntoAtencionId);
    }

    [Fact]
    public async Task ResuelveEmpatesPorColaYLuegoPorIdentificadorSinUsarCarga()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Certificado", 10),
            CrearPunto(2, "Ventanilla 02", 10),
            CrearPunto(1, "Ventanilla 01", 90),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context).CalcularAsignacionAsync(1);

        Assert.Equal(1, resultado.PuntoAtencionId);
    }

    [Fact]
    public async Task ClasificaEsperaMinimaIntermediaYMaxima()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Seleccionado", 10),
            CrearTramite(2, "Largo", 20),
            CrearTramite(3, "Medio", 10),
            CrearPunto(1, "Ventanilla 01", 90),
            CrearPunto(2, "Ventanilla 02", 10),
            CrearPunto(3, "Ventanilla 03", 50),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 3 },
            CrearTurno(1, 2, 1, "Asignado"),
            CrearTurno(2, 3, 3, "Asignado"));
        await context.SaveChangesAsync();

        var estados = await CrearServicio(context)
            .ObtenerEstadosVentanillasAsync(1);

        Assert.Equal("Verde", estados.Single(item => item.Id == 2).NivelCongestion);
        Assert.Equal("Amarillo", estados.Single(item => item.Id == 3).NivelCongestion);
        Assert.Equal("Rojo", estados.Single(item => item.Id == 1).NivelCongestion);
    }

    [Fact]
    public async Task LosEmpatesEnElMinimoSonVerdesYElMaximoEsRojo()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Seleccionado", 10),
            CrearTramite(2, "Largo", 20),
            CrearPunto(1, "Ventanilla 01", 90),
            CrearPunto(2, "Ventanilla 02", 10),
            CrearPunto(3, "Ventanilla 03", 50),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 3 },
            CrearTurno(1, 2, 1, "Asignado"));
        await context.SaveChangesAsync();

        var estados = await CrearServicio(context)
            .ObtenerEstadosVentanillasAsync(1);

        Assert.Equal("Rojo", estados.Single(item => item.Id == 1).NivelCongestion);
        Assert.Equal("Verde", estados.Single(item => item.Id == 2).NivelCongestion);
        Assert.Equal("Verde", estados.Single(item => item.Id == 3).NivelCongestion);
    }

    [Fact]
    public async Task SiTodasLasEsperasSonIgualesTodasSonVerdes()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Seleccionado", 10),
            CrearPunto(1, "Ventanilla 01", 90),
            CrearPunto(2, "Ventanilla 02", 10),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 });
        await context.SaveChangesAsync();

        var estados = await CrearServicio(context)
            .ObtenerEstadosVentanillasAsync(1);

        Assert.All(estados, item => Assert.Equal("Verde", item.NivelCongestion));
    }

    [Fact]
    public async Task RechazaTramiteInexistenteOInactivo()
    {
        await using var context = CrearContext();
        context.Add(CrearTramite(1, "Inactivo", 10, "Inactivo"));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CrearServicio(context).CalcularAsignacionAsync(99));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CrearServicio(context).CalcularAsignacionAsync(1));
    }

    [Fact]
    public async Task FinalizandoConservaLaColaPeroNoEsCandidata()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Seleccionado", 10),
            CrearPunto(1, "Finalizando", 20, "Finalizando turno"),
            CrearPunto(2, "Activa", 80),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 1 },
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 },
            CrearTurno(1, 1, 1, "Asignado"));
        await context.SaveChangesAsync();

        var servicio = CrearServicio(context);
        var estados = await servicio.ObtenerEstadosVentanillasAsync(1);
        var resultado = await servicio.CalcularAsignacionAsync(1);
        var finalizando = estados.Single(item => item.Id == 1);

        Assert.Equal(1, finalizando.UsuariosEnCola);
        Assert.False(finalizando.PuedeRecibirAsignaciones);
        Assert.Equal(2, resultado.PuntoAtencionId);
    }

    [Fact]
    public async Task ReasignaPorLlegadaYRespetaCompatibilidad()
    {
        await using var context = CrearContext();
        var llegadaBase = new DateTime(2026, 10, 27, 9, 0, 0);
        context.AddRange(
            CrearTramite(1, "Registro", 10),
            CrearTramite(2, "Certificado", 20),
            CrearPunto(1, "Origen", 1, "Inactiva"),
            CrearPunto(2, "Destino registro", 1),
            CrearPunto(3, "Destino certificado", 1),
            new TramitePuntoAtencion { TramiteId = 1, PuntoAtencionId = 2 },
            new TramitePuntoAtencion { TramiteId = 2, PuntoAtencionId = 3 },
            new TurnoSimulado
            {
                Id = 1,
                CodigoTurno = "A-101",
                TramiteId = 1,
                PuntoAtencionId = 1,
                FechaHoraSolicitud = llegadaBase.AddMinutes(1),
                Estado = "Asignado"
            },
            new TurnoSimulado
            {
                Id = 2,
                CodigoTurno = "A-102",
                TramiteId = 2,
                PuntoAtencionId = 1,
                FechaHoraSolicitud = llegadaBase.AddMinutes(3),
                Estado = "Asignado"
            },
            new TurnoSimulado
            {
                Id = 3,
                CodigoTurno = "A-103",
                TramiteId = 1,
                PuntoAtencionId = 1,
                FechaHoraSolicitud = llegadaBase.AddMinutes(5),
                Estado = "En atencion"
            });
        await context.SaveChangesAsync();
        var resultado = await CrearServicio(context)
            .ReasignarTurnosPendientesAsync(1, "Cierre");
        await context.SaveChangesAsync();

        var turnos = await context.TurnosSimulados
            .OrderBy(turno => turno.Id)
            .ToListAsync();
        var historial = await context.TurnosAsignacionesHistorial
            .OrderBy(item => item.TurnoSimuladoId)
            .ToListAsync();

        Assert.Equal(2, resultado.Reasignados);
        Assert.True(resultado.TieneTurnosEnAtencion);
        Assert.Equal(2, turnos[0].PuntoAtencionId);
        Assert.Equal(3, turnos[1].PuntoAtencionId);
        Assert.Equal(1, turnos[2].PuntoAtencionId);
        Assert.Equal(2, historial.Count);
        Assert.All(historial, item => Assert.Equal("Reasignacion", item.TipoMovimiento));
    }

    [Fact]
    public async Task RegistraBloqueoCuandoNoExisteDestinoCompatible()
    {
        await using var context = CrearContext();
        context.AddRange(
            CrearTramite(1, "Sin destino", 10),
            CrearPunto(1, "Origen", 1, "Inactiva"),
            new TurnoSimulado
            {
                Id = 1,
                CodigoTurno = "B-101",
                TramiteId = 1,
                PuntoAtencionId = 1,
                FechaHoraSolicitud = DateTime.UtcNow,
                Estado = "Asignado"
            });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context)
            .ReasignarTurnosPendientesAsync(1, "Cierre");

        Assert.Equal(["B-101"], resultado.Bloqueados);
        Assert.Equal(0, resultado.Reasignados);
        Assert.Equal(1, (await context.TurnosSimulados.SingleAsync()).PuntoAtencionId);
    }

    private static DynamicAssignmentServicio CrearServicio(ApplicationDbContext context) =>
        new(context);

    private static ApplicationDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Tramite CrearTramite(
        int id,
        string nombre,
        int tiempo,
        string estado = "Activo") =>
        new()
        {
            Id = id,
            Nombre = nombre,
            TiempoEstimadoMinutos = tiempo,
            NivelDemanda = "Media",
            Estado = estado
        };

    private static PuntoAtencion CrearPunto(
        int id,
        string nombre,
        int carga,
        string estado = "Activa") =>
        new()
        {
            Id = id,
            Nombre = nombre,
            CargaActualPorcentaje = carga,
            Estado = estado
        };

    private static TurnoSimulado CrearTurno(
        int id,
        int tramiteId,
        int puntoId,
        string estado) =>
        new()
        {
            Id = id,
            CodigoTurno = $"TEST-{id}",
            TramiteId = tramiteId,
            PuntoAtencionId = puntoId,
            FechaHoraSolicitud = DateTime.UtcNow.AddMinutes(id),
            Estado = estado,
            TiempoEsperaEstimadoMinutos = 0
        };
}
