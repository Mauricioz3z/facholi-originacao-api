using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api/simulacao")]
[Authorize]
public class SimulacaoController : ControllerBase
{
    private readonly CalculoService _calculoService;
    private readonly MunicipioOrigemRepository _munOrigemRepo;
    private readonly MunicipioDestinoRepository _munDestinoRepo;
    private readonly CategoriaRepository _catRepo;
    private readonly IcmsRepository _icmsRepo;
    private readonly ConfigComissaoRepository _configRepo;
    private readonly CotacaoRegionalRepository _cotacaoRepo;

    public SimulacaoController(
        CalculoService calculoService,
        MunicipioOrigemRepository munOrigemRepo,
        MunicipioDestinoRepository munDestinoRepo,
        CategoriaRepository catRepo,
        IcmsRepository icmsRepo,
        ConfigComissaoRepository configRepo,
        CotacaoRegionalRepository cotacaoRepo)
    {
        _calculoService = calculoService;
        _munOrigemRepo = munOrigemRepo;
        _munDestinoRepo = munDestinoRepo;
        _catRepo = catRepo;
        _icmsRepo = icmsRepo;
        _configRepo = configRepo;
        _cotacaoRepo = cotacaoRepo;
    }

    [HttpPost]
    public async Task<IActionResult> Calcular([FromBody] SimulacaoRequest request)
    {
        var municipioOrigem = await _munOrigemRepo.ObterPorId(request.MunicipioOrigemId);
        if (municipioOrigem == null) return BadRequest(new { mensagem = "Município de origem não encontrado" });

        var municipioDestino = await _munDestinoRepo.ObterPorId(request.MunicipioDestinoId);
        if (municipioDestino == null) return BadRequest(new { mensagem = "Município de destino não encontrado" });

        var itensResponse = new List<SimulacaoItemResponse>();
        foreach (var item in request.Itens)
        {
            var categoria = await _catRepo.ObterPorId(item.CategoriaId);
            if (categoria == null) continue;

            var resultado = await _calculoService.CalcularPraca(item.PrecoColocado, municipioOrigem, categoria);
            itensResponse.Add(new SimulacaoItemResponse(
                categoria.Id, categoria.Nome, categoria.PesoMin, categoria.PesoMax,
                categoria.PesoMedio, categoria.CabCaminhao,
                item.PrecoColocado, resultado.PrecoPraca, resultado.FreteKg,
                resultado.ValorIcms, resultado.ValorComissao));
        }

        return Ok(new SimulacaoResponse(
            municipioOrigem.Id, municipioOrigem.Nome, municipioOrigem.Uf,
            municipioDestino.Id, municipioDestino.Nome, itensResponse));
    }

    [HttpGet("oportunidades")]
    public async Task<IActionResult> Oportunidades([FromQuery] int categoriaId, [FromQuery] decimal precoColocado)
    {
        var categoria = await _catRepo.ObterPorId(categoriaId);
        if (categoria == null) return BadRequest(new { mensagem = "Categoria não encontrada" });
        if (precoColocado <= 0) return BadRequest(new { mensagem = "Informe um preço colocado válido" });

        // Carrega dados de referência UMA VEZ (evita N+1 dentro do loop de origens)
        var origensTask = _munOrigemRepo.Listar(ativo: true);
        var icmsTask = _icmsRepo.Listar();
        var configTask = _configRepo.Obter();
        var cotacoesTask = _cotacaoRepo.Listar();
        await Task.WhenAll(origensTask, icmsTask, configTask, cotacoesTask);

        var origens = await origensTask;
        var icmsPorUf = (await icmsTask).ToDictionary(i => i.Uf, StringComparer.OrdinalIgnoreCase);
        var config = await configTask;
        var cotacoesPorUf = (await cotacoesTask).ToDictionary(c => c.Uf, StringComparer.OrdinalIgnoreCase);

        var resultados = new List<OportunidadeItemResponse>();
        foreach (var origem in origens)
        {
            icmsPorUf.TryGetValue(origem.Uf, out var icms);
            cotacoesPorUf.TryGetValue(origem.Uf, out var cotacao);

            var resultado = _calculoService.CalcularPraca(precoColocado, origem, categoria, icms, config);
            var cotacaoPracaKg = _calculoService.CalcularCotacaoPracaKg(cotacao, categoria);

            // Valor cru da cotação da UF (R$/@), sem ágio da categoria.
            // Diretoria pediu para exibir o valor "oficial" da praça e usar como referência do deságio.
            var valorArrobaUf = cotacao?.ValorArroba ?? 0m;
            var cotacaoCruaKg = valorArrobaUf / 30m;

            // Deságio: compara o preço colocado (R$/kg) com a cotação CRUA da praça em R$/kg.
            // Positivo = pagando acima da praça (favorável). Negativo = abaixo (difícil).
            decimal? desagioPct = cotacaoCruaKg > 0
                ? (resultado.PrecoPraca / cotacaoCruaKg - 1m) * 100m
                : null;

            resultados.Add(new OportunidadeItemResponse(
                origem.Id, origem.Nome, origem.Uf, origem.DistanciaKm,
                resultado.FreteKg, resultado.ValorIcms, resultado.ValorComissao,
                resultado.PrecoPraca, cotacaoPracaKg, desagioPct, valorArrobaUf));
        }

        // Ranking: melhor oportunidade = maior ágio (positivo) → maior deságio (negativo).
        // Origens sem cotação cadastrada vão para o final.
        return Ok(resultados
            .OrderByDescending(r => r.DesagioPercentual.HasValue)
            .ThenByDescending(r => r.DesagioPercentual ?? decimal.MinValue)
            .ToList());
    }

