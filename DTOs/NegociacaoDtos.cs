namespace PrecoBoi.Api.DTOs;

public record NegociacaoRequest(
    int CompradorId,
    int CorretorId,
    int MunicipioOrigemId,
    int MunicipioDestinoId,
    DateTime? DataPrevistaEntrega,
    string? Observacoes,
    List<NegociacaoItemRequest> Itens
);

public record NegociacaoItemRequest(
    int CategoriaId,
    int? QtdNegociada,
    decimal? PrecoNegociado,
    decimal? PesoMedio
);

public record FechamentoRequest(int NegociacaoId);

public record EntregaItemRequest(int ItemId, int QtdEntregue);

public record EntregaRequest(int NegociacaoId, List<EntregaItemRequest> Itens);

public record NegociacaoFiltroRequest(
    int? CompradorId,
    int? CorretorId,
    string? Categoria,
    string? Uf,
    string? CidadeOrigem,
    string? Status,
    int Pagina = 1,
    int TamanhoPagina = 20
);
