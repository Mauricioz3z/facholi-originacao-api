using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PrecoBoi.Api.Controllers;

[ApiController]
[Route("debug")]
public class DebugController : ControllerBase
{
    private readonly IConfiguration _config;

    public DebugController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("tz")]
    [AllowAnonymous]
    public async Task<IActionResult> Tz()
    {
        var connStr = _config.GetConnectionString("DefaultConnection")!;
        using var conn = new NpgsqlConnection(connStr);
        var dbInfo = await conn.QuerySingleAsync<dynamic>(
            "SELECT current_setting('TIMEZONE') AS tz, NOW() AS now_pg, CURRENT_TIMESTAMP AS current_ts, LOCALTIMESTAMP AS local_ts");

        return Ok(new
        {
            DotNet = new
            {
                Now = DateTime.Now,
                NowKind = DateTime.Now.Kind.ToString(),
                UtcNow = DateTime.UtcNow,
                LocalTzId = TimeZoneInfo.Local.Id,
                LocalTzDisplay = TimeZoneInfo.Local.DisplayName,
                BaseUtcOffset = TimeZoneInfo.Local.BaseUtcOffset.ToString(),
                EnvTz = Environment.GetEnvironmentVariable("TZ") ?? "(não definida)"
            },
            Postgres = new
            {
                Timezone = (string)dbInfo.tz,
                NowPg = (DateTime)dbInfo.now_pg,
                CurrentTimestamp = (DateTime)dbInfo.current_ts,
                LocalTimestamp = (DateTime)dbInfo.local_ts
            }
        });
    }
}
