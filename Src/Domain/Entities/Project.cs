using Domain.Common;
using System.Text.Json; // اضافه شده

namespace Domain.Entities;

public class Project : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    // تنظیمات پروژه به صورت JSON ذخیره می‌شود
    public string? SettingsJson { get; set; }

    public virtual ICollection<Sample> Samples { get; set; } = new List<Sample>();

    // --- Helper Methods ---
    public T? GetSettings<T>() where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(SettingsJson))
            return new T(); // تنظیمات پیش‌فرض

        try
        {
            return JsonSerializer.Deserialize<T>(SettingsJson);
        }
        catch
        {
            return new T();
        }
    }

    public void SetSettings<T>(T settings)
    {
        SettingsJson = JsonSerializer.Serialize(settings);
    }
}