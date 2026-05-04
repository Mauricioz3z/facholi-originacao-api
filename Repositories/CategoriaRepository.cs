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
}
