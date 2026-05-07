namespace PrecoBoi.Api.Models;

public class Negociacao
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty; // NNN/AAAA
    public int CompradorId { get; set; }
    public string CompradorNome { get; set; } = string.Empty;
    public int CorretorId { get; set; }
    public string CorretorNome { get; set; } = string.Empty;
    public int MunicipioOrigemId { get; set; }
    public string MunicipioOrigemNome { get; set; } = string.Empty;
    public string MunicipioOrigemUf { get; set; } = string.Empty;
    public int MunicipioDestinoId { get; set; }
    public string MunicipioDestinoNome { get; set; } = string.Empty;
    public DateTime? DataPrevistaEntrega { get; set; }
    public string Status { get; set; } = "EmNegociacao"; // EmNegociacao, Fechado
    public DateTime? DataFechamento { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime? AtualizadoEm { get; set; }
    public List<NegociacaoItem> Itens { get; set; } = new();
}

public class NegociacaoItem
{
    public int Id { get; set; }
    public int NegociacaoId { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public decimal PesoMin { get; set; }
    public decimal PesoMax { get; set; }
    public int? QtdNegociada { get; set; }
    public decimal? PrecoNegociado { get; set; } // R$ Praça (origem)
    public decimal? PesoMedio { get; set; }
    public decimal? PrecoColocado { get; set; } // calculado
    public int QtdEntregue { get; set; } = 0;
    public string StatusEntrega { get; set; } = "Pendente"; // Pendente, Parcial, Concluido
    public decimal PercentualConclusao => QtdNegociada.HasValue && QtdNegociada > 0
        ? Math.Round((decimal)QtdEntregue / QtdNegociada.Value * 100, 1)
        : 0;
}
