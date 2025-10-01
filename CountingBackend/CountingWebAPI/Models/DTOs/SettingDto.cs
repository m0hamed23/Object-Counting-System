namespace CountingWebAPI.Models.DTOs
{
    public class SettingDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
        public bool IsVisible { get; set; }
    }
}

//