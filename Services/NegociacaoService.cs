using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Services;

public class NegociacaoService
{
    private readonly NegociacaoRepository _negRepo;
    private readonly CategoriaRepository _catRepo;
    private readonly MunicipioOrigemRepository _munOrigemRepo;
    private readonly CalculoService _calculoService;
    private readonly AuditoriaRepository _auditoriaRepo;
    private readonly NegociacaoProdutorRepository _produtorRepo;

    public NegociacaoService(
        NegociacaoRepository negRepo,
        CategoriaRepository catRepo,
        MunicipioOrigemRepository munOrigemRepo,
        CalculoService calculoService,
        AuditoriaRepository auditoriaRepo,
        NegociacaoProdutorRepository produtorRepo)
    {
        _negRepo = negRepo;
        _catRepo = catRepo;
        _munOrigemRepo = munOrigemRepo;
        _calculoService = calculoService;
        _auditoriaRepo = auditoriaRepo;
        _produtorRepo = produtorRepo;
    }

    public async Task<Negociacao> Criar(NegociacaoRequest request, int usuarioId, string usuarioNome)
    {
        ValidarQuantidades(request.Itens);

        var numero = await _negRepo.GerarNumero();
        var municipioOrigem = await _munOrigemRepo.ObterPorId(request.MunicipioOrigemId)
            ?? throw new Exception("Município de origem não encontrado");

        var negociacao = new Negociacao
        {
            Numero = numero,
            CompradorId = request.CompradorId,
            CorretorId = request.CorretorId,
            MunicipioOrigemId = request.MunicipioOrigemId,
            MunicipioDestinoId = request.MunicipioDestinoId,
            DataPrevistaEntrega = request.DataPrevistaEntrega,
            Observacoes = TruncarObservacoes(request.Observacoes),
            TipoNegocio = request.TipoNegocio == "Perna" ? "Perna" : "KG",
            Status = "EmNegociacao",
            CriadoEm = DateTime.Now
        };

        negociacao.Itens = await CalcularItens(request.Itens, municipioOrigem);
        var id = await _negRepo.Criar(negociacao);
        negociacao.Id = id;

        await _auditoriaRepo.Registrar("negociacoes", id, "criacao", null, numero, usuarioId, usuarioNome,
            $"Negociação {numero} criada");

        return await _negRepo.ObterPorId(id) ?? negociacao;
    }