    // Modo B (sem ICMS): ranking ordenado pelo menor custo colocado (praça + frete)
    [HttpGet("oportunidades-praca")]
    public async Task<IActionResult> OportunidadesPorPraca([FromQuery] int categoriaId)
    {
        var categoria = await _catRepo.ObterPorId(categoriaId);
        if (categoria == null) return BadRequest(new { mensagem = "Categoria não encontrada" });

        // Carrega dados de referência UMA VEZ (evita N+1)
        var origensTask = _munOrigemRepo.Listar(ativo: true);
        var cotacoesTask = _cotacaoRepo.Listar();
        await Task.WhenAll(origensTask, cotacoesTask);

        var origens = await origensTask;
        var cotacoesPorUf = (await cotacoesTask).ToDictionary(c => c.Uf, StringComparer.OrdinalIgnoreCase);

        var resultados = new List<OportunidadePracaItemResponse>();
        foreach (var origem in origens)
        {
            cotacoesPorUf.TryGetValue(origem.Uf, out var cotacao);

            // Pula origens cuja UF não tem cotação ativa (valor_arroba <= 0):
            // significa que a praça não está comprando — incluí-las distorce o ranking,
            // já que custo = 0 + frete daria a falsa impressão de oportunidade barata.
            if (cotacao == null || cotacao.ValorArroba <= 0) continue;

            var resultado = _calculoService.CalcularCustoColocado(origem, categoria, cotacao);
            resultados.Add(new OportunidadePracaItemResponse(
                origem.Id, origem.Nome, origem.Uf, origem.DistanciaKm,
                resultado.FreteKg, resultado.CotacaoPracaKg, resultado.CustoColocadoKg));
        }

        return Ok(resultados.OrderBy(r => r.CustoColocadoKg).ToList());
    }

    // Retorna simulação completa para todas as categorias dado origem/destino
    [HttpGet("rapida")]
    public async Task<IActionResult> SimulacaoRapida([FromQuery] int origemId, [FromQuery] int destinoId)
    {
        var municipioOrigem = await _munOrigemRepo.ObterPorId(origemId);
        if (municipioOrigem == null) return BadRequest(new { mensagem = "Município de origem não encontrado" });

        var municipioDestino = await _munDestinoRepo.ObterPorId(destinoId);
        if (municipioDestino == null) return BadRequest(new { mensagem = "Município de destino não encontrado" });

        var categorias = await _catRepo.Listar();
        var itensResponse = new List<SimulacaoItemResponse>();

        foreach (var categoria in categorias)
        {
            itensResponse.Add(new SimulacaoItemResponse(
                categoria.Id, categoria.Nome, categoria.PesoMin, categoria.PesoMax,
                categoria.PesoMedio, categoria.CabCaminhao,
                0, 0,
                _calculoService.CalcularFreteKg(municipioOrigem, categoria),
                0, 0));
        }

        return Ok(new SimulacaoResponse(
            municipioOrigem.Id, municipioOrigem.Nome, municipioOrigem.Uf,
            municipioDestino.Id, municipioDestino.Nome, itensResponse));
    }
}
