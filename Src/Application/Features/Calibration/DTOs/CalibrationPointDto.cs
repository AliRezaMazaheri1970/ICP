namespace Application.Features.Calibration.DTOs;

public class CalibrationPointDto
{
    public double Concentration { get; set; }
    public double Intensity { get; set; }
    public bool IsExcluded { get; set; }
}