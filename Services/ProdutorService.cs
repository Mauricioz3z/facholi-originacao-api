using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Services;

public class ProdutorService
{
    private readonly NegociacaoProdutorRepository _produtorRepo;
    private readonly NegociacaoRepository _negRepo;
    private readonly AuditoriaRepository _auditoriaRepo;

    public ProdutorService(NegociacaoProdutorRepository produtorRepo, NegociacaoRepository negRepo, AuditoriaRepository auditoriaRepo)
    {
        _produtorRepo = produtorRepo;
        _negRepo = negRepo;
        _auditoriaRepo = auditoriaRepo;
    }

    private static void ValidarPermissao(Models.Negociacao neg, int usuarioId, string usuarioPerfil)
    {
        var ehAdmin = usuarioPerfil == "Admin";
        if (neg.Status != "EmNegociacao" && neg.Status != "Fechado" && !ehAdmin)
            throw new Exception("Desmembramento não pode mais ser alterado neste status da negociação");
        if (!ehAdmin && neg.CompradorId != usuarioId)
            throw new UnauthorizedAccessException("Você só pode alterar o desmembramento de negociações que criou.");
    }

    public async Task<NegociacaoProdutor> Adicionar(int negociacaoId, NegociacaoProdutorRequest request, int usuarioId, string usuarioNome, string usuarioPerfil)
    {
        var neg = await _negRepo.ObterPorId(negociacaoId) ?? throw new Exception("Negociação não encontrada");
        ValidarPermissao(neg, usuarioId, usuarioPerfil);

        var item = neg.Itens.FirstOrDefault(i => i.CategoriaId == request.CategoriaId)
            ?? throw new Exception("A negociação não tem essa categoria");

        if (string.IsNullOrWhiteSpace(request.ProdutorOrigem))
            throw new Exception("Informe o nome do produtor/origem");
        if (request.QtdCb <= 0)
            throw new Exception("Quantidade deve ser maior que zero");

        var somaAtual = await _produtorRepo.SomaQtdPorCategoria(negociacaoId, request.CategoriaId);
        var qtdNegociada = item.QtdNegociada ?? 0;
        if (somaAtual + request.QtdCb > qtdNegociada)
            throw new Exception($"Soma dos lotes ({somaAtual + request.QtdCb} CB) excede a quantidade negociada ({qtdNegociada} CB) para {item.CategoriaNome}");

        var existente = await _produtorRepo.BuscarPorProdutorCategoria(negociacaoId, request.CategoriaId, request.ProdutorOrigem);
        if (existente != null)
            throw new Exception($"Já existe um lote de {existente.ProdutorOrigem} para {item.CategoriaNome} nesta negociação — edite o lote existente em vez de criar outro.");

        var lote = new NegociacaoProdutor
        {
            NegociacaoId = negociacaoId,
            CategoriaId = request.CategoriaId,
            ProdutorOrigem = request.ProdutorOrigem.Trim(),
            QtdCb = request.QtdCb,
            Observacoes = request.Observacoes,
            CriadoEm = DateTime.Now
        };
        lote.Id = await _produtorRepo.Criar(lote);

        await _auditoriaRepo.Registrar("negociacao_produtores", lote.Id, "criacao", null,
            $"{lote.ProdutorOrigem} ({item.CategoriaNome}): {lote.QtdCb} CB",
            usuarioId, usuarioNome, $"Lote adicionado na negociação {neg.Numero}");

        return await _produtorRepo.ObterPorId(lote.Id) ?? lote;
    }

    public async Task<NegociacaoProdutor> Atualizar(int loteId, NegociacaoProdutorRequest request, int usuarioId, string usuarioNome, string usuarioPerfil)
    {
        var lote = await _produtorRepo.ObterPorId(loteId) ?? throw new Exception("Lote não encontrado");
        var neg = await _negRepo.ObterPorId(lote.NegociacaoId) ?? throw new Exception("Negociação não encontrada");
        ValidarPermissao(neg, usuarioId, usuarioPerfil);

        if (lote.CategoriaId != request.CategoriaId)
            throw new Exception("Não é possível trocar a categoria de um lote — exclua e crie um novo");

        var item = neg.Itens.FirstOrDefault(i => i.CategoriaId == lote.CategoriaId)
            ?? throw new Exception("A negociação não tem essa categoria");

        if (string.IsNullOrWhiteSpace(request.ProdutorOrigem))
            throw new Exception("Informe o nome do produtor/origem");
        if (request.QtdCb <= 0)
            throw new Exception("Quantidade deve ser maior que zero");
        if (request.QtdCb < lote.QtdEmbarcada)
            throw new Exception($"Não é possível reduzir para {request.QtdCb} CB: já foram embarcadas {lote.QtdEmbarcada} CB deste lote");

        var somaOutros = await _produtorRepo.SomaQtdPorCategoria(lote.NegociacaoId, lote.CategoriaId, loteId);
        var qtdNegociada = item.QtdNegociada ?? 0;
        if (somaOutros + request.QtdCb > qtdNegociada)
            throw new Exception($"Soma dos lotes ({somaOutros + request.QtdCb} CB) excede a quantidade negociada ({qtdNegociada} CB) para {item.CategoriaNome}");

        var anterior = $"{lote.ProdutorOrigem}: {lote.QtdCb} CB";
        lote.ProdutorOrigem = request.ProdutorOrigem.Trim();
        lote.QtdCb = request.QtdCb;
        lote.Observacoes = request.Observacoes;
        lote.AtualizadoEm = DateTime.Now;

        await _produtorRepo.Atualizar(lote);
        await _auditoriaRepo.Registrar("negociacao_produtores", loteId, "edicao", anterior,
            $"{lote.ProdutorOrigem}: {lote.QtdCb} CB", usuarioId, usuarioNome,
            $"Lote atualizado na negociação {neg.Numero}");

        return await _produtorRepo.ObterPorId(loteId) ?? lote;
    }

    public async Task Excluir(int loteId, int usuarioId, string usuarioNome, string usuarioPerfil)
    {
        var lote = await _produtorRepo.ObterPorId(loteId) ?? throw new Exception("Lote não encontrado");
        var neg = await _negRepo.ObterPorId(lote.NegociacaoId) ?? throw new Exception("Negociação não encontrada");
        ValidarPermissao(neg, usuarioId, usuarioPerfil);

        var qtdEmbarques = await _produtorRepo.ContarEmbarquesVinculados(loteId);
        if (qtdEmbarques > 0)
            throw new Exception("Não é possível excluir um lote que já tem embarques vinculados");

        await _auditoriaRepo.Registrar("negociacao_produtores", loteId, "exclusao",
            $"{lote.ProdutorOrigem}: {lote.QtdCb} CB", null, usuarioId, usuarioNome,
            $"Lote excluído da negociação {neg.Numero}");
        await _produtorRepo.Excluir(loteId);
    }
}
