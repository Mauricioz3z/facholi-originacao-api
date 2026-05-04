namespace PrecoBoi.Api.DTOs;

public record LoginRequest(string Email, string Senha);

public record LoginResponse(string Token, string Nome, string Email, string Perfil, int Id);

public record UsuarioRequest(string Nome, string Email, string Senha, string Telefone, string Perfil, bool Ativo);

public record UsuarioUpdateRequest(string Nome, string Email, string? Senha, string Telefone, string Perfil, bool Ativo);

public record UsuarioResponse(int Id, string Nome, string Email, string Telefone, string Perfil, bool Ativo, DateTime CriadoEm);
