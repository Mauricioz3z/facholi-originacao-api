using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PrecoBoi.Api.DTOs;
using PrecoBoi.Api.Repositories;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly NegociacaoRepository _negRepo;
    private readonly IConfiguration _config;

    public DashboardController(NegociacaoRepository negRepo, IConfiguration config)
    {
        _negRepo = negRepo;
        _config = config;
    }

    [HttpGet("compradores")]
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

    [HttpGet("compradores/{compradorId}/negociacoes")]
    public async Task<IActionResult> NegociacoesPorComprador(int compradorId, [FromQuery] NegociacaoFiltroRequest filtro)
    {
        filtro = filtro with { CompradorId = compradorId };
        var (items, total) = await _negRepo.Listar(filtro);
        return Ok(new { items, total });
    }

    [HttpGet("compradores/{compradorId}/categorias-corretor")]
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

    [HttpGet("corretores")]
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

    [HttpGet("totais")]
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

    [HttpGet("por-categoria")]
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

    [HttpGet("por-categoria/{categoriaId}/detalhe")]
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

    [HttpGet("resumo-cabecas")]
    public async Task<IActionResult> ResumoCabecas([FromQuery] int? mock = null)
    {
        // === Modo MOCK para testes de UI (não consulta o banco) ===
        // Uso: GET /api/dashboard/resumo-cabecas?mock=1 (ou 2, 3, 4)
        if (mock.HasValue) return Ok(GerarMock(mock.Value));

        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);

        var sql = @"
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
            WHERE ni.qtd_negociada IS NOT NULL
            GROUP BY cat.id, cat.nome, cat.ordem, cat.peso_min, cat.peso_max
            ORDER BY cat.ordem";

        var porCategoria = (await conn.QueryAsync(sql)).ToList();

        long totalAndamento = 0;
        long totalFechadas  = 0;
        foreach (var r in porCategoria)
        {
            totalAndamento += Convert.ToInt64(r.cb_andamento);
            totalFechadas  += Convert.ToInt64(r.cb_fechadas);
        }

        var contagemSql = @"
            SELECT
                COALESCE(SUM(CASE WHEN status = 'EmNegociacao' THEN 1 ELSE 0 END), 0) AS neg_andamento,
                COALESCE(SUM(CASE WHEN status = 'Fechado'      THEN 1 ELSE 0 END), 0) AS neg_fechadas
            FROM negociacoes";
        var contagem = await conn.QueryFirstAsync(contagemSql);
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

    private string BuildWhere(NegociacaoFiltroRequest filtro, out DynamicParameters parameters)
    {
        var where = new List<string>();
        parameters = new DynamicParameters();

        if (filtro.CompradorId.HasValue) { where.Add("n.comprador_id=@CompradorId"); parameters.Add("CompradorId", filtro.CompradorId); }
        if (filtro.CorretorId.HasValue) { where.Add("n.corretor_id=@CorretorId"); parameters.Add("CorretorId", filtro.CorretorId); }
        if (!string.IsNullOrEmpty(filtro.Uf)) { where.Add("mo.uf=@Uf"); parameters.Add("Uf", filtro.Uf.ToUpper()); }
        if (!string.IsNullOrEmpty(filtro.Status) && filtro.Status != "Todos") { where.Add("n.status=@Status"); parameters.Add("Status", filtro.Status); }

        return where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
    }
}
