using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class ConfigComissaoRepository : BaseRepository
{
    public ConfigComissaoRepository(IConfiguration config) : base(config) { }

    public async Task<ConfigComissao?> Obter()
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ConfigComissao>(
            "SELECT * FROM config_comissao LIMIT 1");
    }

    public async Task Salvar(ConfigComissao config)
    {
        using var conn = CreateConnection();
        var existente = await conn.QueryFirstOrDefaultAsync<int?>("SELECT id FROM config_comissao LIMIT 1");
        if (existente == null)
            await conn.ExecuteAsync(
                "INSERT INTO config_comissao (percentual, ativo) VALUES (@Percentual, @Ativo)", config);
        else
            await conn.ExecuteAsync(
                "UPDATE config_comissao SET percentual=@Percentual, ativo=@Ativo WHERE id=@Id",
                new { config.Percentual, config.Ativo, Id = existente });
    }
}
