using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CadastrosController : ControllerBase
{
    private readonly CorretorRepository _corretorRepo;
    private readonly MunicipioOrigemRepository _munOrigemRepo;
    private readonly MunicipioDestinoRepository _munDestinoRepo;
    private readonly CategoriaRepository _catRepo;
    private readonly IcmsRepository _icmsRepo;
    private readonly CotacaoRegionalRepository _cotacaoRepo;
    private readonly ConfigComissaoRepository _configRepo;
    private readonly AuditoriaRepository _auditoriaRepo;

    public CadastrosController(
        CorretorRepository corretorRepo, MunicipioOrigemRepository munOrigemRepo,
        MunicipioDestinoRepository munDestinoRepo, CategoriaRepository catRepo,
        IcmsRepository icmsRepo, CotacaoRegionalRepository cotacaoRepo,
        ConfigComissaoRepository configRepo, AuditoriaRepository auditoriaRepo)
    {
        _corretorRepo = corretorRepo;
        _munOrigemRepo = munOrigemRepo;
        _munDestinoRepo = munDestinoRepo;
        _catRepo = catRepo;
        _icmsRepo = icmsRepo;
        _cotacaoRepo = cotacaoRepo;
        _configRepo = configRepo;
        _auditoriaRepo = auditoriaRepo;
    }

    // === CORRETORES ===
    [HttpGet("corretores")]
    public async Task<IActionResult> ListarCorretores([FromQuery] bool? ativo)
        => Ok(await _corretorRepo.Listar(ativo));

    [HttpGet("corretores/{id}")]
    public async Task<IActionResult> ObterCorretor(int id)
    {
        var c = await _corretorRepo.ObterPorId(id);
        return c == null ? NotFound() : Ok(c);
    }

    [HttpPost("corretores")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CriarCorretor([FromBody] CorretorRequest req)
    {
        var corretor = new Corretor { Nome = req.Nome, Telefone = req.Telefone, Municipio = req.Municipio, Uf = req.Uf, Propriedade = req.Propriedade, Observacoes = req.Observacoes, Ativo = req.Ativo };
        var id = await _corretorRepo.Criar(corretor);
        await _auditoriaRepo.Registrar("corretores", id, "criacao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Corretor {req.Nome} criado");
        corretor.Id = id;
        return CreatedAtAction(nameof(ObterCorretor), new { id }, corretor);
    }

    [HttpPut("corretores/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AtualizarCorretor(int id, [FromBody] CorretorRequest req)
    {
        var corretor = new Corretor { Id = id, Nome = req.Nome, Telefone = req.Telefone, Municipio = req.Municipio, Uf = req.Uf, Propriedade = req.Propriedade, Observacoes = req.Observacoes, Ativo = req.Ativo };
        await _corretorRepo.Atualizar(corretor);
        await _auditoriaRepo.Registrar("corretores", id, "edicao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Corretor {req.Nome} atualizado");
        return NoContent();
    }

    [HttpDelete("corretores/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExcluirCorretor(int id) { await _corretorRepo.Excluir(id); return NoContent(); }

    // === MUNICÍPIOS DE ORIGEM ===
    [HttpGet("municipios-origem")]
    public async Task<IActionResult> ListarMunicipiosOrigem([FromQuery] bool? ativo)
        => Ok(await _munOrigemRepo.Listar(ativo));

    [HttpGet("municipios-origem/{id}")]
    public async Task<IActionResult> ObterMunicipioOrigem(int id)
    {
        var m = await _munOrigemRepo.ObterPorId(id);
        return m == null ? NotFound() : Ok(m);
    }

    [HttpPost("municipios-origem")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CriarMunicipioOrigem([FromBody] MunicipioOrigemRequest req)
    {
        var m = new MunicipioOrigem { Nome = req.Nome, Uf = req.Uf.ToUpper(), DistanciaKm = req.DistanciaKm, ValorKm = req.ValorKm, Ativo = req.Ativo };
        var id = await _munOrigemRepo.Criar(m);
        await _auditoriaRepo.Registrar("municipios_origem", id, "criacao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Município {req.Nome}-{req.Uf} criado");
        m.Id = id;
        return CreatedAtAction(nameof(ObterMunicipioOrigem), new { id }, m);
    }

    [HttpPut("municipios-origem/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AtualizarMunicipioOrigem(int id, [FromBody] MunicipioOrigemRequest req)
    {
        var anterior = await _munOrigemRepo.ObterPorId(id);
        var m = new MunicipioOrigem { Id = id, Nome = req.Nome, Uf = req.Uf.ToUpper(), DistanciaKm = req.DistanciaKm, ValorKm = req.ValorKm, Ativo = req.Ativo };
        await _munOrigemRepo.Atualizar(m);
        if (anterior != null && anterior.ValorKm != req.ValorKm)
            await _auditoriaRepo.Registrar("municipios_origem", id, "valor_km",
                anterior.ValorKm.ToString("F4"), req.ValorKm.ToString("F4"),
                ObterUsuarioId(), ObterUsuarioNome(), $"Valor KM de {req.Nome}-{req.Uf} alterado");
        return NoContent();
    }

    [HttpDelete("municipios-origem/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExcluirMunicipioOrigem(int id) { await _munOrigemRepo.Excluir(id); return NoContent(); }

    // === MUNICÍPIOS DE DESTINO ===
    [HttpGet("municipios-destino")]
    public async Task<IActionResult> ListarMunicipiosDestino() => Ok(await _munDestinoRepo.Listar());

    [HttpGet("municipios-destino/padrao")]
    public async Task<IActionResult> ObterDestinopadrao() => Ok(await _munDestinoRepo.ObterPadrao());

    [HttpPost("municipios-destino")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CriarMunicipioDestino([FromBody] MunicipioDestinoRequest req)
    {
        var m = new MunicipioDestino { Nome = req.Nome, Uf = req.Uf.ToUpper(), Padrao = req.Padrao };
        var id = await _munDestinoRepo.Criar(m);
        m.Id = id;
        return Ok(m);
    }

    [HttpPut("municipios-destino/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AtualizarMunicipioDestino(int id, [FromBody] MunicipioDestinoRequest req)
    {
        var m = new MunicipioDestino { Id = id, Nome = req.Nome, Uf = req.Uf.ToUpper(), Padrao = req.Padrao };
        await _munDestinoRepo.Atualizar(m);
        return NoContent();
    }

    [HttpDelete("municipios-destino/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExcluirMunicipioDestino(int id) { await _munDestinoRepo.Excluir(id); return NoContent(); }

    // === CATEGORIAS ===
    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias() => Ok(await _catRepo.Listar());

    [HttpPut("categorias/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AtualizarCategoria(int id, [FromBody] CategoriaRequest req)
    {
        var cat = new Categoria { Id = id, Nome = req.Nome, PesoMin = req.PesoMin, PesoMax = req.PesoMax, PesoMedio = req.PesoMedio, CabCaminhao = req.CabCaminhao, Ordem = req.Ordem };
        await _catRepo.Atualizar(cat);
        return NoContent();
    }

    // === ICMS ===
    [HttpGet("icms")]
    public async Task<IActionResult> ListarIcms() => Ok(await _icmsRepo.Listar());

    [HttpPut("icms/{uf}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AtualizarIcms(string uf, [FromBody] IcmsRequest req)
    {
        var anterior = await _icmsRepo.ObterPorUf(uf);
        var icms = new Icms { Uf = uf.ToUpper(), Aliquota = req.Aliquota, Recuperacao = req.Recuperacao };
        await _icmsRepo.Atualizar(icms);
        if (anterior != null && anterior.Aliquota != req.Aliquota)
            await _auditoriaRepo.Registrar("icms", null, "aliquota",
                anterior.Aliquota.ToString(), req.Aliquota.ToString(),
                ObterUsuarioId(), ObterUsuarioNome(), $"Alíquota ICMS {uf} alterada");
        return NoContent();
    }

    // === COTAÇÃO REGIONAL ===
    [HttpGet("cotacoes")]
    public async Task<IActionResult> ListarCotacoes() => Ok(await _cotacaoRepo.Listar());

    [HttpGet("cotacoes/{uf}")]
    public async Task<IActionResult> ObterCotacao(string uf)
    {
        var c = await _cotacaoRepo.ObterPorUf(uf);
        return c == null ? NotFound() : Ok(c);
    }

    [HttpPost("cotacoes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SalvarCotacao([FromBody] CotacaoRegionalRequest req)
    {
        var anterior = await _cotacaoRepo.ObterPorUf(req.Uf);
        var cotacao = new CotacaoRegional
        {
            Uf = req.Uf.ToUpper(),
            PracaReferenciaUf = req.PracaReferenciaUf?.ToUpper(),
            ValorArroba = req.ValorArroba,
            Agios = req.Agios.Select(a => new AgioCotacao { CategoriaId = a.CategoriaId, Percentual = a.Percentual }).ToList()
        };
        await _cotacaoRepo.Salvar(cotacao);
        if (anterior != null && anterior.ValorArroba != req.ValorArroba)
            await _auditoriaRepo.Registrar("cotacoes_regionais", null, "valor_arroba",
                anterior.ValorArroba.ToString("F2"), req.ValorArroba.ToString("F2"),
                ObterUsuarioId(), ObterUsuarioNome(), $"Cotação {req.Uf} alterada para R$ {req.ValorArroba:F2}/@");
        return Ok();
    }

    // === CONFIG COMISSÃO ===
    [HttpGet("config-comissao")]
    public async Task<IActionResult> ObterConfigComissao() => Ok(await _configRepo.Obter());

    [HttpPost("config-comissao")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SalvarConfigComissao([FromBody] ConfigComissaoRequest req)
    {
        var anterior = await _configRepo.Obter();
        var config = new ConfigComissao { Percentual = req.Percentual, Ativo = req.Ativo };
        await _configRepo.Salvar(config);
        if (anterior != null && anterior.Percentual != req.Percentual)
            await _auditoriaRepo.Registrar("config_comissao", null, "percentual",
                anterior.Percentual.ToString(), req.Percentual.ToString(),
                ObterUsuarioId(), ObterUsuarioNome(), $"Comissão alterada para {req.Percentual}%");
        return Ok();
    }

    // === AUDITORIA ===
    [HttpGet("auditoria")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListarAuditoria([FromQuery] AuditoriaFiltroRequest filtro)
    {
        var (items, total) = await _auditoriaRepo.Listar(filtro.Tabela, filtro.UsuarioId,
            filtro.DataInicio, filtro.DataFim, filtro.Pagina, filtro.TamanhoPagina);
        return Ok(new { items, total, pagina = filtro.Pagina, tamanhoPagina = filtro.TamanhoPagina });
    }

    private int? ObterUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : null;
    }

    private string ObterUsuarioNome() =>
        User.FindFirst(ClaimTypes.Name)?.Value ?? "";
}
