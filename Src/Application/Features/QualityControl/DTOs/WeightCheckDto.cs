namespace Application.Features.QualityControl.DTOs;

public class WeightCheckDto
{
    public Guid Id { get; set; }
    public string? SolutionLabel { get; set; }
    public double Weight { get; set; }
}