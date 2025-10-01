namespace CountingWebAPI.Models.Database
{
    public class Setting
    {
        public int Id { get; set; } // Still useful for ADO.NET if you query by ID, though Name is the key
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
        public bool IsVisible { get; set; } = true;
        public int SortOrder { get; set; }
    }
}