namespace PrecoBoi.Api.Models;

// Lote de desmembramento da negociação: um produtor/origem + uma categoria.
// Alimenta os embarques (cada embarque_item referencia um lote).
public class NegociacaoProdutor
{
    public int Id { get; set; }
    public int NegociacaoId { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public string ProdutorOrigem { get; set; } = string.Empty;
    public int QtdCb { get; set; }
    public string? Observacoes { get; set; }
    public int QtdEmbarcada { get; set; } // soma de embarque_itens.qtd_embarcada — vem de join/agregação
    public int QtdRecebida { get; set; } // soma de embarque_itens.qtd_chegou — vem de join/agregação
    public int SaldoEmbarque => QtdCb - QtdEmbarcada;
    public int SaldoRecebido => QtdCb - QtdRecebida;
    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime? AtualizadoEm { get; set; }
}
