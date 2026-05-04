using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class CorretorRepository : BaseRepository
{
    public CorretorRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<Corretor>> Listar(bool? ativo = null)
    {
        using var conn = CreateConnection();
        var sql = "SELECT * FROM corretores";
        if (ativo.HasValue) sql += " WHERE ativo = @Ativo";
        sql += " ORDER BY nome";
        return await conn.QueryAsync<Corretor>(sql, new { Ativo = ativo });
    }

    public async Task<Corretor?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Corretor>(
            "SELECT * FROM corretores WHERE id=@Id", new { Id = id });
    }

    public async Task<int> Criar(Corretor corretor)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO corretores (nome, telefone, municipio, uf, propriedade, observacoes, ativo, criado_em)
              VALUES (@Nome, @Telefone, @Municipio, @Uf, @Propriedade, @Observacoes, @Ativo, @CriadoEm)
              RETURNING id", corretor);
    }

    public async Task Atualizar(Corretor corretor)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE corretores SET nome=@Nome, telefone=@Telefone, municipio=@Municipio,
              uf=@Uf, propriedade=@Propriedade, observacoes=@Observacoes, ativo=@Ativo
              WHERE id=@Id", corretor);
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM corretores WHERE id=@Id", new { Id = id });
    }
}
