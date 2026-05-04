namespace PrecoBoi.Api.Models;

public class Icms
{
    public int Id { get; set; }
    public string Uf { get; set; } = string.Empty;
    public decimal Aliquota { get; set; }
    public decimal Recuperacao { get; set; }
    public decimal IcmsEfetivo { get; set; }
}
