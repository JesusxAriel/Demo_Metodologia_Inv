namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class TramitePuntoAtencion
{
    public int TramiteId { get; set; }
    public int PuntoAtencionId { get; set; }

    public Tramite Tramite { get; set; } = null!;
    public PuntoAtencion PuntoAtencion { get; set; } = null!;
}
