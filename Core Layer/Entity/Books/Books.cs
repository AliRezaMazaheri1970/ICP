using Core.Entity.Base;

namespace Core.Entity.Books
{
    public class Book : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public int PublishYear { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }
}