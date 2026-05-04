using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class IcmsRepository : BaseRepository
{
    public IcmsRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<Icms>> Listar()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<Icms>("SELECT * FROM icms ORDER BY uf");
    }

    public async Task<Icms?> ObterPorUf(string uf)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Icms>(
            "SELECT * FROM icms WHERE uf=@Uf", new { Uf = uf.ToUpper() });
    }

    public async Task Atualizar(Icms icms)
    {
        icms.IcmsEfetivo = icms.Aliquota / 100m * (1m - icms.Recuperacao / 100m);
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE icms SET aliquota=@Aliquota, recuperacao=@Recuperacao, icms_efetivo=@IcmsEfetivo
              WHERE uf=@Uf", icms);
    }
}
