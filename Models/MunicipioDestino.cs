namespace PrecoBoi.Api.Models;

public class MunicipioDestino
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public bool Padrao { get; set; } = false;
}
