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

    public SimulacaoController(
        CalculoService calculoService,
        MunicipioOrigemRepository munOrigemRepo,
        MunicipioDestinoRepository munDestinoRepo,
        CategoriaRepository catRepo)
    {
        _calculoService = calculoService;
        _munOrigemRepo = munOrigemRepo;
        _munDestinoRepo = munDestinoRepo;
        _catRepo = catRepo;
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

        var origens = await _munOrigemRepo.Listar(ativo: true);
        var resultados = new List<OportunidadeItemResponse>();

        foreach (var origem in origens)
        {
            var resultado = await _calculoService.CalcularPraca(precoColocado, origem, categoria);
            resultados.Add(new OportunidadeItemResponse(
                origem.Id, origem.Nome, origem.Uf, origem.DistanciaKm,
                resultado.FreteKg, resultado.ValorIcms, resultado.ValorComissao,
                resultado.PrecoPraca));
        }

        return Ok(resultados.OrderByDescending(r => r.PrecoPraca).ToList());
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
