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

// Modo B (sem ICMS): cotação da praça + frete, sem deduzir ICMS
public record OportunidadePracaItemResponse(
    int MunicipioId,
    string Nome,
    string Uf,
    decimal DistanciaKm,
    decimal FreteKg,
    decimal CotacaoPracaKg,    // cotação da praça da UF convertida em R$/kg para a categoria
    decimal CustoColocadoKg    // = CotacaoPracaKg + FreteKg
);
