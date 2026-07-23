using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Services;

public class ConferenciaService
{
    private readonly ConferenciaRepository _confRepo;
    private readonly EmbarqueRepository _embarqueRepo;
    private readonly NegociacaoRepository _negRepo;
    private readonly MunicipioOrigemRepository _munOrigemRepo;
    private readonly CategoriaRepository _catRepo;
    private readonly CalculoService _calculoService;
    private readonly AuditoriaRepository _auditoriaRepo;

    public ConferenciaService(
        ConferenciaRepository confRepo,
        EmbarqueRepository embarqueRepo,
        NegociacaoRepository negRepo,
        MunicipioOrigemRepository munOrigemRepo,
        CategoriaRepository catRepo,
        CalculoService calculoService,
        AuditoriaRepository auditoriaRepo)
    {
        _confRepo = confRepo;
        _embarqueRepo = embarqueRepo;
        _negRepo = negRepo;
        _munOrigemRepo = munOrigemRepo;
        _catRepo = catRepo;
        _calculoService = calculoService;
        _auditoriaRepo = auditoriaRepo;
    }

    public async Task<EmbarqueConferencia> ObterOuCriar(int embarqueId)
    {
        var existente = await _confRepo.ObterPorEmbarque(embarqueId);
        if (existente != null) return existente;

        var conf = new EmbarqueConferencia { EmbarqueId = embarqueId, Status = "EmAndamento", CriadoEm = DateTime.Now };
        conf.Id = await _confRepo.Criar(conf);
        return conf;
    }

    public async Task<EmbarqueConferencia> Salvar(int embarqueId, ConferenciaRequest request, int usuarioId, string usuarioNome)
    {
        var conf = await ObterOuCriar(embarqueId);
        if (conf.Status == "Finalizada")
            throw new Exception("Conferência já finalizada");

        conf.ValorTotalNegociacao = request.ValorTotalNegociacao;
        conf.ValorTotalIcms = request.ValorTotalIcms;
        conf.ComissaoCb = request.ComissaoCb;
        conf.IcmsCb = request.IcmsCb;
        conf.FreteCb = request.FreteCb;
        conf.DespesaCb = request.DespesaCb;
        conf.ObservacaoOcorrencias = request.ObservacaoOcorrencias;
        conf.AtualizadoEm = DateTime.Now;

        await CalcularCampos(conf, embarqueId);
        await _confRepo.Salvar(conf);

        return conf;
    }

    public async Task<EmbarqueConferencia> Finalizar(int embarqueId, int usuarioId, string usuarioNome)
    {
        var conf = await _confRepo.ObterPorEmbarque(embarqueId) ?? throw new Exception("Conferência não iniciada — salve os dados antes de finalizar");
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");
        if (!embarque.ChegadaConfirmadaEm.HasValue)
            throw new Exception("Não é possível finalizar a conferência antes da chegada ser confirmada");

        await CalcularCampos(conf, embarqueId);
        conf.Status = "Finalizada";
        conf.FinalizadaEm = DateTime.Now;
        conf.FinalizadaPor = usuarioId;
        await _confRepo.Salvar(conf);

        await _auditoriaRepo.Registrar("embarque_conferencias", conf.Id, "finalizacao", null, null,
            usuarioId, usuarioNome, $"Conferência do embarque {embarque.Numero} finalizada");

        return conf;
    }

    // Preenche os campos "Calculado" da conferência: R$/CB, Total final/CB, R$/KG negociação,
    // R$/KG colocado (reaproveitando CalculoService com o peso real de chegada) e % de desvio de peso.
    private async Task CalcularCampos(EmbarqueConferencia conf, int embarqueId)
    {
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");
        var neg = await _negRepo.ObterPorId(embarque.NegociacaoId) ?? throw new Exception("Negociação não encontrada");
        var municipioOrigem = await _munOrigemRepo.ObterPorId(neg.MunicipioOrigemId);

        var qtdTotal = embarque.Itens.Sum(i => i.QtdEmbarcada);
        conf.RsCb = qtdTotal > 0 && conf.ValorTotalNegociacao.HasValue
            ? Math.Round(conf.ValorTotalNegociacao.Value / qtdTotal, 2)
            : null;

        conf.TotalFinalCb = (conf.RsCb ?? 0) + (conf.ComissaoCb ?? 0) + (conf.IcmsCb ?? 0) + (conf.FreteCb ?? 0) + (conf.DespesaCb ?? 0);

        // Embarques de um só produtor podem ter mais de uma categoria; a apuração de
        // R$/kg e % de desvio usa a primeira categoria do embarque como referência
        // (o ERS não previu preços distintos por categoria nesta tela).
        var primeiroItem = embarque.Itens.FirstOrDefault();
        var itemNegociado = primeiroItem != null ? neg.Itens.FirstOrDefault(i => i.CategoriaId == primeiroItem.CategoriaId) : null;
        if (primeiroItem == null || itemNegociado?.PrecoNegociado == null)
            return;

        conf.RsKgNegociacao = itemNegociado.PrecoNegociado;

        var pesoEntrada = primeiroItem.PesoMedioEntrada;
        if (pesoEntrada.HasValue && pesoEntrada.Value > 0 && municipioOrigem != null)
        {
            var categoria = await _catRepo.ObterPorId(primeiroItem.CategoriaId);
            if (categoria != null)
            {
                var resultado = await _calculoService.CalcularColocado(itemNegociado.PrecoNegociado.Value, municipioOrigem, categoria, pesoEntrada);
                conf.RsKgColocado = resultado.PrecoColocado;
            }
        }

        if (itemNegociado.PesoMedio.HasValue && itemNegociado.PesoMedio.Value > 0 && pesoEntrada.HasValue)
            conf.PercentualQuebraDesvio = Math.Round((pesoEntrada.Value - itemNegociado.PesoMedio.Value) / itemNegociado.PesoMedio.Value * 100, 2);
    }
}
