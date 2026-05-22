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
    decimal PrecoPraca,
    decimal CotacaoPracaKg,           // cotação local da praça em R$/kg para a categoria (com ágio) — uso interno
    decimal? DesagioPercentual,       // (PrecoPraca / (ValorArrobaUf / 30) − 1) × 100; comparação contra cotação CRUA
    decimal ValorArrobaUf             // valor_arroba CRU de cotacoes_regionais (R$/@), sem ágio da categoria
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