    public async Task<Negociacao> Atualizar(int id, NegociacaoRequest request, int usuarioId, string usuarioNome, string usuarioPerfil)
    {
        ValidarQuantidades(request.Itens);

        var negExistente = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        // Admin pode editar qualquer dado de qualquer negociação, inclusive fechadas.
        // Demais perfis continuam bloqueados em negociações fechadas e só editam as próprias.
        var ehAdmin = usuarioPerfil == "Admin";

        if (negExistente.Status != "EmNegociacao" && !ehAdmin)
            throw new Exception("Negociação fechada não pode ser editada");

        if (!ehAdmin && negExistente.CompradorId != usuarioId)
            throw new UnauthorizedAccessException("Você só pode editar negociações que criou.");

        var municipioOrigem = await _munOrigemRepo.ObterPorId(request.MunicipioOrigemId)
            ?? throw new Exception("Município de origem não encontrado");

        // Guardar itens anteriores (já carregados com CategoriaNome via JOIN)
        var itensAnteriores = negExistente.Itens.ToList();

        // Auditar alterações no cabeçalho
        if (negExistente.CompradorId != request.CompradorId)
            await _auditoriaRepo.Registrar("negociacoes", id, "comprador_id",
                negExistente.CompradorId.ToString(), request.CompradorId.ToString(),
                usuarioId, usuarioNome, $"Comprador alterado na negociação {negExistente.Numero}");

        if (negExistente.CorretorId != request.CorretorId)
            await _auditoriaRepo.Registrar("negociacoes", id, "corretor_id",
                negExistente.CorretorId.ToString(), request.CorretorId.ToString(),
                usuarioId, usuarioNome, $"Corretor alterado na negociação {negExistente.Numero}");

        if (negExistente.MunicipioOrigemId != request.MunicipioOrigemId)
            await _auditoriaRepo.Registrar("negociacoes", id, "municipio_origem_id",
                negExistente.MunicipioOrigemId.ToString(), request.MunicipioOrigemId.ToString(),
                usuarioId, usuarioNome, $"Município de origem alterado na negociação {negExistente.Numero}");

        if (negExistente.MunicipioDestinoId != request.MunicipioDestinoId)
            await _auditoriaRepo.Registrar("negociacoes", id, "municipio_destino_id",
                negExistente.MunicipioDestinoId.ToString(), request.MunicipioDestinoId.ToString(),
                usuarioId, usuarioNome, $"Município de destino alterado na negociação {negExistente.Numero}");

        if (negExistente.DataPrevistaEntrega != request.DataPrevistaEntrega)
            await _auditoriaRepo.Registrar("negociacoes", id, "data_prevista_entrega",
                negExistente.DataPrevistaEntrega?.ToString("yyyy-MM-dd"), request.DataPrevistaEntrega?.ToString("yyyy-MM-dd"),
                usuarioId, usuarioNome, $"Data prevista de entrega alterada na negociação {negExistente.Numero}");

        var observacoesNovas = TruncarObservacoes(request.Observacoes);
        if (negExistente.Observacoes != observacoesNovas)
            await _auditoriaRepo.Registrar("negociacoes", id, "observacoes",
                negExistente.Observacoes, observacoesNovas,
                usuarioId, usuarioNome, $"Observações alteradas na negociação {negExistente.Numero}");

        negExistente.CompradorId = request.CompradorId;
        negExistente.CorretorId = request.CorretorId;
        negExistente.MunicipioOrigemId = request.MunicipioOrigemId;
        negExistente.MunicipioDestinoId = request.MunicipioDestinoId;
        negExistente.DataPrevistaEntrega = request.DataPrevistaEntrega;
        negExistente.Observacoes = observacoesNovas;
        negExistente.TipoNegocio = request.TipoNegocio == "Perna" ? "Perna" : "KG";
        negExistente.AtualizadoEm = DateTime.Now;
        negExistente.Itens = await CalcularItens(request.Itens, municipioOrigem);

        // Impede reduzir qtd_negociada abaixo do que já foi comprometido em lotes/produtores
        // (embarques puxam do lote, e o lote não pode ultrapassar a qtd negociada da categoria).
        foreach (var itemNovo in negExistente.Itens)
        {
            var somaLotes = await _produtorRepo.SomaQtdPorCategoria(id, itemNovo.CategoriaId);
            if (somaLotes > 0 && (itemNovo.QtdNegociada ?? 0) < somaLotes)
            {
                var nomeCategoria = itensAnteriores.FirstOrDefault(a => a.CategoriaId == itemNovo.CategoriaId)?.CategoriaNome
                    ?? $"categoria {itemNovo.CategoriaId}";
                throw new Exception(
                    $"Não é possível reduzir a quantidade de {nomeCategoria} para {itemNovo.QtdNegociada}: " +
                    $"já há {somaLotes} CB comprometidos em lotes por produtor.");
            }
        }

        // Preserva entregas já registradas: ao recalcular, os itens são reinseridos
        // zerados. Mantém qtd. entregue/status das categorias que continuam na negociação
        // (relevante quando um Admin edita uma negociação fechada que já teve entregas).
        foreach (var item in negExistente.Itens)
        {
            var ant = itensAnteriores.FirstOrDefault(i => i.CategoriaId == item.CategoriaId);
            if (ant == null) continue;
            item.QtdEntregue = ant.QtdEntregue;
            item.StatusEntrega = CalcularStatusEntrega(ant.QtdEntregue, item.QtdNegociada);
        }

        // Auditar alterações nos itens (comparando pelo categoriaId)
        foreach (var itemNovo in negExistente.Itens)
        {
            var ant = itensAnteriores.FirstOrDefault(i => i.CategoriaId == itemNovo.CategoriaId);
            if (ant == null)
            {
                // Item adicionado
                await _auditoriaRepo.Registrar("negociacao_itens", id, "preco_negociado",
                    null, itemNovo.PrecoNegociado?.ToString("F4"),
                    usuarioId, usuarioNome,
                    $"Categoria ID {itemNovo.CategoriaId} adicionada na negociação {negExistente.Numero}");
                continue;
            }

            if (ant.PrecoNegociado != itemNovo.PrecoNegociado)
                await _auditoriaRepo.Registrar("negociacao_itens", id, "preco_negociado",
                    ant.PrecoNegociado?.ToString("F4"), itemNovo.PrecoNegociado?.ToString("F4"),
                    usuarioId, usuarioNome,
                    $"R$/kg Praça alterado em {ant.CategoriaNome} — neg. {negExistente.Numero}");

            if (ant.QtdNegociada != itemNovo.QtdNegociada)
                await _auditoriaRepo.Registrar("negociacao_itens", id, "qtd_negociada",
                    ant.QtdNegociada?.ToString(), itemNovo.QtdNegociada?.ToString(),
                    usuarioId, usuarioNome,
                    $"Qtd. negociada alterada em {ant.CategoriaNome} — neg. {negExistente.Numero}");

            if (ant.PesoMedio != itemNovo.PesoMedio)
                await _auditoriaRepo.Registrar("negociacao_itens", id, "peso_medio",
                    ant.PesoMedio?.ToString("F2"), itemNovo.PesoMedio?.ToString("F2"),
                    usuarioId, usuarioNome,
                    $"Peso médio alterado em {ant.CategoriaNome} — neg. {negExistente.Numero}");
        }

        // Auditar itens removidos
        foreach (var ant in itensAnteriores.Where(a => !negExistente.Itens.Any(n => n.CategoriaId == a.CategoriaId)))
            await _auditoriaRepo.Registrar("negociacao_itens", id, "preco_negociado",
                ant.PrecoNegociado?.ToString("F4"), null,
                usuarioId, usuarioNome,
                $"Categoria {ant.CategoriaNome} removida da negociação {negExistente.Numero}");

        await _negRepo.Atualizar(negExistente);
        await _auditoriaRepo.Registrar("negociacoes", id, "edicao", null, null, usuarioId, usuarioNome,
            $"Negociação {negExistente.Numero} atualizada");

        return await _negRepo.ObterPorId(id) ?? negExistente;
    }

