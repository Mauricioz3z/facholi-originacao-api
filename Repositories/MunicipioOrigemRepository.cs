using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class MunicipioOrigemRepository : BaseRepository
{
    public MunicipioOrigemRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<MunicipioOrigem>> Listar(bool? ativo = null)
    {
        using var conn = CreateConnection();
        var sql = "SELECT * FROM municipios_origem";
        if (ativo.HasValue) sql += " WHERE ativo = @Ativo";
        sql += " ORDER BY uf, nome";
        return await conn.QueryAsync<MunicipioOrigem>(sql, new { Ativo = ativo });
    }

    public async Task<MunicipioOrigem?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MunicipioOrigem>(
            "SELECT * FROM municipios_origem WHERE id=@Id", new { Id = id });
    }

    public async Task<int> Criar(MunicipioOrigem municipio)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO municipios_origem (nome, uf, distancia_km, valor_km, ativo)
              VALUES (@Nome, @Uf, @DistanciaKm, @ValorKm, @Ativo) RETURNING id", municipio);
    }

    public async Task Atualizar(MunicipioOrigem municipio)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE municipios_origem SET nome=@Nome, uf=@Uf, distancia_km=@DistanciaKm,
              valor_km=@ValorKm, ativo=@Ativo WHERE id=@Id", municipio);
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM municipios_origem WHERE id=@Id", new { Id = id });
    }
}
