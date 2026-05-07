using Dapper;
using PrecoBoi.Api.Models;

namespace PrecoBoi.Api.Repositories;

public class AuditoriaRepository : BaseRepository
{
    public AuditoriaRepository(IConfiguration config) : base(config) { }

    public async Task Registrar(string tabela, int? registroId, string campo,
        string? valorAnterior, string? valorNovo, int? usuarioId, string usuarioNome, string descricao)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"INSERT INTO auditoria (tabela, registro_id, campo, valor_anterior, valor_novo,
              usuario_id, usuario_nome, data_hora, descricao)
              VALUES (@Tabela, @RegistroId, @Campo, @ValorAnterior, @ValorNovo,
              @UsuarioId, @UsuarioNome, @DataHora, @Descricao)",
            new
            {
                Tabela = tabela, RegistroId = registroId, Campo = campo,
                ValorAnterior = valorAnterior, ValorNovo = valorNovo,
                UsuarioId = usuarioId, UsuarioNome = usuarioNome,
                DataHora = DateTime.Now, Descricao = descricao
            });
    }

    public async Task<(IEnumerable<Auditoria> Items, int Total)> Listar(
        string? tabela, int? usuarioId, DateTime? dataInicio, DateTime? dataFim, int pagina, int tamanhoPagina)
    {
        using var conn = CreateConnection();
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(tabela)) { where.Add("tabela=@Tabela"); p.Add("Tabela", tabela); }
        if (usuarioId.HasValue) { where.Add("usuario_id=@UsuarioId"); p.Add("UsuarioId", usuarioId); }
        if (dataInicio.HasValue) { where.Add("data_hora >= @DataInicio"); p.Add("DataInicio", dataInicio); }
        if (dataFim.HasValue) { where.Add("data_hora <= @DataFim"); p.Add("DataFim", dataFim.Value.AddDays(1)); }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var offset = (pagina - 1) * tamanhoPagina;
        p.Add("Limit", tamanhoPagina);
        p.Add("Offset", offset);

        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM auditoria {whereClause}", p);
        var items = await conn.QueryAsync<Auditoria>(
            $"SELECT * FROM auditoria {whereClause} ORDER BY data_hora DESC LIMIT @Limit OFFSET @Offset", p);

        return (items, total);
    }
}
