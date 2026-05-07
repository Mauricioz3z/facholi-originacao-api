using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class CotacaoRegionalRepository : BaseRepository
{
    public CotacaoRegionalRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<CotacaoRegional>> Listar()
    {
        using var conn = CreateConnection();
        var cotacoes = (await conn.QueryAsync<CotacaoRegional>(
            "SELECT * FROM cotacoes_regionais ORDER BY uf")).ToList();

        foreach (var cotacao in cotacoes)
            cotacao.Agios = (await ObterAgios(cotacao.Id, conn)).ToList();

        return cotacoes;
    }

    public async Task<CotacaoRegional?> ObterPorUf(string uf)
    {
        using var conn = CreateConnection();
        var cotacao = await conn.QueryFirstOrDefaultAsync<CotacaoRegional>(
            "SELECT * FROM cotacoes_regionais WHERE uf=@Uf", new { Uf = uf.ToUpper() });
        if (cotacao != null)
            cotacao.Agios = (await ObterAgios(cotacao.Id, conn)).ToList();
        return cotacao;
    }

    public async Task<CotacaoRegional?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        var cotacao = await conn.QueryFirstOrDefaultAsync<CotacaoRegional>(
            "SELECT * FROM cotacoes_regionais WHERE id=@Id", new { Id = id });
        if (cotacao != null)
            cotacao.Agios = (await ObterAgios(id, conn)).ToList();
        return cotacao;
    }

    private async Task<IEnumerable<AgioCotacao>> ObterAgios(int cotacaoId, System.Data.IDbConnection conn)
    {
        return await conn.QueryAsync<AgioCotacao>(
            @"SELECT a.*, c.nome as categoria_nome, c.peso_min, c.peso_max
              FROM agios_cotacao a
              JOIN categorias c ON c.id = a.categoria_id
              WHERE a.cotacao_regional_id=@Id ORDER BY c.ordem",
            new { Id = cotacaoId });
    }

    public async Task Salvar(CotacaoRegional cotacao)
    {
        using var conn = CreateConnection();
        var existente = await conn.QueryFirstOrDefaultAsync<CotacaoRegional>(
            "SELECT id FROM cotacoes_regionais WHERE uf=@Uf", new { cotacao.Uf });

        int id;
        if (existente == null)
        {
            id = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO cotacoes_regionais (uf, praca_referencia_uf, valor_arroba, atualizado_em)
                  VALUES (@Uf, @PracaReferenciaUf, @ValorArroba, @AtualizadoEm) RETURNING id",
                new { cotacao.Uf, cotacao.PracaReferenciaUf, cotacao.ValorArroba, AtualizadoEm = DateTime.Now });
        }
        else
        {
            id = existente.Id;
            await conn.ExecuteAsync(
                @"UPDATE cotacoes_regionais SET praca_referencia_uf=@PracaReferenciaUf,
                  valor_arroba=@ValorArroba, atualizado_em=@AtualizadoEm WHERE id=@Id",
                new { cotacao.PracaReferenciaUf, cotacao.ValorArroba, AtualizadoEm = DateTime.Now, Id = id });
        }

        await conn.ExecuteAsync("DELETE FROM agios_cotacao WHERE cotacao_regional_id=@Id", new { Id = id });
        foreach (var agio in cotacao.Agios)
        {
            await conn.ExecuteAsync(
                "INSERT INTO agios_cotacao (cotacao_regional_id, categoria_id, percentual) VALUES (@Id, @CategoriaId, @Percentual)",
                new { Id = id, agio.CategoriaId, agio.Percentual });
        }
    }
}
