using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UsuarioRepository _usuarioRepo;

    public AuthController(AuthService authService, UsuarioRepository usuarioRepo)
    {
        _authService = authService;
        _usuarioRepo = usuarioRepo;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.Login(request.Email, request.Senha);
        if (result == null) return Unauthorized(new { mensagem = "E-mail ou senha inválidos" });
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _usuarioRepo.ObterPorId(id);
        if (usuario == null) return NotFound();
        return Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email,
            usuario.Telefone, usuario.Perfil, usuario.Ativo, usuario.CriadoEm));
    }
}
