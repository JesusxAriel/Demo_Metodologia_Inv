namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class TurnoAsignacionHistorial
{
    public long Id { get; set; }
    public int TurnoSimuladoId { get; set; }
    public int? PuntoAtencionOrigenId { get; set; }
    public int? PuntoAtencionDestinoId { get; set; }
    public DateTime FechaHoraMovimiento { get; set; }
    public string TipoMovimiento { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;

    public TurnoSimulado TurnoSimulado { get; set; } = null!;
    public PuntoAtencion? PuntoAtencionOrigen { get; set; }
    public PuntoAtencion? PuntoAtencionDestino { get; set; }
}
