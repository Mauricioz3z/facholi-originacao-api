using Npgsql;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Services;

public class EmbarqueService
{
    private readonly EmbarqueRepository _embarqueRepo;
    private readonly NegociacaoRepository _negRepo;
    private readonly NegociacaoProdutorRepository _produtorRepo;
    private readonly AuditoriaRepository _auditoriaRepo;
    private readonly NegociacaoService _negociacaoService;

    public EmbarqueService(
        EmbarqueRepository embarqueRepo,
        NegociacaoRepository negRepo,
        NegociacaoProdutorRepository produtorRepo,
        AuditoriaRepository auditoriaRepo,
        NegociacaoService negociacaoService)
    {
        _embarqueRepo = embarqueRepo;
        _negRepo = negRepo;
        _produtorRepo = produtorRepo;
        _auditoriaRepo = auditoriaRepo;
        _negociacaoService = negociacaoService;
    }

    public async Task<Embarque> Criar(int negociacaoId, EmbarqueRequest request, int usuarioId, string usuarioNome)
    {
        var neg = await _negRepo.ObterPorId(negociacaoId) ?? throw new Exception("Negociação não encontrada");
        if (neg.Status != "Fechado" && neg.Status != "EmEntrega")
            throw new Exception("Embarques só podem ser criados para negociações Fechadas ou Em entrega");

        var produtorOrigem = string.IsNullOrWhiteSpace(request.ProdutorOrigem) ? "Não informado" : request.ProdutorOrigem.Trim();
        var itensResolvidos = await ResolverItens(neg, produtorOrigem, request.Itens, excluirEmbarqueId: null);

        var embarque = new Embarque
        {
            NegociacaoId = negociacaoId,
            ProdutorOrigem = produtorOrigem,
            MunicipioDestinoId = request.MunicipioDestinoId ?? neg.MunicipioDestinoId,
            DataEmbarque = request.DataEmbarque,
            Nf = request.Nf,
            Gta = request.Gta,
            CriadoEm = DateTime.Now,
            CriadoPor = usuarioId,
            Itens = itensResolvidos
        };

        embarque.Id = await _embarqueRepo.Criar(embarque);

        await _auditoriaRepo.Registrar("embarques", embarque.Id, "criacao", null,
            $"Emb. {embarque.Numero} — {produtorOrigem}", usuarioId, usuarioNome,
            $"Embarque {embarque.Numero} criado na negociação {neg.Numero}");

        return await _embarqueRepo.ObterPorId(embarque.Id) ?? embarque;
    }

    public async Task<Embarque> Atualizar(int embarqueId, EmbarqueRequest request, int usuarioId, string usuarioNome)
    {
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");
        if (embarque.ChegadaConfirmadaEm.HasValue)
            throw new Exception("Não é possível alterar um embarque que já teve a chegada confirmada");

        var neg = await _negRepo.ObterPorId(embarque.NegociacaoId) ?? throw new Exception("Negociação não encontrada");
        var produtorOrigem = string.IsNullOrWhiteSpace(request.ProdutorOrigem) ? embarque.ProdutorOrigem : request.ProdutorOrigem.Trim();
        var itensResolvidos = await ResolverItens(neg, produtorOrigem, request.Itens, excluirEmbarqueId: embarqueId);

        embarque.ProdutorOrigem = produtorOrigem;
        embarque.MunicipioDestinoId = request.MunicipioDestinoId ?? embarque.MunicipioDestinoId;
        embarque.DataEmbarque = request.DataEmbarque;
        embarque.Nf = request.Nf;
        embarque.Gta = request.Gta;
        embarque.AtualizadoEm = DateTime.Now;
        embarque.Itens = itensResolvidos;

        await _embarqueRepo.Atualizar(embarque);
        await _auditoriaRepo.Registrar("embarques", embarqueId, "edicao", null, null,
            usuarioId, usuarioNome, $"Embarque {embarque.Numero} atualizado");

        return await _embarqueRepo.ObterPorId(embarqueId) ?? embarque;
    }