    public async Task Excluir(int id, int usuarioId, string usuarioNome, string usuarioPerfil)
    {
        var neg = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (usuarioPerfil != "Admin" && neg.CompradorId != usuarioId)
            throw new UnauthorizedAccessException("Você só pode excluir negociações que criou.");

        await _auditoriaRepo.Registrar("negociacoes", id, "exclusao", neg.Numero, null,
            usuarioId, usuarioNome, $"Negociação {neg.Numero} excluída (status: {neg.Status})");

        await _negRepo.Excluir(id);
    }

    public async Task Fechar(int id, int usuarioId, string usuarioNome)
    {
        var neg = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (neg.Status != "EmNegociacao")
            throw new Exception("Negociação já está fechada");

        await _negRepo.Fechar(id);
        await _auditoriaRepo.Registrar("negociacoes", id, "fechamento", "EmNegociacao", "Fechado",
            usuarioId, usuarioNome, $"Negociação {neg.Numero} fechada");
    }

    // Status válidos do novo ciclo (Fase 2).
    private static readonly string[] StatusValidos = { "EmNegociacao", "Fechado", "EmEntrega", "Concluido" };

    // Alteração manual de status pelo Master: reabertura, ajuste de "Em entrega",
    // ou conclusão forçada abaixo de 100% (ex.: 49 de 50). Toda mudança é auditada.
    public async Task AlterarStatus(int id, string novoStatus, string? motivo, int usuarioId, string usuarioNome)
    {
        if (!StatusValidos.Contains(novoStatus))
            throw new Exception($"Status inválido: {novoStatus}");

        var neg = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (neg.Status == novoStatus) return;

        await _negRepo.AlterarStatus(id, novoStatus);
        await _auditoriaRepo.Registrar("negociacoes", id, "status", neg.Status, novoStatus,
            usuarioId, usuarioNome,
            motivo != null
                ? $"Status da negociação {neg.Numero} alterado manualmente: {motivo}"
                : $"Status da negociação {neg.Numero} alterado manualmente de {neg.Status} para {novoStatus}");
    }

