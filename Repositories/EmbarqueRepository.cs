using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class EmbarqueRepository : BaseRepository
{
    public EmbarqueRepository(IConfiguration config) : base(config) { }

    private const string SelectBase = @"
        SELECT e.*, md.nome AS municipio_destino_nome, md.uf AS municipio_destino_uf
        FROM embarques e
        LEFT JOIN municipios_destino md ON md.id = e.municipio_destino_id";

    public async Task<IEnumerable<Embarque>> ListarPorNegociacao(int negociacaoId)
    {
        using var conn = CreateConnection();
        var embarques = (await conn.QueryAsync<Embarque>(
            $"{SelectBase} WHERE e.negociacao_id = @NegociacaoId ORDER BY e.numero",
            new { NegociacaoId = negociacaoId })).ToList();

        foreach (var emb in embarques)
            emb.Itens = (await ObterItens(emb.Id, conn)).ToList();

        return embarques;
    }

    // Embarques ainda sem chegada confirmada — usado pela tela de Chegada (fazenda)
    // para selecionar a minuta, sem precisar saber o id da negociação de antemão.
    public async Task<IEnumerable<Embarque>> ListarPendentes()
    {
        using var conn = CreateConnection();
        var embarques = (await conn.QueryAsync<Embarque>(
            @"SELECT e.*, n.numero AS negociacao_numero, md.nome AS municipio_destino_nome, md.uf AS municipio_destino_uf
              FROM embarques e
              JOIN negociacoes n ON n.id = e.negociacao_id
              LEFT JOIN municipios_destino md ON md.id = e.municipio_destino_id
              WHERE e.chegada_confirmada_em IS NULL
              ORDER BY e.criado_em DESC")).ToList();

        foreach (var emb in embarques)
            emb.Itens = (await ObterItens(emb.Id, conn)).ToList();

        return embarques;
    }

    public async Task<Embarque?> ObterPorId(int id)
    {
        using var conn = CreateConnection();
        var emb = await conn.QueryFirstOrDefaultAsync<Embarque>($"{SelectBase} WHERE e.id = @Id", new { Id = id });
        if (emb != null)
            emb.Itens = (await ObterItens(id, conn)).ToList();
        return emb;
    }

    private async Task<IEnumerable<EmbarqueItem>> ObterItens(int embarqueId, System.Data.IDbConnection conn)
    {
        return await conn.QueryAsync<EmbarqueItem>(
            @"SELECT ei.*, np.produtor_origem, np.categoria_id, c.nome AS categoria_nome
              FROM embarque_itens ei
              JOIN negociacao_produtores np ON np.id = ei.negociacao_produtor_id
              JOIN categorias c ON c.id = np.categoria_id
              WHERE ei.embarque_id = @EmbarqueId ORDER BY c.ordem", new { EmbarqueId = embarqueId });
    }

    // Numeração atômica dentro da negociação: sem tabela de contador extra, o próprio
    // UPDATE trava a linha da negociação (mesmo espírito de negociacao_contadores).
    public async Task<int> Criar(Embarque emb)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var numero = await conn.ExecuteScalarAsync<int>(
            "UPDATE negociacoes SET embarques_ultimo_numero = embarques_ultimo_numero + 1 WHERE id=@Id RETURNING embarques_ultimo_numero",
            new { Id = emb.NegociacaoId }, tx);
        emb.Numero = numero;

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO embarques (negociacao_id, numero, produtor_origem, municipio_destino_id, data_embarque, nf, gta, criado_em, criado_por)
              VALUES (@NegociacaoId, @Numero, @ProdutorOrigem, @MunicipioDestinoId, @DataEmbarque, @Nf, @Gta, @CriadoEm, @CriadoPor)
              RETURNING id", emb, tx);
        emb.Id = id;

        foreach (var item in emb.Itens)
        {
            item.EmbarqueId = id;
            await conn.ExecuteAsync(
                @"INSERT INTO embarque_itens (embarque_id, negociacao_produtor_id, qtd_embarcada)
                  VALUES (@EmbarqueId, @NegociacaoProdutorId, @QtdEmbarcada)", item, tx);
        }

        tx.Commit();
        return id;
    }

    public async Task Atualizar(Embarque emb)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            @"UPDATE embarques SET municipio_destino_id=@MunicipioDestinoId, data_embarque=@DataEmbarque,
              nf=@Nf, gta=@Gta, atualizado_em=@AtualizadoEm WHERE id=@Id", emb, tx);

        await conn.ExecuteAsync("DELETE FROM embarque_itens WHERE embarque_id=@Id", new { emb.Id }, tx);
        foreach (var item in emb.Itens)
        {
            item.EmbarqueId = emb.Id;
            await conn.ExecuteAsync(
                @"INSERT INTO embarque_itens (embarque_id, negociacao_produtor_id, qtd_embarcada)
                  VALUES (@EmbarqueId, @NegociacaoProdutorId, @QtdEmbarcada)", item, tx);
        }

        tx.Commit();
    }

    public async Task Excluir(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM embarques WHERE id=@Id", new { Id = id });
    }

    // Atualiza só NF/GTA, sem passar pela trava de "não editar após chegada confirmada" —
    // são documentos que o ERS permite completar na tela de Conferência.
    public async Task AtualizarDocumentos(int id, string? nf, string? gta)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE embarques SET nf=@Nf, gta=@Gta, atualizado_em=@Agora WHERE id=@Id",
            new { Id = id, Nf = nf, Gta = gta, Agora = DateTime.Now });
    }

    // Soma embarcada de um lote em outros embarques (exceto o atual, se editando) —
    // usado para calcular o saldo disponível para embarcar.
    public async Task<int> SomaEmbarcadaPorLote(int negociacaoProdutorId, int? excluirEmbarqueId = null)
    {
        using var conn = CreateConnection();
        var sql = excluirEmbarqueId.HasValue
            ? "SELECT COALESCE(SUM(qtd_embarcada), 0) FROM embarque_itens WHERE negociacao_produtor_id=@LoteId AND embarque_id <> @ExcluirEmbarqueId"
            : "SELECT COALESCE(SUM(qtd_embarcada), 0) FROM embarque_itens WHERE negociacao_produtor_id=@LoteId";
        return await conn.ExecuteScalarAsync<int>(sql, new { LoteId = negociacaoProdutorId, ExcluirEmbarqueId = excluirEmbarqueId });
    }

    // Grava a chegada por item (categoria) e marca o cabeçalho do embarque como recebido.
    // Não trava por excedente: qtdChegou pode ultrapassar qtdEmbarcada.
    public async Task RegistrarChegada(int embarqueId, List<(int ItemId, int QtdChegou, decimal? PesoMedioEntrada, int AnimaisDebilitados)> itens,
        string? observacoesChegada, int usuarioId)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        foreach (var item in itens)
            await conn.ExecuteAsync(
                @"UPDATE embarque_itens SET qtd_chegou=@QtdChegou, peso_medio_entrada=@PesoMedioEntrada,
                  animais_debilitados=@AnimaisDebilitados WHERE id=@ItemId",
                new { item.ItemId, item.QtdChegou, item.PesoMedioEntrada, item.AnimaisDebilitados }, tx);

        await conn.ExecuteAsync(
            @"UPDATE embarques SET observacoes_chegada=@Obs, chegada_confirmada_em=@Agora, chegada_confirmada_por=@UsuarioId
              WHERE id=@Id", new { Id = embarqueId, Obs = observacoesChegada, Agora = DateTime.Now, UsuarioId = usuarioId }, tx);

        tx.Commit();
    }

    // Recalcula qtd_entregue/status_entrega de negociacao_itens a partir do SUM(embarque_itens.qtd_chegou),
    // para todas as categorias com lote na negociação.
    public async Task RecalcularEntregaPorNegociacao(int negociacaoId)
    {
        using var conn = CreateConnection();
        var categoriasAfetadas = await conn.QueryAsync<int>(
            "SELECT DISTINCT categoria_id FROM negociacao_produtores WHERE negociacao_id=@NegociacaoId",
            new { NegociacaoId = negociacaoId });

        foreach (var categoriaId in categoriasAfetadas)
        {
            var totalChegou = await conn.ExecuteScalarAsync<int>(
                @"SELECT COALESCE(SUM(ei.qtd_chegou), 0) FROM embarque_itens ei
                  JOIN negociacao_produtores np ON np.id = ei.negociacao_produtor_id
                  WHERE np.negociacao_id=@NegociacaoId AND np.categoria_id=@CategoriaId",
                new { NegociacaoId = negociacaoId, CategoriaId = categoriaId });

            var item = await conn.QueryFirstOrDefaultAsync<NegociacaoItem>(
                "SELECT * FROM negociacao_itens WHERE negociacao_id=@NegociacaoId AND categoria_id=@CategoriaId",
                new { NegociacaoId = negociacaoId, CategoriaId = categoriaId });
            if (item == null) continue;

            var statusEntrega = totalChegou <= 0 ? "Pendente"
                : (item.QtdNegociada.HasValue && totalChegou >= item.QtdNegociada.Value ? "Concluido" : "Parcial");

            await conn.ExecuteAsync(
                "UPDATE negociacao_itens SET qtd_entregue=@Qtd, status_entrega=@Status WHERE id=@Id",
                new { Qtd = totalChegou, Status = statusEntrega, Id = item.Id });
        }
    }

    // Saldo recebido total (soma de todas categorias): negociado - recebido. <= 0 quando 100% recebido.
    public async Task<int> SaldoRecebidoTotal(int negociacaoId)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"SELECT COALESCE(SUM(qtd_negociada), 0) - COALESCE(SUM(qtd_entregue), 0)
              FROM negociacao_itens WHERE negociacao_id=@NegociacaoId", new { NegociacaoId = negociacaoId });
    }

    // Verifica se esta é a primeira chegada confirmada da negociação (para disparar Fechado -> EmEntrega).
    public async Task<bool> EhPrimeiraChegada(int negociacaoId, int embarqueIdAtual)
    {
        using var conn = CreateConnection();
        var outrasConfirmadas = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM embarques
              WHERE negociacao_id=@NegociacaoId AND id<>@EmbarqueIdAtual AND chegada_confirmada_em IS NOT NULL",
            new { NegociacaoId = negociacaoId, EmbarqueIdAtual = embarqueIdAtual });
        return outrasConfirmadas == 0;
    }
}
