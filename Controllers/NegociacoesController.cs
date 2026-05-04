using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api/negociacoes")]
[Authorize]
public class NegociacoesController : ControllerBase
{
    private readonly NegociacaoService _negService;
    private readonly NegociacaoRepository _negRepo;

    public NegociacoesController(NegociacaoService negService, NegociacaoRepository negRepo)
    {
        _negService = negService;
        _negRepo = negRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var (items, total) = await _negRepo.Listar(filtro);
        return Ok(new { items, total, pagina = filtro.Pagina, tamanhoPagina = filtro.TamanhoPagina });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var neg = await _negRepo.ObterPorId(id);
        if (neg == null) return NotFound();
        return Ok(neg);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] NegociacaoRequest request)
    {
        try
        {
            var neg = await _negService.Criar(request, ObterUsuarioId(), ObterUsuarioNome());
            return CreatedAtAction(nameof(ObterPorId), new { id = neg.Id }, neg);
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] NegociacaoRequest request)
    {
        try
        {
            var neg = await _negService.Atualizar(id, request, ObterUsuarioId(), ObterUsuarioNome());
            return Ok(neg);
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/fechar")]
    public async Task<IActionResult> Fechar(int id)
    {
        try
        {
            await _negService.Fechar(id, ObterUsuarioId(), ObterUsuarioNome());
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPut("entrega")]
    public async Task<IActionResult> AtualizarEntrega([FromBody] EntregaRequest request)
    {
        try
        {
            await _negService.AtualizarEntrega(request, ObterUsuarioId(), ObterUsuarioNome());
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    private int ObterUsuarioId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : 0;
    }

    private string ObterUsuarioNome() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "";
}
