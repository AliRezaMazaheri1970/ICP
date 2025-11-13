using Core.Icp.Domain.Enums;
namespace Shared.Icp.DTOs.Reports
{
    public class TemplateParameter
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ParameterType Type { get; set; }
        public bool IsRequired { get; set; }
        public object? DefaultValue { get; set; }
        public List<string>? AllowedValues { get; set; }
    }
}