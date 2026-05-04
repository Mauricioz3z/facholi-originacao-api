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
            GROUP BY c.id, c.nome, cat.nome
            ORDER BY c.nome, cat.nome";

        var result = await conn.QueryAsync(sql, parameters);
        return Ok(result);
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
