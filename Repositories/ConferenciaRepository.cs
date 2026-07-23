using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class ConferenciaRepository : BaseRepository
{
    public ConferenciaRepository(IConfiguration config) : base(config) { }

    public async Task<EmbarqueConferencia?> ObterPorEmbarque(int embarqueId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<EmbarqueConferencia>(
            "SELECT * FROM embarque_conferencias WHERE embarque_id=@EmbarqueId", new { EmbarqueId = embarqueId });
    }

    public async Task<int> Criar(EmbarqueConferencia conf)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO embarque_conferencias (embarque_id, status, criado_em)
              VALUES (@EmbarqueId, @Status, @CriadoEm) RETURNING id", conf);
    }

    public async Task Salvar(EmbarqueConferencia conf)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE embarque_conferencias SET
              valor_total_negociacao=@ValorTotalNegociacao, valor_total_icms=@ValorTotalIcms,
              comissao_cb=@ComissaoCb, icms_cb=@IcmsCb, frete_cb=@FreteCb, despesa_cb=@DespesaCb,
              rs_cb=@RsCb, total_final_cb=@TotalFinalCb, rs_kg_negociacao=@RsKgNegociacao,
              rs_kg_colocado=@RsKgColocado, percentual_quebra_desvio=@PercentualQuebraDesvio,
              observacao_ocorrencias=@ObservacaoOcorrencias, status=@Status,
              finalizada_em=@FinalizadaEm, finalizada_por=@FinalizadaPor, atualizado_em=@AtualizadoEm
              WHERE id=@Id", conf);
    }
}
