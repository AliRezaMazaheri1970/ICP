namespace Domain.Models; // یا Domain.Entities اگر راحت‌ترید

public class ProjectSettings
{
    public bool AutoQualityControl { get; set; } = true;

    public double? MinAcceptableWeight { get; set; }
    public double? MaxAcceptableWeight { get; set; }

    public double? MinAcceptableVolume { get; set; }
    public double? MaxAcceptableVolume { get; set; }

    public double? MinDilutionFactor { get; set; }
    public double? MaxDilutionFactor { get; set; }
}