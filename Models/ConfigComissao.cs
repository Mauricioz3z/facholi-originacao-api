namespace PrecoBoi.Api.Models;

public class ConfigComissao
{
    public int Id { get; set; }
    public decimal Percentual { get; set; } = 1.0m;
    public bool Ativo { get; set; } = true;
}
