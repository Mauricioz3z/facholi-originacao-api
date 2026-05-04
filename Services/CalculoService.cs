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

        var freteKg = CalcularFreteKg(municipioOrigem, categoria);
        var valorNaCompra = precoColocado - freteKg;
        var icmsEfetivo = icms?.IcmsEfetivo ?? 0m;
        var valorIcms = valorNaCompra * icmsEfetivo;
        var valorComissao = config != null && config.Ativo ? valorNaCompra * (config.Percentual / 100m) : 0m;
        var precoPraca = valorNaCompra - valorIcms - valorComissao;

        // Arredondamento para baixo, 1 casa decimal
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
