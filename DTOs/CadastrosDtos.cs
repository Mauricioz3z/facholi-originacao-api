namespace PrecoBoi.Api.DTOs;

public record CorretorRequest(string Nome, string Telefone, string Municipio, string Uf, string Propriedade, string Observacoes, bool Ativo);

public record MunicipioOrigemRequest(string Nome, string Uf, decimal DistanciaKm, decimal ValorKm, bool Ativo);

public record MunicipioDestinoRequest(string Nome, string Uf, bool Padrao);

public record CategoriaRequest(string Nome, decimal PesoMin, decimal PesoMax, decimal PesoMedio, int CabCaminhao, int Ordem);

public record IcmsRequest(string Uf, decimal Aliquota, decimal Recuperacao);

public record CotacaoRegionalRequest(
    string Uf,
    string? PracaReferenciaUf,
    decimal ValorArroba,
    List<AgioCotacaoRequest> Agios
);

public record AgioCotacaoRequest(int CategoriaId, decimal Percentual);

public record ConfigComissaoRequest(decimal Percentual, bool Ativo);

public record AuditoriaFiltroRequest(string? Tabela, int? UsuarioId, DateTime? DataInicio, DateTime? DataFim, int Pagina = 1, int TamanhoPagina = 50);