    // Automático: chamado por EmbarqueService ao registrar chegada. Não sobrescreve
    // uma mudança manual mais avançada (ex.: já Concluido) e não regride status.
    public async Task AvancarStatusAutomatico(int id, string statusSugerido, int usuarioId, string usuarioNome)
    {
        var ordem = new Dictionary<string, int> { ["EmNegociacao"] = 0, ["Fechado"] = 1, ["EmEntrega"] = 2, ["Concluido"] = 3 };
        var neg = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (!ordem.TryGetValue(neg.Status, out var atual) || !ordem.TryGetValue(statusSugerido, out var sugerido))
            return;
        if (sugerido <= atual) return;

        await _negRepo.AlterarStatus(id, statusSugerido);
        await _auditoriaRepo.Registrar("negociacoes", id, "status", neg.Status, statusSugerido,
            usuarioId, usuarioNome, $"Status da negociação {neg.Numero} avançou automaticamente para {statusSugerido}");
    }

    public async Task AlterarComissaoPaga(int id, bool paga, int usuarioId, string usuarioNome)
    {
        var neg = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (neg.ComissaoPaga == paga) return;

        await _negRepo.AlterarComissaoPaga(id, paga, usuarioId);
        await _auditoriaRepo.Registrar("negociacoes", id, "comissao_paga", neg.ComissaoPaga.ToString(), paga.ToString(),
            usuarioId, usuarioNome, $"Comissão da negociação {neg.Numero} marcada como {(paga ? "paga" : "não paga")}");
    }

    // Impede salvar negociação sem animais: pelo menos uma categoria
    // precisa ter quantidade de cabeças preenchida e maior que zero.
    private static void ValidarQuantidades(List<NegociacaoItemRequest> itens)
    {
        var temAnimais = itens?.Any(i => i.QtdNegociada.HasValue && i.QtdNegociada.Value > 0) ?? false;
        if (!temAnimais)
            throw new Exception("Informe a quantidade de cabeças em pelo menos uma categoria. Não é possível salvar uma negociação com zero animais.");
    }

    // Deriva o status de entrega de um item a partir da qtd. entregue vs. negociada.
    private static string CalcularStatusEntrega(int qtdEntregue, int? qtdNegociada)
    {
        if (qtdEntregue <= 0) return "Pendente";
        if (qtdNegociada.HasValue && qtdEntregue >= qtdNegociada.Value) return "Concluido";
        return "Parcial";
    }

    private static string? TruncarObservacoes(string? obs)
    {
        if (string.IsNullOrWhiteSpace(obs)) return null;
        obs = obs.Trim();
        return obs.Length > 500 ? obs[..500] : obs;
    }

    private async Task<List<NegociacaoItem>> CalcularItens(List<NegociacaoItemRequest> itensReq, MunicipioOrigem municipioOrigem)
    {
        var itens = new List<NegociacaoItem>();
        foreach (var itemReq in itensReq)
        {
            var categoria = await _catRepo.ObterPorId(itemReq.CategoriaId)
                ?? throw new Exception($"Categoria {itemReq.CategoriaId} não encontrada");

            decimal? precoColocado = null;
            if (itemReq.PrecoNegociado.HasValue && itemReq.PrecoNegociado > 0)
            {
                var catCalculo = itemReq.PesoMedio.HasValue
                    ? new Categoria { Id = categoria.Id, Nome = categoria.Nome, PesoMin = categoria.PesoMin, PesoMax = categoria.PesoMax, PesoMedio = itemReq.PesoMedio.Value, CabCaminhao = categoria.CabCaminhao, Ordem = categoria.Ordem }
                    : categoria;
                var resultado = await _calculoService.CalcularColocado(itemReq.PrecoNegociado.Value, municipioOrigem, catCalculo);
                precoColocado = resultado.PrecoColocado;
            }

            itens.Add(new NegociacaoItem
            {
                CategoriaId = itemReq.CategoriaId,
                QtdNegociada = itemReq.QtdNegociada,
                PrecoNegociado = itemReq.PrecoNegociado,
                PesoMedio = itemReq.PesoMedio ?? categoria.PesoMedio,
                PrecoColocado = precoColocado,
                QtdEntregue = 0,
                StatusEntrega = "Pendente"
            });
        }
        return itens;
    }
}
