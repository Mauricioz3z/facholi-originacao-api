namespace PrecoBoi.Api.Models;

public class MunicipioOrigem
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public decimal DistanciaKm { get; set; }
    public decimal ValorKm { get; set; }
    public bool Ativo { get; set; } = true;
}
