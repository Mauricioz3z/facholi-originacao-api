using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioRepository _repo;
    private readonly AuditoriaRepository _auditoriaRepo;

    public UsuariosController(UsuarioRepository repo, AuditoriaRepository auditoriaRepo)
    {
        _repo = repo;
        _auditoriaRepo = auditoriaRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? ativo)
    {
        var usuarios = await _repo.Listar(ativo);
        var response = usuarios.Select(u => new UsuarioResponse(
            u.Id, u.Nome, u.Email, u.Telefone, u.Perfil, u.Ativo, u.CriadoEm));
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var u = await _repo.ObterPorId(id);
        if (u == null) return NotFound();
        return Ok(new UsuarioResponse(u.Id, u.Nome, u.Email, u.Telefone, u.Perfil, u.Ativo, u.CriadoEm));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
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

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
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

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
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
