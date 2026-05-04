using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class MunicipioDestinoRepository : BaseRepository
{
    public MunicipioDestinoRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<MunicipioDestino>> Listar()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<MunicipioDestino>(
            "SELECT * FROM municipios_destino ORDER BY padrao DESC, nome");
    }

    public async Task<MunicipioDestino?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MunicipioDestino>(
            "SELECT * FROM municipios_destino WHERE id=@Id", new { Id = id });
    }

    public async Task<MunicipioDestino?> ObterPadrao()
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MunicipioDestino>(
            "SELECT * FROM municipios_destino WHERE padrao=true LIMIT 1");
    }

    public async Task<int> Criar(MunicipioDestino municipio)
    {
        using var conn = CreateConnection();
        if (municipio.Padrao)
            await conn.ExecuteAsync("UPDATE municipios_destino SET padrao=false");
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO municipios_destino (nome, uf, padrao) VALUES (@Nome, @Uf, @Padrao) RETURNING id", municipio);
    }

    public async Task Atualizar(MunicipioDestino municipio)
    {
        using var conn = CreateConnection();
        if (municipio.Padrao)
            await conn.ExecuteAsync("UPDATE municipios_destino SET padrao=false WHERE id != @Id", new { municipio.Id });
        await conn.ExecuteAsync(
            "UPDATE municipios_destino SET nome=@Nome, uf=@Uf, padrao=@Padrao WHERE id=@Id", municipio);
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM municipios_destino WHERE id=@Id", new { Id = id });
    }
}
