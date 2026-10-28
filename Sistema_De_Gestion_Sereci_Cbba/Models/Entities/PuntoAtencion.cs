namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class PuntoAtencion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int CargaActualPorcentaje { get; set; }
    public int ColaUsuarios { get; set; }
    public string Estado { get; set; } = "Activa";

    public ICollection<TramitePuntoAtencion> TramitesPuntosAtencion { get; set; } = [];
    public ICollection<TurnoSimulado> TurnosSimulados { get; set; } = [];
    public ICollection<TurnoAsignacionHistorial> HistorialComoOrigen { get; set; } = [];
    public ICollection<TurnoAsignacionHistorial> HistorialComoDestino { get; set; } = [];
}
