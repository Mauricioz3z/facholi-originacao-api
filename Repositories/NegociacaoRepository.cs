using Dapper;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class NegociacaoRepository : BaseRepository
{
    public NegociacaoRepository(IConfiguration config) : base(config) { }

    public async Task<string> GerarNumero()
    {
        using var conn = CreateConnection();
        var ano = DateTime.Now.Year;

        var proximo = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO negociacao_contadores (ano, ultimo_numero)
              VALUES (@Ano, 1)
              ON CONFLICT (ano) DO UPDATE
              SET ultimo_numero = negociacao_contadores.ultimo_numero + 1
              RETURNING ultimo_numero",
            new { Ano = ano });

        return $"{proximo:D3}/{ano}";
    }

    public async Task<int> Criar(Negociacao neg)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO negociacoes (numero, comprador_id, corretor_id, municipio_origem_id,
              municipio_destino_id, data_prevista_entrega, status, criado_em)
              VALUES (@Numero, @CompradorId, @CorretorId, @MunicipioOrigemId,
              @MunicipioDestinoId, @DataPrevistaEntrega, @Status, @CriadoEm)
              RETURNING id", neg, tx);

        foreach (var item in neg.Itens)
        {
            item.NegociacaoId = id;
            await conn.ExecuteAsync(
                @"INSERT INTO negociacao_itens (negociacao_id, categoria_id, qtd_negociada, preco_negociado,
                  peso_medio, preco_colocado, qtd_entregue, status_entrega)
                  VALUES (@NegociacaoId, @CategoriaId, @QtdNegociada, @PrecoNegociado,
                  @PesoMedio, @PrecoColocado, @QtdEntregue, @StatusEntrega)", item, tx);
        }

        tx.Commit();
        return id;
    }

    public async Task Atualizar(Negociacao neg)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            @"UPDATE negociacoes SET comprador_id=@CompradorId, corretor_id=@CorretorId,
              municipio_origem_id=@MunicipioOrigemId, municipio_destino_id=@MunicipioDestinoId,
              data_prevista_entrega=@DataPrevistaEntrega, atualizado_em=@AtualizadoEm
              WHERE id=@Id", neg, tx);

        await conn.ExecuteAsync("DELETE FROM negociacao_itens WHERE negociacao_id=@Id", new { neg.Id }, tx);

        foreach (var item in neg.Itens)
        {
            item.NegociacaoId = neg.Id;
            await conn.ExecuteAsync(
                @"INSERT INTO negociacao_itens (negociacao_id, categoria_id, qtd_negociada, preco_negociado,
                  peso_medio, preco_colocado, qtd_entregue, status_entrega)
                  VALUES (@NegociacaoId, @CategoriaId, @QtdNegociada, @PrecoNegociado,
                  @PesoMedio, @PrecoColocado, @QtdEntregue, @StatusEntrega)", item, tx);
        }

        tx.Commit();
    }

    public async Task Fechar(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE negociacoes SET status='Fechado', data_fechamento=@DataFechamento WHERE id=@Id",
            new { Id = id, DataFechamento = DateTime.Now });
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM negociacoes WHERE id=@Id", new { Id = id });
    }

    public async Task AtualizarItemEntrega(int itemId, int qtdEntregue, string statusEntrega)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE negociacao_itens SET qtd_entregue=@QtdEntregue, status_entrega=@StatusEntrega WHERE id=@Id",
            new { Id = itemId, QtdEntregue = qtdEntregue, StatusEntrega = statusEntrega });
    }

    public async Task<Negociacao?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        var neg = await conn.QueryFirstOrDefaultAsync<Negociacao>(
            @"SELECT n.*, u.nome as comprador_nome, c.nome as corretor_nome,
              mo.nome as municipio_origem_nome, mo.uf as municipio_origem_uf,
              md.nome as municipio_destino_nome
              FROM negociacoes n
              LEFT JOIN usuarios u ON u.id = n.comprador_id
              LEFT JOIN corretores c ON c.id = n.corretor_id
              LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
              LEFT JOIN municipios_destino md ON md.id = n.municipio_destino_id
              WHERE n.id=@Id", new { Id = id });

        if (neg != null)
            neg.Itens = (await ObterItens(id, conn)).ToList();

        return neg;
    }

    public async Task<(IEnumerable<Negociacao> Items, int Total)> Listar(NegociacaoFiltroRequest filtro)
    {
        using var conn = CreateConnection();
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (filtro.CompradorId.HasValue) { where.Add("n.comprador_id=@CompradorId"); parameters.Add("CompradorId", filtro.CompradorId); }
        if (filtro.CorretorId.HasValue) { where.Add("n.corretor_id=@CorretorId"); parameters.Add("CorretorId", filtro.CorretorId); }
        if (!string.IsNullOrEmpty(filtro.Uf)) { where.Add("mo.uf=@Uf"); parameters.Add("Uf", filtro.Uf.ToUpper()); }
        if (!string.IsNullOrEmpty(filtro.CidadeOrigem)) { where.Add("LOWER(mo.nome) LIKE @Cidade"); parameters.Add("Cidade", $"%{filtro.CidadeOrigem.ToLower()}%"); }
        if (!string.IsNullOrEmpty(filtro.Status) && filtro.Status != "Todos") { where.Add("n.status=@Status"); parameters.Add("Status", filtro.Status); }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var offset = (filtro.Pagina - 1) * filtro.TamanhoPagina;
        parameters.Add("Limit", filtro.TamanhoPagina);
        parameters.Add("Offset", offset);

        var sqlBase = $@"FROM negociacoes n
            LEFT JOIN usuarios u ON u.id = n.comprador_id
            LEFT JOIN corretores c ON c.id = n.corretor_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            LEFT JOIN municipios_destino md ON md.id = n.municipio_destino_id
            {whereClause}";

        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) {sqlBase}", parameters);

        var negs = (await conn.QueryAsync<Negociacao>(
            $@"SELECT n.*, u.nome as comprador_nome, c.nome as corretor_nome,
              mo.nome as municipio_origem_nome, mo.uf as municipio_origem_uf,
              md.nome as municipio_destino_nome
              {sqlBase} ORDER BY n.criado_em DESC LIMIT @Limit OFFSET @Offset", parameters)).ToList();

        foreach (var neg in negs)
            neg.Itens = (await ObterItens(neg.Id, conn)).ToList();

        return (negs, total);
    }

    private async Task<IEnumerable<NegociacaoItem>> ObterItens(int negId, System.Data.IDbConnection conn)
    {
        return await conn.QueryAsync<NegociacaoItem>(
            @"SELECT ni.*, c.nome as categoria_nome, c.peso_min, c.peso_max
              FROM negociacao_itens ni
              JOIN categorias c ON c.id = ni.categoria_id
              WHERE ni.negociacao_id=@Id ORDER BY c.ordem", new { Id = negId });
    }
}
