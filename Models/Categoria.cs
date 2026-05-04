namespace PrecoBoi.Api.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty; // Bezerro, Garrote, Boi
    public decimal PesoMin { get; set; }
    public decimal PesoMax { get; set; }
    public decimal PesoMedio { get; set; }
    public int CabCaminhao { get; set; }
    public int Ordem { get; set; }
}
