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

    public NegociacaoService(
        NegociacaoRepository negRepo,
        CategoriaRepository catRepo,
        MunicipioOrigemRepository munOrigemRepo,
        CalculoService calculoService,
        AuditoriaRepository auditoriaRepo)
    {
        _negRepo = negRepo;
        _catRepo = catRepo;
        _munOrigemRepo = munOrigemRepo;
        _calculoService = calculoService;
        _auditoriaRepo = auditoriaRepo;
    }

    public async Task<Negociacao> Criar(NegociacaoRequest request, int usuarioId, string usuarioNome)
    {
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
        var negExistente = await _negRepo.ObterPorId(id)
            ?? throw new Exception("Negociação não encontrada");

        if (negExistente.Status == "Fechado")
            throw new Exception("Negociação fechada não pode ser editada");

        if (usuarioPerfil != "Admin" && negExistente.CompradorId != usuarioId)
            throw new UnauthorizedAccessException("Você só pode editar negociações que criou.");

        var municipioOrigem = await _munOrigemRepo.ObterPorId(request.MunicipioOrigemId)
            ?? throw new Exception("Município de origem não encontrado");

        // Guardar itens anteriores (já carregados com CategoriaNome via JOIN)
        var itensAnteriores = negExistente.Itens.ToList();

        // Auditar alterações no cabeçalho
        if (negExistente.CorretorId != request.CorretorId)
            await _auditoriaRepo.Registrar("negociacoes", id, "corretor_id",
                negExistente.CorretorId.ToString(), request.CorretorId.ToString(),
                usuarioId, usuarioNome, $"Corretor alterado na negociação {negExistente.Numero}");

        if (negExistente.MunicipioOrigemId != request.MunicipioOrigemId)
            await _auditoriaRepo.Registrar("negociacoes", id, "municipio_origem_id",
                negExistente.MunicipioOrigemId.ToString(), request.MunicipioOrigemId.ToString(),
                usuarioId, usuarioNome, $"Município de origem alterado na negociação {negExistente.Numero}");

        negExistente.CompradorId = request.CompradorId;
        negExistente.CorretorId = request.CorretorId;
        negExistente.MunicipioOrigemId = request.MunicipioOrigemId;
        negExistente.MunicipioDestinoId = request.MunicipioDestinoId;
        negExistente.DataPrevistaEntrega = request.DataPrevistaEntrega;
        negExistente.AtualizadoEm = DateTime.Now;
        negExistente.Itens = await CalcularItens(request.Itens, municipioOrigem);

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

        if (neg.Status == "Fechado")
            throw new Exception("Negociação já está fechada");

        await _negRepo.Fechar(id);
        await _auditoriaRepo.Registrar("negociacoes", id, "fechamento", "EmNegociacao", "Fechado",
            usuarioId, usuarioNome, $"Negociação {neg.Numero} fechada");
    }

    public async Task AtualizarEntrega(EntregaRequest request, int usuarioId, string usuarioNome)
    {
        var neg = await _negRepo.ObterPorId(request.NegociacaoId)
            ?? throw new Exception("Negociação não encontrada");

        if (neg.Status != "Fechado")
            throw new Exception("Controle de entrega disponível apenas para negociações fechadas");

        foreach (var itemReq in request.Itens)
        {
            var item = neg.Itens.FirstOrDefault(i => i.Id == itemReq.ItemId)
                ?? throw new Exception($"Item {itemReq.ItemId} não encontrado");

            if (itemReq.QtdEntregue < 0)
                throw new Exception("Quantidade entregue não pode ser negativa");

            string statusEntrega;
            if (itemReq.QtdEntregue == 0) statusEntrega = "Pendente";
            else if (item.QtdNegociada.HasValue && itemReq.QtdEntregue >= item.QtdNegociada.Value) statusEntrega = "Concluido";
            else statusEntrega = "Parcial";

            await _negRepo.AtualizarItemEntrega(itemReq.ItemId, itemReq.QtdEntregue, statusEntrega);
        }

        await _auditoriaRepo.Registrar("negociacoes", request.NegociacaoId, "entrega", null, null,
            usuarioId, usuarioNome, $"Entrega atualizada para negociação {neg.Numero}");
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
