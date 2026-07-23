using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

/// <summary>Embarques (carregamentos de caminhão) e registro de chegada na fazenda.</summary>
[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Embarques, chegada e desmembramento de saldo")]
public class EmbarquesController : ControllerBase
{
    private readonly EmbarqueService _embarqueService;
    private readonly EmbarqueRepository _embarqueRepo;
    private readonly ConferenciaService _conferenciaService;

    public EmbarquesController(EmbarqueService embarqueService, EmbarqueRepository embarqueRepo, ConferenciaService conferenciaService)
    {
        _embarqueService = embarqueService;
        _embarqueRepo = embarqueRepo;
        _conferenciaService = conferenciaService;
    }

    /// <summary>Lista os embarques de uma negociação.</summary>
    [HttpGet("negociacoes/{negociacaoId}/embarques")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(int negociacaoId)
    {
        var embarques = await _embarqueRepo.ListarPorNegociacao(negociacaoId);
        return Ok(embarques);
    }

    /// <summary>Lista embarques ainda sem chegada confirmada (para a tela de Chegada selecionar a minuta).</summary>
    [HttpGet("embarques/pendentes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPendentes()
    {
        var embarques = await _embarqueRepo.ListarPendentes();
        return Ok(embarques);
    }

    /// <summary>Obtém um embarque pelo identificador.</summary>
    [HttpGet("embarques/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var embarque = await _embarqueRepo.ObterPorId(id);
        if (embarque == null) return NotFound();
        return Ok(embarque);
    }

    /// <summary>Cria um novo embarque (carregamento de caminhão) para a negociação.</summary>
    [HttpPost("negociacoes/{negociacaoId}/embarques")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(int negociacaoId, [FromBody] EmbarqueRequest request)
    {
        try
        {
            var embarque = await _embarqueService.Criar(negociacaoId, request, ObterUsuarioId(), ObterUsuarioNome());
            return CreatedAtAction(nameof(ObterPorId), new { id = embarque.Id }, embarque);
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Atualiza um embarque (bloqueado após a chegada ser confirmada).</summary>
    [HttpPut("embarques/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(int id, [FromBody] EmbarqueRequest request)
    {
        try
        {
            var embarque = await _embarqueService.Atualizar(id, request, ObterUsuarioId(), ObterUsuarioNome());
            return Ok(embarque);
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Exclui um embarque (bloqueado após a chegada ser confirmada).</summary>
    [HttpDelete("embarques/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            await _embarqueService.Excluir(id, ObterUsuarioId(), ObterUsuarioNome());
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Registra a chegada na fazenda (quantidade recebida e peso médio por categoria).</summary>
    /// <remarks>
    /// Sem restrição de perfil — qualquer usuário autenticado pode confirmar a chegada
    /// (o cliente define operacionalmente quem usa esta tela no campo). Não trava por
    /// excedente: se a quantidade recebida ultrapassar a embarcada, apenas registra.
    /// </remarks>
    [HttpPut("embarques/{id}/chegada")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarChegada(int id, [FromBody] ChegadaRequest request)
    {
        try
        {
            await _embarqueService.RegistrarChegada(id, request, ObterUsuarioId(), ObterUsuarioNome());
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Atualiza NF/GTA de um embarque (permitido mesmo após a chegada confirmada).</summary>
    [HttpPut("embarques/{id}/documentos")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarDocumentos(int id, [FromBody] DocumentosRequest request)
    {
        try
        {
            await _embarqueService.AtualizarDocumentos(id, request.Nf, request.Gta, ObterUsuarioId(), ObterUsuarioNome());
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Obtém (ou inicia) a conferência administrativa de um embarque.</summary>
    [HttpGet("embarques/{id}/conferencia")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterConferencia(int id)
    {
        var conf = await _conferenciaService.ObterOuCriar(id);
        return Ok(conf);
    }

    /// <summary>Salva (rascunho) os dados da conferência administrativa.</summary>
    [HttpPut("embarques/{id}/conferencia")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SalvarConferencia(int id, [FromBody] ConferenciaRequest request)
    {
        try
        {
            var conf = await _conferenciaService.Salvar(id, request, ObterUsuarioId(), ObterUsuarioNome());
            return Ok(conf);
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Finaliza a conferência administrativa (trava novas edições).</summary>
    [HttpPost("embarques/{id}/conferencia/finalizar")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FinalizarConferencia(int id)
    {
        try
        {
            var conf = await _conferenciaService.Finalizar(id, ObterUsuarioId(), ObterUsuarioNome());
            return Ok(conf);
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
}
