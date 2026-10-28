namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class TurnoSimulado
{
    public int Id { get; set; }
    public string CodigoTurno { get; set; } = string.Empty;
    public int TramiteId { get; set; }
    public int? PuntoAtencionId { get; set; }
    public int? EscenarioId { get; set; }
    public DateTime FechaHoraSolicitud { get; set; }
    public DateTime? FechaHoraAtencion { get; set; }
    public int TiempoEsperaEstimadoMinutos { get; set; }
    public string Estado { get; set; } = "En espera";

    public EscenarioSimulacion? EscenarioSimulacion { get; set; }
    public Tramite Tramite { get; set; } = null!;
    public PuntoAtencion? PuntoAtencion { get; set; }
    public ICollection<TurnoAsignacionHistorial> HistorialAsignaciones { get; set; } = [];
}
