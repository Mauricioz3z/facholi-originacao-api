using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace PrecoBoi.Api.Controllers;

/// <summary>Gestão de usuários do sistema (compradores e administradores).</summary>
/// <remarks>Criação, edição e exclusão são restritas ao perfil <b>Admin</b>.</remarks>
[ApiController]
[Route("api/usuarios")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("CRUD de usuários — escrita restrita a Admin")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioRepository _repo;
    private readonly AuditoriaRepository _auditoriaRepo;

    public UsuariosController(UsuarioRepository repo, AuditoriaRepository auditoriaRepo)
    {
        _repo = repo;
        _auditoriaRepo = auditoriaRepo;
    }

    /// <summary>Lista os usuários cadastrados.</summary>
    /// <param name="ativo">Filtro opcional por status: <c>true</c> apenas ativos, <c>false</c> apenas inativos, omitido = todos.</param>
    /// <response code="200">Lista de usuários.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] bool? ativo)
    {
        var usuarios = await _repo.Listar(ativo);
        var response = usuarios.Select(u => new UsuarioResponse(
            u.Id, u.Nome, u.Email, u.Telefone, u.Perfil, u.Ativo, u.CriadoEm));
        return Ok(response);
    }

    /// <summary>Obtém um usuário pelo identificador.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <response code="200">Usuário encontrado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var u = await _repo.ObterPorId(id);
        if (u == null) return NotFound();
        return Ok(new UsuarioResponse(u.Id, u.Nome, u.Email, u.Telefone, u.Perfil, u.Ativo, u.CriadoEm));
    }

    /// <summary>Cria um novo usuário. <b>Requer perfil Admin.</b></summary>
    /// <param name="request">Dados do usuário, incluindo a senha em texto puro (armazenada com hash BCrypt).</param>
    /// <response code="201">Usuário criado.</response>
    /// <response code="409">Já existe um usuário com o e-mail informado.</response>
    /// <response code="403">Usuário autenticado não possui perfil Admin.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Criar([FromBody] UsuarioRequest request)
    {
        var existente = await _repo.ObterPorEmail(request.Email);
        if (existente != null) return Conflict(new { mensagem = "E-mail já cadastrado" });

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Email = request.Email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            Telefone = request.Telefone,
            Perfil = request.Perfil,
            Ativo = request.Ativo
        };
        var id = await _repo.Criar(usuario);
        usuario.Id = id;
        await _auditoriaRepo.Registrar("usuarios", id, "criacao", null, request.Nome,
            ObterUsuarioId(), ObterUsuarioNome(), $"Usuário {request.Nome} criado");
        return CreatedAtAction(nameof(ObterPorId), new { id }, new UsuarioResponse(
            id, usuario.Nome, usuario.Email, usuario.Telefone, usuario.Perfil, usuario.Ativo, usuario.CriadoEm));
    }

    /// <summary>Atualiza um usuário existente. <b>Requer perfil Admin.</b></summary>
    /// <remarks>A senha só é alterada quando o campo <c>senha</c> é informado e não vazio.</remarks>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="request">Novos dados do usuário.</param>
    /// <response code="204">Usuário atualizado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    /// <response code="403">Usuário autenticado não possui perfil Admin.</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Atualizar(int id, [FromBody] UsuarioUpdateRequest request)
    {
        var usuario = await _repo.ObterPorId(id);
        if (usuario == null) return NotFound();

        usuario.Nome = request.Nome;
        usuario.Email = request.Email;
        usuario.Telefone = request.Telefone;
        usuario.Perfil = request.Perfil;
        usuario.Ativo = request.Ativo;
        await _repo.Atualizar(usuario);

        if (!string.IsNullOrEmpty(request.Senha))
            await _repo.AtualizarSenha(id, BCrypt.Net.BCrypt.HashPassword(request.Senha));

        await _auditoriaRepo.Registrar("usuarios", id, "edicao", null, request.Nome,
            ObterUsuarioId(), ObterUsuarioNome(), $"Usuário {request.Nome} atualizado");
        return NoContent();
    }

    /// <summary>Exclui um usuário. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <response code="204">Usuário excluído.</response>
    /// <response code="404">Usuário não encontrado.</response>
    /// <response code="403">Usuário autenticado não possui perfil Admin.</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Excluir(int id)
    {
        var usuario = await _repo.ObterPorId(id);
        if (usuario == null) return NotFound();
        await _repo.Excluir(id);
        return NoContent();
    }

    private int? ObterUsuarioId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : null;
    }

    private string ObterUsuarioNome() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
}
