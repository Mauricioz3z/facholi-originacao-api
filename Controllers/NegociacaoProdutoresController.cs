using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

/// <summary>Desmembramento de negociações por produtor/origem (lotes).</summary>
[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Lotes por produtor/origem dentro de uma negociação")]
public class NegociacaoProdutoresController : ControllerBase
{
    private readonly ProdutorService _produtorService;
    private readonly NegociacaoProdutorRepository _produtorRepo;

    public NegociacaoProdutoresController(ProdutorService produtorService, NegociacaoProdutorRepository produtorRepo)
    {
        _produtorService = produtorService;
        _produtorRepo = produtorRepo;
    }

    /// <summary>Lista os lotes (produtor + categoria) de uma negociação.</summary>
    [HttpGet("negociacoes/{negociacaoId}/produtores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(int negociacaoId)
    {
        var lotes = await _produtorRepo.ListarPorNegociacao(negociacaoId);
        return Ok(lotes);
    }

    /// <summary>Adiciona um lote (produtor + categoria) à negociação.</summary>
    [HttpPost("negociacoes/{negociacaoId}/produtores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Criar(int negociacaoId, [FromBody] NegociacaoProdutorRequest request)
    {
        try
        {
            var lote = await _produtorService.Adicionar(negociacaoId, request, ObterUsuarioId(), ObterUsuarioNome(), ObterUsuarioPerfil());
            return Ok(lote);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Atualiza um lote existente.</summary>
    [HttpPut("produtores/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Atualizar(int id, [FromBody] NegociacaoProdutorRequest request)
    {
        try
        {
            var lote = await _produtorService.Atualizar(id, request, ObterUsuarioId(), ObterUsuarioNome(), ObterUsuarioPerfil());
            return Ok(lote);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Exclui um lote (bloqueado se já tiver embarques vinculados).</summary>
    [HttpDelete("produtores/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            await _produtorService.Excluir(id, ObterUsuarioId(), ObterUsuarioNome(), ObterUsuarioPerfil());
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { mensagem = ex.Message });
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

    private string ObterUsuarioNome() => User.FindFirstValue(ClaimTypes.Name) ?? "";

    private string ObterUsuarioPerfil() => User.FindFirstValue(ClaimTypes.Role) ?? "";
}
