using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class NegociacaoProdutorRepository : BaseRepository
{
    public NegociacaoProdutorRepository(IConfiguration config) : base(config) { }

    private const string SelectBase = @"
        SELECT np.*, c.nome AS categoria_nome,
          COALESCE((SELECT SUM(ei.qtd_embarcada) FROM embarque_itens ei WHERE ei.negociacao_produtor_id = np.id), 0) AS qtd_embarcada,
          COALESCE((SELECT SUM(ei.qtd_chegou) FROM embarque_itens ei WHERE ei.negociacao_produtor_id = np.id), 0) AS qtd_recebida
        FROM negociacao_produtores np
        JOIN categorias c ON c.id = np.categoria_id";

    public async Task<IEnumerable<NegociacaoProdutor>> ListarPorNegociacao(int negociacaoId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<NegociacaoProdutor>(
            $"{SelectBase} WHERE np.negociacao_id = @NegociacaoId ORDER BY np.produtor_origem, c.ordem",
            new { NegociacaoId = negociacaoId });
    }

    public async Task<NegociacaoProdutor?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<NegociacaoProdutor>(
            $"{SelectBase} WHERE np.id = @Id", new { Id = id });
    }

    // Busca por nome normalizado (trim + case-insensitive) — usado no find-or-create
    // ao criar um embarque sem lote pré-existente, pra não fragmentar "Jose"/"jose ".
    public async Task<NegociacaoProdutor?> BuscarPorProdutorCategoria(int negociacaoId, int categoriaId, string produtorOrigem)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<NegociacaoProdutor>(
            $@"{SelectBase} WHERE np.negociacao_id = @NegociacaoId AND np.categoria_id = @CategoriaId
               AND LOWER(TRIM(np.produtor_origem)) = LOWER(TRIM(@ProdutorOrigem))",
            new { NegociacaoId = negociacaoId, CategoriaId = categoriaId, ProdutorOrigem = produtorOrigem });
    }

    // Soma dos lotes já cadastrados para a categoria (exceto o próprio, se estiver editando).
    public async Task<int> SomaQtdPorCategoria(int negociacaoId, int categoriaId, int? excluirId = null)
    {
        using var conn = CreateConnection();
        var sql = excluirId.HasValue
            ? "SELECT COALESCE(SUM(qtd_cb), 0) FROM negociacao_produtores WHERE negociacao_id=@NegociacaoId AND categoria_id=@CategoriaId AND id <> @ExcluirId"
            : "SELECT COALESCE(SUM(qtd_cb), 0) FROM negociacao_produtores WHERE negociacao_id=@NegociacaoId AND categoria_id=@CategoriaId";
        return await conn.ExecuteScalarAsync<int>(sql, new { NegociacaoId = negociacaoId, CategoriaId = categoriaId, ExcluirId = excluirId });
    }

    public async Task<int> Criar(NegociacaoProdutor lote)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO negociacao_produtores (negociacao_id, categoria_id, produtor_origem, qtd_cb, observacoes, criado_em)
              VALUES (@NegociacaoId, @CategoriaId, @ProdutorOrigem, @QtdCb, @Observacoes, @CriadoEm)
              RETURNING id", lote);
    }

    public async Task Atualizar(NegociacaoProdutor lote)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE negociacao_produtores SET produtor_origem=@ProdutorOrigem, qtd_cb=@QtdCb,
              observacoes=@Observacoes, atualizado_em=@AtualizadoEm WHERE id=@Id", lote);
    }

    public async Task<int> ContarEmbarquesVinculados(int loteId)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM embarque_itens WHERE negociacao_produtor_id=@Id", new { Id = loteId });
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM negociacao_produtores WHERE id=@Id", new { Id = id });
    }
}
