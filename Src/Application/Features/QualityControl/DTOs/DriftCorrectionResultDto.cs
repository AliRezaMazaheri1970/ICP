namespace Application.Features.QualityControl.DTOs;

public class DriftCorrectionResultDto
{
    public string? ElementName { get; set; }
    public int SamplesCorrected { get; set; }
    public double AverageDriftFactor { get; set; }
}