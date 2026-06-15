using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

/// <summary>Autenticação e emissão de tokens JWT.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[SwaggerTag("Login e identificação do usuário autenticado")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UsuarioRepository _usuarioRepo;

    public AuthController(AuthService authService, UsuarioRepository usuarioRepo)
    {
        _authService = authService;
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>Autentica um usuário e retorna um token JWT.</summary>
    /// <remarks>
    /// Endpoint **público**. O token retornado deve ser enviado nas demais requisições
    /// no cabeçalho <c>Authorization: Bearer {token}</c>.
    /// </remarks>
    /// <param name="request">Credenciais de acesso (e-mail e senha).</param>
    /// <response code="200">Autenticação bem-sucedida; retorna o token e os dados do usuário.</response>
    /// <response code="401">E-mail ou senha inválidos, ou usuário inativo.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.Login(request.Email, request.Senha);
        if (result == null) return Unauthorized(new { mensagem = "E-mail ou senha inválidos" });
        return Ok(result);
    }

    /// <summary>Retorna os dados do usuário autenticado no token atual.</summary>
    /// <response code="200">Dados do usuário autenticado.</response>
    /// <response code="401">Token ausente ou inválido.</response>
    /// <response code="404">Usuário do token não encontrado.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _usuarioRepo.ObterPorId(id);
        if (usuario == null) return NotFound();
        return Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email,
            usuario.Telefone, usuario.Perfil, usuario.Ativo, usuario.CriadoEm));
    }
}
