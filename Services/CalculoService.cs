using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Services;

public class CalculoService
{
    private readonly IcmsRepository _icmsRepo;
    private readonly ConfigComissaoRepository _configRepo;
    private readonly CotacaoRegionalRepository _cotacaoRepo;

    public CalculoService(IcmsRepository icmsRepo, ConfigComissaoRepository configRepo, CotacaoRegionalRepository cotacaoRepo)
    {
        _icmsRepo = icmsRepo;
        _configRepo = configRepo;
        _cotacaoRepo = cotacaoRepo;
    }

    // Direção 1: Colocado → Praça (Simulação Rápida)
    // Dado preço posto fazenda, calcula preço na origem
    public async Task<ResultadoCalculo> CalcularPraca(
        decimal precoColocado,
        MunicipioOrigem municipioOrigem,
        Categoria categoria)
    {
        var icms = await _icmsRepo.ObterPorUf(municipioOrigem.Uf);
        var config = await _configRepo.Obter();
        return CalcularPraca(precoColocado, municipioOrigem, categoria, icms, config);
    }

    // Overload sem I/O — para uso em loops (ex.: ranking de oportunidades) carregando
    // ICMS e config uma única vez fora do loop. Evita N+1 de conexões/queries.
    public ResultadoCalculo CalcularPraca(
        decimal precoColocado,
        MunicipioOrigem municipioOrigem,
        Categoria categoria,
        Icms? icms,
        ConfigComissao? config)
    {
        var freteKg = CalcularFreteKg(municipioOrigem, categoria);
        var valorNaCompra = precoColocado - freteKg;
        var icmsEfetivo = icms?.IcmsEfetivo ?? 0m;
        var valorIcms = valorNaCompra * icmsEfetivo;
        var valorComissao = config != null && config.Ativo ? valorNaCompra * (config.Percentual / 100m) : 0m;
        var precoPraca = valorNaCompra - valorIcms - valorComissao;

        precoPraca = ArredondarParaBaixo(precoPraca);

        return new ResultadoCalculo
        {
            PrecoColocado = precoColocado,
            PrecoPraca = precoPraca,
            FreteKg = freteKg,
            ValorIcms = valorIcms,
            ValorComissao = valorComissao,
            ValorNaCompra = valorNaCompra
        };
    }

    // Direção 2: Negociado → Colocado (Negociações)
    // Dado preço negociado na praça, calcula quanto vai custar posto na fazenda
    public async Task<ResultadoCalculo> CalcularColocado(
        decimal precoNegociado,
        MunicipioOrigem municipioOrigem,
        Categoria categoria,
        decimal? pesoMedioOverride = null)
    {
        var categoriaEfetiva = categoria;
        if (pesoMedioOverride.HasValue)
        {
            categoriaEfetiva = new Categoria
            {
                Id = categoria.Id,
                Nome = categoria.Nome,
                PesoMin = categoria.PesoMin,
                PesoMax = categoria.PesoMax,
                PesoMedio = pesoMedioOverride.Value,
                CabCaminhao = categoria.CabCaminhao,
                Ordem = categoria.Ordem
            };
        }

        var icms = await _icmsRepo.ObterPorUf(municipioOrigem.Uf);
        var config = await _configRepo.Obter();

        var freteKg = CalcularFreteKg(municipioOrigem, categoriaEfetiva);
        var icmsEfetivo = icms?.IcmsEfetivo ?? 0m;
        var percentualComissao = config != null && config.Ativo ? config.Percentual / 100m : 0m;

        // Cálculo inverso: Colocado = Negociado + Frete + ICMS + Comissão
        // ICMS e comissão são calculados sobre o valor na compra (= precoNegociado)
        var valorIcms = precoNegociado * icmsEfetivo;
        var valorComissao = precoNegociado * percentualComissao;
        var precoColocado = precoNegociado + freteKg + valorIcms + valorComissao;

        return new ResultadoCalculo
        {
            PrecoColocado = precoColocado,
            PrecoPraca = precoNegociado,
            FreteKg = freteKg,
            ValorIcms = valorIcms,
            ValorComissao = valorComissao,
            ValorNaCompra = precoNegociado
        };
    }

    // Frete por kg conforme especificação
    // FreteKg = (Distância × 2 × ValorKM × 0,88) ÷ CabCaminhão ÷ PesoMédio
    public decimal CalcularFreteKg(MunicipioOrigem municipioOrigem, Categoria categoria)
    {
        if (categoria.PesoMedio <= 0 || categoria.CabCaminhao <= 0) return 0;
        var kmTotal = municipioOrigem.DistanciaKm * 2;
        var valorTotal = kmTotal * municipioOrigem.ValorKm;
        var freteRecuperado = valorTotal * 0.88m;
        var valorPorCabeca = freteRecuperado / categoria.CabCaminhao;
        return valorPorCabeca / categoria.PesoMedio;
    }

    // Direção 3 (Mapa de Oportunidades — Modo B, sem ICMS):
    // Custo de buscar o animal na origem = cotação da praça (R$/kg) + frete (R$/kg)
    // CotacaoPracaKg = (ValorArroba / 30) × (1 + Ágio% da categoria na UF)
    public async Task<ResultadoCustoColocado> CalcularCustoColocado(
        MunicipioOrigem municipioOrigem,
        Categoria categoria)
    {
        var cotacao = await _cotacaoRepo.ObterPorUf(municipioOrigem.Uf);
        return CalcularCustoColocado(municipioOrigem, categoria, cotacao);
    }

    // Overload sem I/O — carrega cotações uma única vez fora do loop.
    public ResultadoCustoColocado CalcularCustoColocado(
        MunicipioOrigem municipioOrigem,
        Categoria categoria,
        CotacaoRegional? cotacao)
    {
        var cotacaoPracaKg = CalcularCotacaoPracaKg(cotacao, categoria);
        var freteKg = CalcularFreteKg(municipioOrigem, categoria);

        return new ResultadoCustoColocado
        {
            CotacaoPracaKg = cotacaoPracaKg,
            FreteKg = freteKg,
            CustoColocadoKg = cotacaoPracaKg + freteKg
        };
    }

    // Converte cotação da praça (R$/@) em R$/kg para a categoria, aplicando o ágio cadastrado.
    public decimal CalcularCotacaoPracaKg(CotacaoRegional? cotacao, Categoria categoria)
    {
        if (cotacao == null || cotacao.ValorArroba <= 0) return 0;
        var agio = cotacao.Agios?.FirstOrDefault(a => a.CategoriaId == categoria.Id);
        var percentual = agio != null ? agio.Percentual / 100m : 0m;
        return (cotacao.ValorArroba / 30m) * (1 + percentual);
    }

    public decimal ArredondarParaBaixo(decimal valor)
    {
        return Math.Floor(valor * 10) / 10;
    }
}

public class ResultadoCalculo
{
    public decimal PrecoColocado { get; set; }
    public decimal PrecoPraca { get; set; }
    public decimal FreteKg { get; set; }
    public decimal ValorIcms { get; set; }
    public decimal ValorComissao { get; set; }
    public decimal ValorNaCompra { get; set; }
}

public class ResultadoCustoColocado
{
    public decimal CotacaoPracaKg { get; set; }
    public decimal FreteKg { get; set; }
    public decimal CustoColocadoKg { get; set; }
}