    // Registra a chegada por categoria (fazenda). Não trava por excedente — se qtdChegou
    // ultrapassar o embarcado, apenas registra (o frontend já pediu confirmação antes de enviar).
    // Baixa o saldo da negociação e dispara a transição automática de status:
    // Fechado -> EmEntrega (primeira chegada) e EmEntrega -> Concluido (saldo total = 0).
    public async Task RegistrarChegada(int embarqueId, ChegadaRequest request, int usuarioId, string usuarioNome)
    {
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");

        if (request.Itens == null || request.Itens.Count == 0)
            throw new Exception("Informe a quantidade recebida de ao menos uma categoria");

        foreach (var itemReq in request.Itens)
        {
            if (!embarque.Itens.Any(i => i.Id == itemReq.EmbarqueItemId))
                throw new Exception($"Item {itemReq.EmbarqueItemId} não pertence a este embarque");
            if (itemReq.QtdChegou < 0)
                throw new Exception("Quantidade que chegou não pode ser negativa");
        }

        var ehPrimeiraChegada = await _embarqueRepo.EhPrimeiraChegada(embarque.NegociacaoId, embarqueId);

        await _embarqueRepo.RegistrarChegada(
            embarqueId,
            request.Itens.Select(i => (i.EmbarqueItemId, i.QtdChegou, i.PesoMedioEntrada, i.AnimaisDebilitados)).ToList(),
            request.ObservacoesChegada, usuarioId);

        await _embarqueRepo.RecalcularEntregaPorNegociacao(embarque.NegociacaoId);

        await _auditoriaRepo.Registrar("embarques", embarqueId, "chegada", null, null,
            usuarioId, usuarioNome, $"Chegada registrada para o embarque {embarque.Numero}");

        if (ehPrimeiraChegada)
            await _negociacaoService.AvancarStatusAutomatico(embarque.NegociacaoId, "EmEntrega", usuarioId, usuarioNome);

        var saldoRestante = await _embarqueRepo.SaldoRecebidoTotal(embarque.NegociacaoId);
        if (saldoRestante <= 0)
            await _negociacaoService.AvancarStatusAutomatico(embarque.NegociacaoId, "Concluido", usuarioId, usuarioNome);
    }

    // Completa NF/GTA a partir da tela de Conferência, mesmo após a chegada confirmada
    // (o ERS permite finalizar a documentação nessa etapa).
    public async Task AtualizarDocumentos(int embarqueId, string? nf, string? gta, int usuarioId, string usuarioNome)
    {
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");
        await _embarqueRepo.AtualizarDocumentos(embarqueId, nf, gta);
        await _auditoriaRepo.Registrar("embarques", embarqueId, "documentos", null, null,
            usuarioId, usuarioNome, $"NF/GTA do embarque {embarque.Numero} atualizados na conferência");
    }

    public async Task Excluir(int embarqueId, int usuarioId, string usuarioNome)
    {
        var embarque = await _embarqueRepo.ObterPorId(embarqueId) ?? throw new Exception("Embarque não encontrado");
        if (embarque.ChegadaConfirmadaEm.HasValue)
            throw new Exception("Não é possível excluir um embarque que já teve a chegada confirmada");

        await _auditoriaRepo.Registrar("embarques", embarqueId, "exclusao", $"Emb. {embarque.Numero}", null,
            usuarioId, usuarioNome, $"Embarque {embarque.Numero} excluído");
        await _embarqueRepo.Excluir(embarqueId);
    }

