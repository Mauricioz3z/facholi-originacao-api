namespace PrecoBoi.Api.Models;

public class CotacaoRegional
{
    public int Id { get; set; }
    public string Uf { get; set; } = string.Empty;
    public string? PracaReferenciaUf { get; set; }
    public decimal ValorArroba { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public List<AgioCotacao> Agios { get; set; } = new();
}

public class AgioCotacao
{
    public int Id { get; set; }
    public int CotacaoRegionalId { get; set; }
    public int CategoriaId { get; set; }
    public decimal Percentual { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public decimal PesoMin { get; set; }
    public decimal PesoMax { get; set; }
}
