// Src/Application/Interfaces/Services/IReportGenerator.cs
using Application.Features.Reports.DTOs;

public interface IReportGenerator
{
    byte[] GenerateExcel(PivotReportDto data);
}