using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Tramite> Tramites => Set<Tramite>();
    public DbSet<PuntoAtencion> PuntosAtencion => Set<PuntoAtencion>();
    public DbSet<TramitePuntoAtencion> TramitesPuntosAtencion => Set<TramitePuntoAtencion>();
    public DbSet<RegistroDemanda> RegistrosDemanda => Set<RegistroDemanda>();
    public DbSet<EscenarioSimulacion> EscenariosSimulacion => Set<EscenarioSimulacion>();
    public DbSet<TurnoSimulado> TurnosSimulados => Set<TurnoSimulado>();
    public DbSet<TurnoAsignacionHistorial> TurnosAsignacionesHistorial => Set<TurnoAsignacionHistorial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tramite>(entity =>
        {
            entity.ToTable("Tramites", table =>
            {
                table.HasCheckConstraint("chk_tramites_tiempo", "TiempoEstimadoMinutos > 0");
                table.HasCheckConstraint("chk_tramites_demanda",
                    "NivelDemanda IN ('Alta', 'Media', 'Baja')");
                table.HasCheckConstraint("chk_tramites_estado",
                    "Estado IN ('Activo', 'Inactivo')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Descripcion).HasColumnType("text");
            entity.Property(x => x.TiempoEstimadoMinutos).IsRequired();
            entity.Property(x => x.NivelDemanda).HasMaxLength(20).IsRequired().HasDefaultValue("Media");
            entity.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("Activo");
            entity.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("uq_tramites_nombre");
            entity.HasIndex(x => x.Estado).HasDatabaseName("idx_tramites_estado");
            entity.HasIndex(x => x.NivelDemanda).HasDatabaseName("idx_tramites_demanda");
        });

        modelBuilder.Entity<PuntoAtencion>(entity =>
        {
            entity.ToTable("PuntosAtencion", table =>
            {
                table.HasCheckConstraint("chk_puntos_carga",
                    "CargaActualPorcentaje BETWEEN 0 AND 100");
                table.HasCheckConstraint("chk_puntos_cola", "ColaUsuarios >= 0");
                table.HasCheckConstraint("chk_puntos_estado",
                    "Estado IN ('Activa', 'Inactiva', 'Finalizando turno', 'En descanso')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CargaActualPorcentaje).IsRequired().HasDefaultValue(0);
            entity.Property(x => x.ColaUsuarios).IsRequired().HasDefaultValue(0);
            entity.Property(x => x.Estado).HasMaxLength(30).IsRequired().HasDefaultValue("Activa");
            entity.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("uq_puntos_nombre");
            entity.HasIndex(x => new { x.Estado, x.CargaActualPorcentaje })
                .HasDatabaseName("idx_puntos_estado_carga");
        });

        modelBuilder.Entity<TramitePuntoAtencion>(entity =>
        {
            entity.ToTable("TramitePuntoAtencion");
            entity.HasKey(x => new { x.TramiteId, x.PuntoAtencionId });
            entity.HasIndex(x => x.PuntoAtencionId).HasDatabaseName("idx_tpa_punto");

            entity.HasOne(x => x.Tramite)
                .WithMany(x => x.TramitesPuntosAtencion)
                .HasForeignKey(x => x.TramiteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PuntoAtencion)
                .WithMany(x => x.TramitesPuntosAtencion)
                .HasForeignKey(x => x.PuntoAtencionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RegistroDemanda>(entity =>
        {
            entity.ToTable("RegistrosDemanda", table => table.HasCheckConstraint(
                "chk_demanda_cantidad", "CantidadUsuarios >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FranjaHoraria).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Fecha).HasColumnType("date").IsRequired();
            entity.Property(x => x.CantidadUsuarios).IsRequired();
            entity.HasIndex(x => new { x.Fecha, x.FranjaHoraria, x.TramiteId })
                .IsUnique().HasDatabaseName("uq_demanda_franja");
            entity.HasIndex(x => new { x.Fecha, x.FranjaHoraria })
                .HasDatabaseName("idx_demanda_fecha_franja");
            entity.HasIndex(x => new { x.TramiteId, x.Fecha })
                .HasDatabaseName("idx_demanda_tramite_fecha");

            entity.HasOne(x => x.Tramite)
                .WithMany(x => x.RegistrosDemanda)
                .HasForeignKey(x => x.TramiteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EscenarioSimulacion>(entity =>
        {
            entity.ToTable("EscenariosSimulacion", table =>
            {
                table.HasCheckConstraint("chk_escenario_usuarios",
                    "CantidadUsuariosEstimados >= 0");
                table.HasCheckConstraint("chk_escenario_puntos", "PuntosActivos >= 0");
                table.HasCheckConstraint("chk_escenario_horario", "HoraFin > HoraInicio");
                table.HasCheckConstraint("chk_escenario_metodo",
                    "MetodoAsignacion IN ('Convencional', 'Dinamica')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CantidadUsuariosEstimados).IsRequired();
            entity.Property(x => x.PuntosActivos).IsRequired();
            entity.Property(x => x.HoraInicio).HasColumnType("time").IsRequired();
            entity.Property(x => x.HoraFin).HasColumnType("time").IsRequired();
            entity.Property(x => x.MetodoAsignacion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FechaCreacion).HasColumnType("datetime").IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.FechaCreacion).HasDatabaseName("idx_escenarios_fecha");
            entity.HasIndex(x => x.MetodoAsignacion).HasDatabaseName("idx_escenarios_metodo");
        });

        modelBuilder.Entity<TurnoSimulado>(entity =>
        {
            entity.ToTable("TurnosSimulados", table =>
            {
                table.HasCheckConstraint("chk_turno_espera",
                    "TiempoEsperaEstimadoMinutos >= 0");
                table.HasCheckConstraint("chk_turno_estado",
                    "Estado IN ('En espera', 'Asignado', 'En atencion', 'Atendido', 'Cancelado')");
            });

            modelBuilder.Entity<TurnoAsignacionHistorial>(entity =>
            {
                entity.ToTable("TurnosAsignacionesHistorial", table =>
                    table.HasCheckConstraint(
                        "chk_historial_tipo",
                        "TipoMovimiento IN ('Asignacion inicial', 'Reasignacion')"));
                entity.HasKey(item => item.Id);
                entity.Property(item => item.TipoMovimiento).HasMaxLength(30).IsRequired();
                entity.Property(item => item.Motivo).HasMaxLength(200).IsRequired();
                entity.Property(item => item.FechaHoraMovimiento)
                    .HasColumnType("datetime")
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(item => new
                {
                    item.TurnoSimuladoId,
                    item.FechaHoraMovimiento
                }).HasDatabaseName("idx_historial_turno_fecha");
                entity.HasIndex(item => item.FechaHoraMovimiento)
                    .HasDatabaseName("idx_historial_fecha");
                entity.HasOne(item => item.TurnoSimulado)
                    .WithMany(item => item.HistorialAsignaciones)
                    .HasForeignKey(item => item.TurnoSimuladoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.PuntoAtencionOrigen)
                    .WithMany(item => item.HistorialComoOrigen)
                    .HasForeignKey(item => item.PuntoAtencionOrigenId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.PuntoAtencionDestino)
                    .WithMany(item => item.HistorialComoDestino)
                    .HasForeignKey(item => item.PuntoAtencionDestinoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodigoTurno).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FechaHoraSolicitud).HasColumnType("datetime").IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.FechaHoraAtencion).HasColumnType("datetime");
            entity.Property(x => x.TiempoEsperaEstimadoMinutos).IsRequired().HasDefaultValue(0);
            entity.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("En espera");
            entity.HasIndex(x => x.CodigoTurno).IsUnique().HasDatabaseName("uq_turno_codigo");
            entity.HasIndex(x => new { x.Estado, x.FechaHoraSolicitud })
                .HasDatabaseName("idx_turno_estado_fecha");
            entity.HasIndex(x => new { x.PuntoAtencionId, x.Estado })
                .HasDatabaseName("idx_turno_punto_estado");
            entity.HasIndex(x => x.EscenarioId).HasDatabaseName("idx_turno_escenario");

            entity.HasOne(x => x.EscenarioSimulacion)
                .WithMany(x => x.TurnosSimulados)
                .HasForeignKey(x => x.EscenarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Tramite)
                .WithMany(x => x.TurnosSimulados)
                .HasForeignKey(x => x.TramiteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PuntoAtencion)
                .WithMany(x => x.TurnosSimulados)
                .HasForeignKey(x => x.PuntoAtencionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
