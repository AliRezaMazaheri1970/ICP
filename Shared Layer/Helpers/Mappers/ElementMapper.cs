using Core.Icp.Domain.Entities.Elements;
using Shared.Icp.DTOs.Elements;

namespace Shared.Icp.Helpers.Mappers
{
    /// <summary>
    /// Mapper برای Element
    /// </summary>
    public static class ElementMapper
    {
        /// <summary>
        /// Entity به DTO
        /// </summary>
        public static ElementDto ToDto(this Element element)
        {
            return new ElementDto
            {
                Id = element.Id,
                Symbol = element.Symbol,
                Name = element.Name,
                AtomicNumber = element.AtomicNumber,
                AtomicMass = element.AtomicMass,
                IsActive = element.IsActive,
                DisplayOrder = element.DisplayOrder,
                IsotopeCount = element.Isotopes?.Count ?? 0,
                CreatedAt = element.CreatedAt,
                UpdatedAt = element.UpdatedAt
            };
        }

        /// <summary>
        /// لیست Entity به لیست DTO
        /// </summary>
        public static IEnumerable<ElementDto> ToDtoList(this IEnumerable<Element> elements)
        {
            return elements.Select(e => e.ToDto());
        }
    }
}