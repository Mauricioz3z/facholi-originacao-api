namespace PrecoBoi.Api.Models;

// Conferência administrativa da recepção, 1:1 com um embarque.
public class EmbarqueConferencia
{
    public int Id { get; set; }
    public int EmbarqueId { get; set; }
    public string Status { get; set; } = "EmAndamento"; // EmAndamento, Finalizada

    public decimal? ValorTotalNegociacao { get; set; }
    public decimal? ValorTotalIcms { get; set; }
    public decimal? ComissaoCb { get; set; }
    public decimal? IcmsCb { get; set; }
    public decimal? FreteCb { get; set; }
    public decimal? DespesaCb { get; set; }

    public decimal? RsCb { get; set; }
    public decimal? TotalFinalCb { get; set; }
    public decimal? RsKgNegociacao { get; set; }
    public decimal? RsKgColocado { get; set; }
    public decimal? PercentualQuebraDesvio { get; set; }

    public string? ObservacaoOcorrencias { get; set; }
    public DateTime? FinalizadaEm { get; set; }
    public int? FinalizadaPor { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime? AtualizadoEm { get; set; }
}
