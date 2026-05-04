using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class UsuarioRepository : BaseRepository
{
    public UsuarioRepository(IConfiguration config) : base(config) { }

    public async Task<Usuario?> ObterPorEmail(string email)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Usuario>(
            "SELECT * FROM usuarios WHERE email = @Email", new { Email = email });
    }

    public async Task<Usuario?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Usuario>(
            "SELECT * FROM usuarios WHERE id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Usuario>> Listar(bool? ativo = null)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, nome, email, telefone, perfil, ativo, criado_em FROM usuarios";
        if (ativo.HasValue) sql += " WHERE ativo = @Ativo";
        sql += " ORDER BY nome";
        return await conn.QueryAsync<Usuario>(sql, new { Ativo = ativo });
    }

    public async Task<int> Criar(Usuario usuario)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO usuarios (nome, email, senha_hash, telefone, perfil, ativo, criado_em)
              VALUES (@Nome, @Email, @SenhaHash, @Telefone, @Perfil, @Ativo, @CriadoEm)
              RETURNING id", usuario);
    }

    public async Task Atualizar(Usuario usuario)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE usuarios SET nome=@Nome, email=@Email, telefone=@Telefone,
              perfil=@Perfil, ativo=@Ativo WHERE id=@Id", usuario);
    }

    public async Task AtualizarSenha(int id, string senhaHash)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE usuarios SET senha_hash=@SenhaHash WHERE id=@Id",
            new { Id = id, SenhaHash = senhaHash });
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM usuarios WHERE id=@Id", new { Id = id });
    }
}
