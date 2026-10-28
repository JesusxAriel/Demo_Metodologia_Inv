namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class RegistroDemanda
{
    public int Id { get; set; }
    public string FranjaHoraria { get; set; } = string.Empty;
    public int TramiteId { get; set; }
    public int CantidadUsuarios { get; set; }
    public DateOnly Fecha { get; set; }

    public Tramite Tramite { get; set; } = null!;
}
