using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Models;
using PrecoBoi.Api.Repositories;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PrecoBoi.Api.Controllers;

/// <summary>
/// Cadastros de apoio: corretores, municípios (origem/destino), categorias,
/// ICMS por UF, cotações regionais, configuração de comissão e auditoria.
/// </summary>
/// <remarks>
/// Leitura disponível a qualquer usuário autenticado. As operações de escrita
/// (criar/editar/excluir) exigem perfil <b>Admin</b> e geram registros de auditoria.
/// </remarks>
[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Cadastros de apoio, parâmetros de cálculo e auditoria")]
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

    /// <summary>Lista os corretores.</summary>
    /// <param name="ativo">Filtro opcional por status (ativo/inativo); omitido = todos.</param>
    /// <response code="200">Lista de corretores.</response>
    [HttpGet("corretores")]
    [ProducesResponseType(typeof(IEnumerable<Corretor>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCorretores([FromQuery] bool? ativo)
        => Ok(await _corretorRepo.Listar(ativo));

    /// <summary>Obtém um corretor pelo identificador.</summary>
    /// <param name="id">Identificador do corretor.</param>
    /// <response code="200">Corretor encontrado.</response>
    /// <response code="404">Corretor não encontrado.</response>
    [HttpGet("corretores/{id}")]
    [ProducesResponseType(typeof(Corretor), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCorretor(int id)
    {
        var c = await _corretorRepo.ObterPorId(id);
        return c == null ? NotFound() : Ok(c);
    }

    /// <summary>Cria um corretor. <b>Requer perfil Admin.</b></summary>
    /// <param name="req">Dados do corretor.</param>
    /// <response code="201">Corretor criado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("corretores")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Corretor), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarCorretor([FromBody] CorretorRequest req)
    {
        var corretor = new Corretor { Nome = req.Nome, Telefone = req.Telefone, Municipio = req.Municipio, Uf = req.Uf, Propriedade = req.Propriedade, Observacoes = req.Observacoes, Ativo = req.Ativo };
        var id = await _corretorRepo.Criar(corretor);
        await _auditoriaRepo.Registrar("corretores", id, "criacao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Corretor {req.Nome} criado");
        corretor.Id = id;
        return CreatedAtAction(nameof(ObterCorretor), new { id }, corretor);
    }

    /// <summary>Atualiza um corretor. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do corretor.</param>
    /// <param name="req">Novos dados do corretor.</param>
    /// <response code="204">Corretor atualizado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPut("corretores/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AtualizarCorretor(int id, [FromBody] CorretorRequest req)
    {
        var corretor = new Corretor { Id = id, Nome = req.Nome, Telefone = req.Telefone, Municipio = req.Municipio, Uf = req.Uf, Propriedade = req.Propriedade, Observacoes = req.Observacoes, Ativo = req.Ativo };
        await _corretorRepo.Atualizar(corretor);
        await _auditoriaRepo.Registrar("corretores", id, "edicao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Corretor {req.Nome} atualizado");
        return NoContent();
    }

    /// <summary>Exclui um corretor. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do corretor.</param>
    /// <response code="204">Corretor excluído.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpDelete("corretores/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExcluirCorretor(int id) { await _corretorRepo.Excluir(id); return NoContent(); }

    // === MUNICÍPIOS DE ORIGEM ===

    /// <summary>Lista os municípios de origem (com distância e valor por km).</summary>
    /// <param name="ativo">Filtro opcional por status; omitido = todos.</param>
    /// <response code="200">Lista de municípios de origem.</response>
    [HttpGet("municipios-origem")]
    [ProducesResponseType(typeof(IEnumerable<MunicipioOrigem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarMunicipiosOrigem([FromQuery] bool? ativo)
        => Ok(await _munOrigemRepo.Listar(ativo));

    /// <summary>Obtém um município de origem pelo identificador.</summary>
    /// <param name="id">Identificador do município.</param>
    /// <response code="200">Município encontrado.</response>
    /// <response code="404">Município não encontrado.</response>
    [HttpGet("municipios-origem/{id}")]
    [ProducesResponseType(typeof(MunicipioOrigem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterMunicipioOrigem(int id)
    {
        var m = await _munOrigemRepo.ObterPorId(id);
        return m == null ? NotFound() : Ok(m);
    }

    /// <summary>Cria um município de origem. <b>Requer perfil Admin.</b></summary>
    /// <param name="req">Dados do município (a UF é normalizada para maiúsculas).</param>
    /// <response code="201">Município criado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("municipios-origem")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MunicipioOrigem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarMunicipioOrigem([FromBody] MunicipioOrigemRequest req)
    {
        var m = new MunicipioOrigem { Nome = req.Nome, Uf = req.Uf.ToUpper(), DistanciaKm = req.DistanciaKm, ValorKm = req.ValorKm, Ativo = req.Ativo };
        var id = await _munOrigemRepo.Criar(m);
        await _auditoriaRepo.Registrar("municipios_origem", id, "criacao", null, req.Nome, ObterUsuarioId(), ObterUsuarioNome(), $"Município {req.Nome}-{req.Uf} criado");
        m.Id = id;
        return CreatedAtAction(nameof(ObterMunicipioOrigem), new { id }, m);
    }

    /// <summary>Atualiza um município de origem. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Alterações no valor por km são registradas em auditoria.</remarks>
    /// <param name="id">Identificador do município.</param>
    /// <param name="req">Novos dados do município.</param>
    /// <response code="204">Município atualizado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPut("municipios-origem/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Exclui um município de origem. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do município.</param>
    /// <response code="204">Município excluído.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpDelete("municipios-origem/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExcluirMunicipioOrigem(int id) { await _munOrigemRepo.Excluir(id); return NoContent(); }

    // === MUNICÍPIOS DE DESTINO ===

    /// <summary>Lista os municípios de destino (praças de abate/comercialização).</summary>
    /// <response code="200">Lista de municípios de destino.</response>
    [HttpGet("municipios-destino")]
    [ProducesResponseType(typeof(IEnumerable<MunicipioDestino>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarMunicipiosDestino() => Ok(await _munDestinoRepo.Listar());

    /// <summary>Obtém o município de destino marcado como padrão.</summary>
    /// <response code="200">Município de destino padrão (ou vazio se não houver).</response>
    [HttpGet("municipios-destino/padrao")]
    [ProducesResponseType(typeof(MunicipioDestino), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterDestinopadrao() => Ok(await _munDestinoRepo.ObterPadrao());

    /// <summary>Cria um município de destino. <b>Requer perfil Admin.</b></summary>
    /// <param name="req">Dados do município de destino.</param>
    /// <response code="200">Município criado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("municipios-destino")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MunicipioDestino), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarMunicipioDestino([FromBody] MunicipioDestinoRequest req)
    {
        var m = new MunicipioDestino { Nome = req.Nome, Uf = req.Uf.ToUpper(), Padrao = req.Padrao };
        var id = await _munDestinoRepo.Criar(m);
        m.Id = id;
        return Ok(m);
    }

    /// <summary>Atualiza um município de destino. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do município.</param>
    /// <param name="req">Novos dados do município.</param>
    /// <response code="204">Município atualizado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPut("municipios-destino/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AtualizarMunicipioDestino(int id, [FromBody] MunicipioDestinoRequest req)
    {
        var m = new MunicipioDestino { Id = id, Nome = req.Nome, Uf = req.Uf.ToUpper(), Padrao = req.Padrao };
        await _munDestinoRepo.Atualizar(m);
        return NoContent();
    }

    /// <summary>Exclui um município de destino. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador do município.</param>
    /// <response code="204">Município excluído.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpDelete("municipios-destino/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExcluirMunicipioDestino(int id) { await _munDestinoRepo.Excluir(id); return NoContent(); }

    // === CATEGORIAS ===

    /// <summary>Lista as categorias de gado (faixas de peso) e seus parâmetros.</summary>
    /// <response code="200">Lista de categorias.</response>
    [HttpGet("categorias")]
    [ProducesResponseType(typeof(IEnumerable<Categoria>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCategorias() => Ok(await _catRepo.Listar());

    /// <summary>Atualiza uma categoria. <b>Requer perfil Admin.</b></summary>
    /// <param name="id">Identificador da categoria.</param>
    /// <param name="req">Novos dados da categoria.</param>
    /// <response code="204">Categoria atualizada.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPut("categorias/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AtualizarCategoria(int id, [FromBody] CategoriaRequest req)
    {
        var cat = new Categoria { Id = id, Nome = req.Nome, PesoMin = req.PesoMin, PesoMax = req.PesoMax, PesoMedio = req.PesoMedio, CabCaminhao = req.CabCaminhao, Ordem = req.Ordem };
        await _catRepo.Atualizar(cat);
        return NoContent();
    }

    /// <summary>Cria uma categoria. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Valida a faixa de peso (mín &lt; máx), o peso médio dentro da faixa e cabeças por caminhão &gt; 0.</remarks>
    /// <param name="req">Dados da categoria, incluindo ágio padrão opcional.</param>
    /// <response code="201">Categoria criada.</response>
    /// <response code="400">Faixa de peso ou parâmetros inválidos.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("categorias")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarCategoria([FromBody] CategoriaRequest req)
    {
        if (req.PesoMin < 0 || req.PesoMax <= req.PesoMin)
            return BadRequest(new { mensagem = "Faixa de peso inválida (mín deve ser menor que máx)." });
        if (req.PesoMedio < req.PesoMin || req.PesoMedio > req.PesoMax)
            return BadRequest(new { mensagem = "Peso médio deve estar dentro da faixa." });
        if (req.CabCaminhao <= 0)
            return BadRequest(new { mensagem = "Cabeças por caminhão deve ser maior que zero." });

        var cat = new Categoria
        {
            Nome = req.Nome,
            PesoMin = req.PesoMin,
            PesoMax = req.PesoMax,
            PesoMedio = req.PesoMedio,
            CabCaminhao = req.CabCaminhao,
            Ordem = req.Ordem
        };
        var id = await _catRepo.Criar(cat, req.AgioPadrao ?? 0m);
        cat.Id = id;
        return CreatedAtAction(nameof(ListarCategorias), new { id }, cat);
    }

    /// <summary>Exclui uma categoria. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Bloqueada quando a categoria está em uso em itens de negociação.</remarks>
    /// <param name="id">Identificador da categoria.</param>
    /// <response code="204">Categoria excluída.</response>
    /// <response code="400">Categoria em uso em negociações.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpDelete("categorias/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExcluirCategoria(int id)
    {
        var uso = await _catRepo.ContarUsoEmNegociacoes(id);
        if (uso > 0)
            return BadRequest(new { mensagem = $"Categoria está em uso em {uso} item(ns) de negociação e não pode ser excluída." });

        await _catRepo.Excluir(id);
        return NoContent();
    }

    // === ICMS ===

    /// <summary>Lista as alíquotas de ICMS e recuperação por UF.</summary>
    /// <response code="200">Lista de ICMS por UF.</response>
    [HttpGet("icms")]
    [ProducesResponseType(typeof(IEnumerable<Icms>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarIcms() => Ok(await _icmsRepo.Listar());

    /// <summary>Atualiza o ICMS de uma UF. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Mudanças de alíquota são registradas em auditoria.</remarks>
    /// <param name="uf">Sigla da UF (ex.: <c>SP</c>).</param>
    /// <param name="req">Alíquota e percentual de recuperação.</param>
    /// <response code="204">ICMS atualizado.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPut("icms/{uf}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Lista as cotações regionais (valor da arroba por UF) com ágios por categoria.</summary>
    /// <response code="200">Lista de cotações regionais.</response>
    [HttpGet("cotacoes")]
    [ProducesResponseType(typeof(IEnumerable<CotacaoRegional>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCotacoes() => Ok(await _cotacaoRepo.Listar());

    /// <summary>Obtém a cotação regional de uma UF.</summary>
    /// <param name="uf">Sigla da UF (ex.: <c>SP</c>).</param>
    /// <response code="200">Cotação encontrada.</response>
    /// <response code="404">Não há cotação cadastrada para a UF.</response>
    [HttpGet("cotacoes/{uf}")]
    [ProducesResponseType(typeof(CotacaoRegional), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCotacao(string uf)
    {
        var c = await _cotacaoRepo.ObterPorUf(uf);
        return c == null ? NotFound() : Ok(c);
    }

    /// <summary>Cria ou atualiza (upsert) a cotação regional de uma UF. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Mudanças no valor da arroba são registradas em auditoria.</remarks>
    /// <param name="req">Cotação (valor da arroba e ágios por categoria).</param>
    /// <response code="200">Cotação salva.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("cotacoes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Obtém a configuração de comissão vigente.</summary>
    /// <response code="200">Configuração de comissão (percentual e status).</response>
    [HttpGet("config-comissao")]
    [ProducesResponseType(typeof(ConfigComissao), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterConfigComissao() => Ok(await _configRepo.Obter());

    /// <summary>Salva a configuração de comissão. <b>Requer perfil Admin.</b></summary>
    /// <remarks>Mudanças no percentual são registradas em auditoria.</remarks>
    /// <param name="req">Percentual de comissão e status.</param>
    /// <response code="200">Configuração salva.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpPost("config-comissao")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Lista o trilho de auditoria de forma paginada. <b>Requer perfil Admin.</b></summary>
    /// <param name="filtro">Filtros (tabela, usuário, intervalo de datas) e paginação.</param>
    /// <response code="200">Página de registros de auditoria com o total.</response>
    /// <response code="403">Usuário sem perfil Admin.</response>
    [HttpGet("auditoria")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
