namespace PrecoBoi.Api.DTOs;

public record NegociacaoProdutorRequest(
    int CategoriaId,
    string ProdutorOrigem,
    int QtdCb,
    string? Observacoes
);

// Se NegociacaoProdutorId for informado, usa um lote já existente (descontando o
// saldo disponível dele). Se for null, cria um lote novo na hora: CategoriaId e
// QtdTotalProdutor (tamanho do lote a criar) passam a ser obrigatórios, e
// QtdEmbarcada não pode passar de QtdTotalProdutor.
public record EmbarqueItemRequest(
    int? NegociacaoProdutorId,
    int? CategoriaId,
    int? QtdTotalProdutor,
    int QtdEmbarcada
);

public record EmbarqueRequest(
    string ProdutorOrigem,
    int? MunicipioDestinoId,
    DateTime? DataEmbarque,
    string? Nf,
    string? Gta,
    List<EmbarqueItemRequest> Itens
);

public record ChegadaItemRequest(
    int EmbarqueItemId,
    int QtdChegou,
    decimal? PesoMedioEntrada,
    int AnimaisDebilitados
);

public record ChegadaRequest(
    List<ChegadaItemRequest> Itens,
    string? ObservacoesChegada
);

public record DocumentosRequest(string? Nf, string? Gta);

public record ConferenciaRequest(
    decimal? ValorTotalNegociacao,
    decimal? ValorTotalIcms,
    decimal? ComissaoCb,
    decimal? IcmsCb,
    decimal? FreteCb,
    decimal? DespesaCb,
    string? ObservacaoOcorrencias
);
