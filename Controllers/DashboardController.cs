using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace PrecoBoi.Api.Controllers;

/// <summary>Indicadores analíticos agregados das negociações.</summary>
/// <remarks>
/// Consolida volumes e preços médios (ponderados por peso) por comprador, corretor e categoria.
/// Todos os endpoints aceitam os mesmos filtros opcionais via query string
/// (comprador, corretor, UF, status). Preços médios são ponderados por <c>qtd × peso médio</c>.
/// </remarks>
[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Indicadores e relatórios analíticos")]
public class DashboardController : ControllerBase
{
    private readonly NegociacaoRepository _negRepo;
    private readonly IConfiguration _config;

    public DashboardController(NegociacaoRepository negRepo, IConfiguration config)
    {
        _negRepo = negRepo;
        _config = config;
    }

    /// <summary>Totais e preços médios agregados por comprador.</summary>
    /// <param name="filtro">Filtros opcionais e paginação.</param>
    /// <response code="200">Indicadores por comprador.</response>
    [HttpGet("compradores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PorComprador([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);

        var sql = $@"
            SELECT
                u.id as comprador_id,
                u.nome as comprador_nome,
                COUNT(DISTINCT n.id) as total_negociacoes,
                SUM(ni.qtd_negociada) as qtd_total,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) as preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) as preco_colocado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada), 0), 2
                ) as peso_medio
            FROM negociacoes n
            JOIN negociacao_itens ni ON ni.negociacao_id = n.id
            JOIN usuarios u ON u.id = n.comprador_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}
            GROUP BY u.id, u.nome
            ORDER BY u.nome";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Lista as negociações de um comprador específico.</summary>
    /// <param name="compradorId">Identificador do comprador.</param>
    /// <param name="filtro">Filtros adicionais e paginação.</param>
    /// <response code="200">Negociações do comprador com total.</response>
    [HttpGet("compradores/{compradorId}/negociacoes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> NegociacoesPorComprador(int compradorId, [FromQuery] NegociacaoFiltroRequest filtro)
    {
        filtro = filtro with { CompradorId = compradorId };
        var (items, total) = await _negRepo.Listar(filtro);
        return Ok(new { items, total });
    }

    /// <summary>Distribuição por corretor e categoria das negociações de um comprador.</summary>
    /// <param name="compradorId">Identificador do comprador.</param>
    /// <param name="filtro">Filtros adicionais.</param>
    /// <response code="200">Quantidades (em andamento/fechadas) e preços médios por corretor e categoria.</response>
    [HttpGet("compradores/{compradorId}/categorias-corretor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CategoriasPorComprador(int compradorId, [FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        filtro = filtro with { CompradorId = compradorId };
        var where = BuildWhere(filtro, out var parameters);

        var sql = $@"
            SELECT
                c.id   AS corretor_id,
                c.nome AS corretor_nome,
                cat.nome     AS categoria,
                cat.peso_min,
                cat.peso_max,
                cat.ordem,
                COALESCE(SUM(ni.qtd_negociada), 0) AS qtd_total,
                COALESCE(SUM(CASE WHEN n.status = 'EmNegociacao' THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_andamento,
                COALESCE(SUM(CASE WHEN n.status = 'Fechado'      THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_fechadas,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_colocado_medio
            FROM negociacao_itens ni
            JOIN negociacoes n        ON n.id   = ni.negociacao_id
            JOIN corretores c         ON c.id   = n.corretor_id
            JOIN categorias cat       ON cat.id  = ni.categoria_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}
            GROUP BY c.id, c.nome, cat.nome, cat.peso_min, cat.peso_max, cat.ordem
            ORDER BY c.nome, cat.ordem";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Totais e preços médios agregados por corretor e categoria.</summary>
    /// <param name="filtro">Filtros opcionais.</param>
    /// <response code="200">Indicadores por corretor e categoria.</response>
    [HttpGet("corretores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PorCorretor([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);

        var sql = $@"
            SELECT
                c.id as corretor_id,
                c.nome as corretor_nome,
                cat.nome as categoria,
                cat.peso_min,
                cat.peso_max,
                COUNT(DISTINCT n.id) as total_negociacoes,
                SUM(ni.qtd_negociada) as qtd_total,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) as preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) as preco_colocado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada), 0), 2
                ) as peso_medio
            FROM negociacoes n
            JOIN negociacao_itens ni ON ni.negociacao_id = n.id
            JOIN corretores c ON c.id = n.corretor_id
            JOIN categorias cat ON cat.id = ni.categoria_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}
            GROUP BY c.id, c.nome, cat.nome, cat.peso_min, cat.peso_max, cat.ordem
            ORDER BY c.nome, cat.ordem";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Totais consolidados (quantidade e preços médios) de toda a base filtrada.</summary>
    /// <param name="filtro">Filtros opcionais.</param>
    /// <response code="200">Objeto único com os totais agregados.</response>
    [HttpGet("totais")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Totais([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);

        var sql = $@"
            SELECT
                COALESCE(SUM(ni.qtd_negociada), 0) AS qtd_total,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_colocado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada), 0), 2
                ) AS peso_medio
            FROM negociacao_itens ni
            JOIN negociacoes n        ON n.id  = ni.negociacao_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}";

        var result = await conn.QuerySingleAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Totais e preços médios agregados por categoria.</summary>
    /// <param name="filtro">Filtros opcionais.</param>
    /// <response code="200">Indicadores por categoria, ordenados pela ordem da categoria.</response>
    [HttpGet("por-categoria")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PorCategoria([FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);

        var sql = $@"
            SELECT
                cat.id    AS categoria_id,
                cat.nome  AS categoria,
                cat.peso_min,
                cat.peso_max,
                cat.ordem,
                COUNT(DISTINCT n.id) AS total_negociacoes,
                COALESCE(SUM(ni.qtd_negociada), 0) AS qtd_total,
                COALESCE(SUM(CASE WHEN n.status = 'EmNegociacao' THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_andamento,
                COALESCE(SUM(CASE WHEN n.status = 'Fechado'      THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_fechadas,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_colocado_medio
            FROM negociacao_itens ni
            JOIN negociacoes n        ON n.id   = ni.negociacao_id
            JOIN categorias cat       ON cat.id  = ni.categoria_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}
            GROUP BY cat.id, cat.nome, cat.peso_min, cat.peso_max, cat.ordem
            ORDER BY cat.ordem";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Detalha uma categoria por comprador e corretor.</summary>
    /// <param name="categoriaId">Identificador da categoria.</param>
    /// <param name="filtro">Filtros opcionais.</param>
    /// <response code="200">Quebra por comprador/corretor da categoria.</response>
    [HttpGet("por-categoria/{categoriaId}/detalhe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DetalhePorCategoria(int categoriaId, [FromQuery] NegociacaoFiltroRequest filtro)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);
        var andCategoria = where.Length > 0 ? "AND ni.categoria_id = @CategoriaId" : "WHERE ni.categoria_id = @CategoriaId";
        parameters.Add("CategoriaId", categoriaId);

        var sql = $@"
            SELECT
                u.id   AS comprador_id,
                u.nome AS comprador_nome,
                c.id   AS corretor_id,
                c.nome AS corretor_nome,
                COALESCE(SUM(ni.qtd_negociada), 0) AS qtd_total,
                COALESCE(SUM(CASE WHEN n.status = 'EmNegociacao' THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_andamento,
                COALESCE(SUM(CASE WHEN n.status = 'Fechado'      THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_fechadas,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_negociado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_negociado_medio,
                ROUND(
                    SUM(ni.qtd_negociada * ni.preco_colocado * ni.peso_medio) /
                    NULLIF(SUM(ni.qtd_negociada * ni.peso_medio), 0), 4
                ) AS preco_colocado_medio
            FROM negociacao_itens ni
            JOIN negociacoes n        ON n.id  = ni.negociacao_id
            JOIN usuarios u           ON u.id  = n.comprador_id
            JOIN corretores c         ON c.id  = n.corretor_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where} {andCategoria}
            GROUP BY u.id, u.nome, c.id, c.nome
            ORDER BY u.nome, c.nome";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
    }

    /// <summary>Resumo de cabeças em andamento e fechadas, por categoria e total geral.</summary>
    /// <param name="mock">
    /// Quando informado (1 a 4), retorna dados sintéticos de magnitudes crescentes para
    /// testar a renderização do front sem consultar o banco. Omitir em produção.
    /// </param>
    /// <response code="200">Totais de cabeças (andamento/fechadas) e quebra por categoria.</response>
    [HttpGet("resumo-cabecas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumoCabecas([FromQuery] NegociacaoFiltroRequest filtro, [FromQuery] int? mock = null)
    {
        // === Modo MOCK para testes de UI (não consulta o banco) ===
        // Uso: GET /api/dashboard/resumo-cabecas?mock=1 (ou 2, 3, 4)
        if (mock.HasValue) return Ok(GerarMock(mock.Value));

        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var where = BuildWhere(filtro, out var parameters);
        var andQtd = where.Length > 0 ? "AND ni.qtd_negociada IS NOT NULL" : "WHERE ni.qtd_negociada IS NOT NULL";

        var sql = $@"
            SELECT
                cat.nome      AS categoria,
                cat.ordem,
                cat.peso_min,
                cat.peso_max,
                COALESCE(SUM(CASE WHEN n.status = 'EmNegociacao' THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_andamento,
                COALESCE(SUM(CASE WHEN n.status = 'Fechado'      THEN ni.qtd_negociada ELSE 0 END), 0) AS cb_fechadas
            FROM negociacao_itens ni
            JOIN negociacoes n  ON n.id  = ni.negociacao_id
            JOIN categorias cat ON cat.id = ni.categoria_id
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where} {andQtd}
            GROUP BY cat.id, cat.nome, cat.ordem, cat.peso_min, cat.peso_max
            ORDER BY cat.ordem";

        var porCategoria = (await conn.QueryAsync(sql, parameters)).ToList();

        long totalAndamento = 0;
        long totalFechadas  = 0;
        foreach (var r in porCategoria)
        {
            totalAndamento += Convert.ToInt64(r.cb_andamento);
            totalFechadas  += Convert.ToInt64(r.cb_fechadas);
        }

        var contagemSql = $@"
            SELECT
                COALESCE(SUM(CASE WHEN n.status = 'EmNegociacao' THEN 1 ELSE 0 END), 0) AS neg_andamento,
                COALESCE(SUM(CASE WHEN n.status = 'Fechado'      THEN 1 ELSE 0 END), 0) AS neg_fechadas
            FROM negociacoes n
            LEFT JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
            {where}";
        var contagem = await conn.QueryFirstAsync(contagemSql, parameters);
        long negociacoesAndamento = Convert.ToInt64(contagem.neg_andamento);
        long negociacoesFechadas  = Convert.ToInt64(contagem.neg_fechadas);

        return Ok(new
        {
            totalAndamento,
            totalFechadas,
            negociacoesAndamento,
            negociacoesFechadas,
            porCategoria
        });
    }

    private static object GerarMock(int preset)
    {
        // Presets crescentes para testar a renderização do front com diferentes magnitudes
        (long negA, long cbA, long negF, long cbF) = preset switch
        {
            1 => (22L,        250L,         350L,          2_530L),       // realidade atual
            2 => (480L,       12_500L,      1_250L,        85_000L),      // ~final de ano
            3 => (3_500L,     95_000L,      12_500L,       450_000L),     // grande empresa
            4 => (25_000L,    1_250_000L,   180_000L,      12_500_000L),  // gigante
            _ => (22L,        250L,         350L,          2_530L)
        };

        // Distribuição aproximada por categoria (5 categorias do seed)
        var categoriasNomes = new[]
        {
            ("Bezerro", 200m, 240m, 1),
            ("Bezerro", 241m, 270m, 2),
            ("Garrote", 271m, 300m, 3),
            ("Garrote", 301m, 330m, 4),
            ("Boi",     331m, 360m, 5),
            ("Boi",     361m, 390m, 6),
        };

        // Pesos arbitrários para distribuir o total entre categorias (somam 100)
        var pesosA = new[] { 8, 18, 22, 20, 18, 14 };
        var pesosF = new[] { 5, 15, 20, 22, 22, 16 };

        var porCategoria = new List<object>();
        for (int i = 0; i < categoriasNomes.Length; i++)
        {
            var (nome, pmin, pmax, ord) = categoriasNomes[i];
            porCategoria.Add(new
            {
                categoria   = nome,
                ordem       = ord,
                peso_min    = pmin,
                peso_max    = pmax,
                cb_andamento = (long)Math.Round(cbA * (pesosA[i] / 100.0)),
                cb_fechadas  = (long)Math.Round(cbF * (pesosF[i] / 100.0))
            });
        }

        return new
        {
            totalAndamento       = cbA,
            totalFechadas        = cbF,
            negociacoesAndamento = negA,
            negociacoesFechadas  = negF,
            porCategoria,
            __mock               = preset
        };
    }

    [HttpGet("anos")]
    public async Task<IActionResult> Anos()
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var sql = @"
            SELECT DISTINCT EXTRACT(YEAR FROM criado_em)::int AS ano
            FROM negociacoes
            WHERE criado_em IS NOT NULL
            ORDER BY ano DESC";

        var anos = await conn.QueryAsync<int>(sql);
        return Ok(anos);
    }

    private string BuildWhere(NegociacaoFiltroRequest filtro, out DynamicParameters parameters)
    {
        var where = new List<string>();
        parameters = new DynamicParameters();

        if (filtro.CompradorId.HasValue) { where.Add("n.comprador_id=@CompradorId"); parameters.Add("CompradorId", filtro.CompradorId); }
        if (filtro.CorretorId.HasValue) { where.Add("n.corretor_id=@CorretorId"); parameters.Add("CorretorId", filtro.CorretorId); }
        if (!string.IsNullOrEmpty(filtro.Uf)) { where.Add("mo.uf=@Uf"); parameters.Add("Uf", filtro.Uf.ToUpper()); }
        if (!string.IsNullOrEmpty(filtro.Status) && filtro.Status != "Todos") { where.Add("n.status=@Status"); parameters.Add("Status", filtro.Status); }
        if (filtro.Ano.HasValue) { where.Add("EXTRACT(YEAR FROM n.criado_em)=@Ano"); parameters.Add("Ano", filtro.Ano); }
        if (filtro.Mes.HasValue) { where.Add("EXTRACT(MONTH FROM n.criado_em)=@Mes"); parameters.Add("Mes", filtro.Mes); }

        return where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
    }
}
