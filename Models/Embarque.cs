namespace PrecoBoi.Api.Models;

// Carregamento de um caminhão, vinculado a uma negociação e a um lote/produtor.
public class Embarque
{
    public int Id { get; set; }
    public int NegociacaoId { get; set; }
    public string? NegociacaoNumero { get; set; }
    public int Numero { get; set; }
    public string ProdutorOrigem { get; set; } = string.Empty;
    public int? MunicipioDestinoId { get; set; }
    public string? MunicipioDestinoNome { get; set; }
    public string? MunicipioDestinoUf { get; set; }
    public DateTime? DataEmbarque { get; set; }
    public string? Nf { get; set; }
    public string? Gta { get; set; }
    public string? ObservacoesChegada { get; set; }
    public DateTime? ChegadaConfirmadaEm { get; set; }
    public int? ChegadaConfirmadaPor { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public int CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public List<EmbarqueItem> Itens { get; set; } = new();

    public string StatusChegada => ChegadaConfirmadaEm.HasValue ? "Recebido" : "Pendente";
}

// Quantidade embarcada/chegada de uma categoria dentro de um embarque.
// A chegada é embutida aqui (qtd_chegou, peso_medio_entrada, animais_debilitados)
// em vez de virar tabela própria: é um evento único por embarque.
public class EmbarqueItem
{
    public int Id { get; set; }
    public int EmbarqueId { get; set; }
    public int NegociacaoProdutorId { get; set; }
    public string ProdutorOrigem { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public int QtdEmbarcada { get; set; }
    public int? QtdChegou { get; set; }
    public decimal? PesoMedioEntrada { get; set; }
    public int AnimaisDebilitados { get; set; }

    // Morte/quebra em transporte: diferença entre o que subiu no caminhão e o que chegou.
    public int? Quebra => QtdChegou.HasValue ? QtdEmbarcada - QtdChegou.Value : null;
}
