namespace PrecoBoi.Api.Models;

public class Auditoria
{
    public int Id { get; set; }
    public string Tabela { get; set; } = string.Empty;
    public int? RegistroId { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public string Descricao { get; set; } = string.Empty;
}
