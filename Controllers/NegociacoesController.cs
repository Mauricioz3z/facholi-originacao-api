using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

/// <summary>Negociações de compra de gado: criação, edição, fechamento e entregas.</summary>
/// <remarks>
/// O ciclo de vida da negociação é <c>EmNegociacao → Fechado</c>. Edição e exclusão
/// seguem regras de autorização por perfil aplicadas no serviço de domínio.
/// </remarks>
[ApiController]
[Route("api/negociacoes")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Ciclo de vida das negociações e controle de entregas")]
public class NegociacoesController : ControllerBase
{
    private readonly NegociacaoService _negService;
    private readonly NegociacaoRepository _negRepo;

    public NegociacoesController(NegociacaoService negService, NegociacaoRepository negRepo)
    {
        _negService = negService;
        _negRepo = negRepo;
    }

    /// <summary>Lista negociações de forma paginada, com filtros opcionais.</summary>
    /// <param name="filtro">Filtros (comprador, corretor, categoria, UF, cidade, status) e paginação.</param>
    /// <response code="200">Página de negociações com o total de registros.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var (items, total) = await _negRepo.Listar(filtro);
        return Ok(new { items, total, pagina = filtro.Pagina, tamanhoPagina = filtro.TamanhoPagina });
    }

    /// <summary>Obtém uma negociação completa (com itens) pelo identificador.</summary>
    /// <param name="id">Identificador da negociação.</param>
    /// <response code="200">Negociação encontrada.</response>
    /// <response code="404">Negociação não encontrada.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Negociacao), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var neg = await _negRepo.ObterPorId(id);
        if (neg == null) return NotFound();
        return Ok(neg);
    }

    /// <summary>Cria uma nova negociação com seus itens por categoria.</summary>
    /// <param name="request">Dados da negociação (comprador, corretor, rota, entrega e itens).</param>
    /// <response code="201">Negociação criada.</response>
    /// <response code="400">Dados inválidos ou regra de negócio violada (mensagem em <c>mensagem</c>).</response>
    [HttpPost]
    [ProducesResponseType(typeof(Negociacao), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Atualiza uma negociação existente.</summary>
    /// <param name="id">Identificador da negociação.</param>
    /// <param name="request">Novos dados da negociação.</param>
    /// <response code="200">Negociação atualizada.</response>
    /// <response code="400">Dados inválidos ou regra de negócio violada.</response>
    /// <response code="403">Usuário sem permissão para alterar esta negociação.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Negociacao), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Atualizar(int id, [FromBody] NegociacaoRequest request)
    {
        try
        {
            var neg = await _negService.Atualizar(id, request, ObterUsuarioId(), ObterUsuarioNome(), ObterUsuarioPerfil());
            return Ok(neg);
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

    /// <summary>Exclui uma negociação.</summary>
    /// <param name="id">Identificador da negociação.</param>
    /// <response code="204">Negociação excluída.</response>
    /// <response code="400">Regra de negócio impede a exclusão.</response>
    /// <response code="403">Usuário sem permissão para excluir esta negociação.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            await _negService.Excluir(id, ObterUsuarioId(), ObterUsuarioNome(), ObterUsuarioPerfil());
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

    /// <summary>Fecha uma negociação, mudando seu status para <c>Fechado</c>.</summary>
    /// <param name="id">Identificador da negociação.</param>
    /// <response code="204">Negociação fechada.</response>
    /// <response code="400">Negociação não pode ser fechada (ex.: itens incompletos).</response>
    [HttpPost("{id}/fechar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Registra as quantidades entregues dos itens de uma negociação.</summary>
    /// <remarks>Atualiza o status de entrega de cada item (<c>Pendente</c>, <c>Parcial</c>, <c>Concluido</c>).</remarks>
    /// <param name="request">Negociação e quantidades entregues por item.</param>
    /// <response code="204">Entregas registradas.</response>
    /// <response code="400">Dados inválidos ou regra de negócio violada.</response>
    [HttpPut("entrega")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    private string ObterUsuarioPerfil() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "";
}
