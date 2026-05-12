using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class CategoriaRepository : BaseRepository
{
    public CategoriaRepository(IConfiguration config) : base(config) { }

    public async Task<IEnumerable<Categoria>> Listar()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<Categoria>(
            "SELECT * FROM categorias ORDER BY ordem");
    }

    public async Task<Categoria?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Categoria>(
            "SELECT * FROM categorias WHERE id=@Id", new { Id = id });
    }

    public async Task Atualizar(Categoria categoria)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE categorias SET nome=@Nome, peso_min=@PesoMin, peso_max=@PesoMax,
              peso_medio=@PesoMedio, cab_caminhao=@CabCaminhao, ordem=@Ordem WHERE id=@Id",
            categoria);
    }

    public async Task<int> Criar(Categoria categoria, decimal agioPadrao)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO categorias (nome, peso_min, peso_max, peso_medio, cab_caminhao, ordem)
              VALUES (@Nome, @PesoMin, @PesoMax, @PesoMedio, @CabCaminhao, @Ordem)
              RETURNING id", categoria, tx);

        await conn.ExecuteAsync(
            @"INSERT INTO agios_cotacao (cotacao_regional_id, categoria_id, percentual)
              SELECT id, @CategoriaId, @Percentual FROM cotacoes_regionais",
            new { CategoriaId = id, Percentual = agioPadrao }, tx);

        tx.Commit();
        return id;
    }

    public async Task<int> ContarUsoEmNegociacoes(int id)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM negociacao_itens WHERE categoria_id=@Id", new { Id = id });
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("DELETE FROM agios_cotacao WHERE categoria_id=@Id", new { Id = id }, tx);
        await conn.ExecuteAsync("DELETE FROM categorias WHERE id=@Id", new { Id = id }, tx);

        tx.Commit();
    }
}
