namespace PrecoBoi.Api.DTOs;

public record NegociacaoRequest(
    int CompradorId,
    int CorretorId,
    int MunicipioOrigemId,
    int MunicipioDestinoId,
    DateTime? DataPrevistaEntrega,
    string? Observacoes,
    List<NegociacaoItemRequest> Itens,
    string TipoNegocio = "KG"
);

public record NegociacaoItemRequest(
    int CategoriaId,
    int? QtdNegociada,
    decimal? PrecoNegociado,
    decimal? PesoMedio
);

public record FechamentoRequest(int NegociacaoId);

public record NegociacaoFiltroRequest(
    int? CompradorId,
    int? CorretorId,
    string? Categoria,
    string? Uf,
    string? CidadeOrigem,
    string? Status,
    int? Ano,
    int? Mes,
    DateTime? DataInicio = null,
    DateTime? DataFim = null,
    string? Comissao = null, // "Paga", "NaoPaga" ou null/"Todas"
    int Pagina = 1,
    int TamanhoPagina = 20
);

public record AlterarStatusRequest(string Status, string? Motivo);

public record AlterarComissaoRequest(bool Paga);