    // Resolve cada item do embarque contra um lote (negociacao_produtores): usa o lote
    // informado, ou localiza um já existente pelo nome do produtor (normalizado), ou cria
    // um novo na hora — desde que o item traga CategoriaId + QtdTotalProdutor. Isso é o que
    // permite criar um embarque sem passar antes pela tela de Desmembramento (o ERS trata o
    // desmembramento como opcional: "poderá ser detalhada em lotes", nem toda negociação é
    // fracionada por produtor).
    private async Task<List<EmbarqueItem>> ResolverItens(
        Negociacao neg, string produtorOrigem, List<EmbarqueItemRequest> itens, int? excluirEmbarqueId)
    {
        if (itens == null || itens.Count == 0)
            throw new Exception("Informe ao menos uma categoria embarcada");

        var resolvidos = new List<EmbarqueItem>();
        foreach (var itemReq in itens)
        {
            if (itemReq.QtdEmbarcada <= 0)
                throw new Exception("Quantidade embarcada deve ser maior que zero");

            NegociacaoProdutor lote;
            if (itemReq.NegociacaoProdutorId.HasValue)
            {
                lote = await _produtorRepo.ObterPorId(itemReq.NegociacaoProdutorId.Value)
                    ?? throw new Exception($"Lote {itemReq.NegociacaoProdutorId} não encontrado");
                if (lote.NegociacaoId != neg.Id)
                    throw new Exception("Lote não pertence a esta negociação");
            }
            else
            {
                if (!itemReq.CategoriaId.HasValue)
                    throw new Exception("Informe a categoria para criar o lote automaticamente");

                lote = await _produtorRepo.BuscarPorProdutorCategoria(neg.Id, itemReq.CategoriaId.Value, produtorOrigem)
                    ?? await CriarLoteAutomatico(neg, itemReq.CategoriaId.Value, produtorOrigem, itemReq.QtdTotalProdutor);
            }

            var jaEmbarcadoOutros = await _embarqueRepo.SomaEmbarcadaPorLote(lote.Id, excluirEmbarqueId);
            var saldoDisponivel = lote.QtdCb - jaEmbarcadoOutros;
            if (itemReq.QtdEmbarcada > saldoDisponivel)
                throw new Exception($"Quantidade embarcada ({itemReq.QtdEmbarcada}) excede o saldo disponível do lote {lote.ProdutorOrigem} ({saldoDisponivel} CB)");

            resolvidos.Add(new EmbarqueItem { NegociacaoProdutorId = lote.Id, QtdEmbarcada = itemReq.QtdEmbarcada });
        }

        return resolvidos;
    }

    // Cria um lote novo (mesma validação de saldo não desmembrado que ProdutorService usa).
    // Se duas requisições concorrentes tentarem criar o mesmo produtor+categoria ao mesmo
    // tempo, a constraint única do banco rejeita a segunda — nesse caso, reaproveita o lote
    // que a outra acabou de criar em vez de falhar.
    private async Task<NegociacaoProdutor> CriarLoteAutomatico(Negociacao neg, int categoriaId, string produtorOrigem, int? qtdTotalProdutor)
    {
        var item = neg.Itens.FirstOrDefault(i => i.CategoriaId == categoriaId)
            ?? throw new Exception("A negociação não tem essa categoria");

        if (!qtdTotalProdutor.HasValue || qtdTotalProdutor.Value <= 0)
            throw new Exception($"Informe a quantidade total do produtor para {item.CategoriaNome} (novo lote)");

        var somaAtual = await _produtorRepo.SomaQtdPorCategoria(neg.Id, categoriaId);
        var saldoNaoDesmembrado = (item.QtdNegociada ?? 0) - somaAtual;
        if (qtdTotalProdutor.Value > saldoNaoDesmembrado)
            throw new Exception($"Quantidade total ({qtdTotalProdutor.Value} CB) excede o saldo não desmembrado de {item.CategoriaNome} ({saldoNaoDesmembrado} CB)");

        var novoLote = new NegociacaoProdutor
        {
            NegociacaoId = neg.Id,
            CategoriaId = categoriaId,
            ProdutorOrigem = produtorOrigem,
            QtdCb = qtdTotalProdutor.Value,
            CriadoEm = DateTime.Now
        };

        try
        {
            novoLote.Id = await _produtorRepo.Criar(novoLote);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Corrida: outra requisição criou o mesmo produtor+categoria primeiro. Usa o dela.
            return await _produtorRepo.BuscarPorProdutorCategoria(neg.Id, categoriaId, produtorOrigem)
                ?? throw new Exception("Não foi possível criar nem localizar o lote do produtor (conflito de concorrência)");
        }

        return await _produtorRepo.ObterPorId(novoLote.Id) ?? novoLote;
    }
}
