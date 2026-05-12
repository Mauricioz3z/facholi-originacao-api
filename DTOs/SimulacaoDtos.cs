namespace PrecoBoi.Api.DTOs;

public record SimulacaoRequest(
    int MunicipioOrigemId,
    int MunicipioDestinoId,
    List<SimulacaoItemRequest> Itens
);

public record SimulacaoItemRequest(
    int CategoriaId,
    decimal PrecoColocado // R$/kg posto fazenda
);

public record SimulacaoResponse(
    int MunicipioOrigemId,
    string MunicipioOrigemNome,
    string MunicipioOrigemUf,
    int MunicipioDestinoId,
    string MunicipioDestinoNome,
    List<SimulacaoItemResponse> Itens
);

public record SimulacaoItemResponse(
    int CategoriaId,
    string CategoriaNome,
    decimal PesoMin,
    decimal PesoMax,
    decimal PesoMedio,
    int CabCaminhao,
    decimal PrecoColocado,
    decimal PrecoPraca,
    decimal FreteKg,
    decimal ValorIcms,
    decimal ValorComissao
);

public record OportunidadeItemResponse(
    int MunicipioId,
    string Nome,
    string Uf,
    decimal DistanciaKm,
    decimal FreteKg,
    decimal ValorIcms,
    decimal ValorComissao,
    decimal PrecoPraca
);
