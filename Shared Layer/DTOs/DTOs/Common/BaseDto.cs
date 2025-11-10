namespace Shared.Icp.DTOs.Common
{
    /// <summary>
    /// کلاس پایه برای تمام DTOs
    /// </summary>
    public abstract class BaseDto
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}