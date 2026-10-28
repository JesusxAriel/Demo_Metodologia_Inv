namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class Tramite
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int TiempoEstimadoMinutos { get; set; }
    public string NivelDemanda { get; set; } = "Media";
    public string Estado { get; set; } = "Activo";

    public ICollection<TramitePuntoAtencion> TramitesPuntosAtencion { get; set; } = [];
    public ICollection<RegistroDemanda> RegistrosDemanda { get; set; } = [];
    public ICollection<TurnoSimulado> TurnosSimulados { get; set; } = [];
}
