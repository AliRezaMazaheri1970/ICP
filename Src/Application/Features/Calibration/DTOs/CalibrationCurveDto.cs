namespace Application.Features.Calibration.DTOs;

public class CalibrationCurveDto
{
    public Guid Id { get; set; }
    public string ElementName { get; set; } = string.Empty;

    public double Slope { get; set; }
    public double Intercept { get; set; }
    public double RSquared { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<CalibrationPointDto> Points { get; set; } = new();
}