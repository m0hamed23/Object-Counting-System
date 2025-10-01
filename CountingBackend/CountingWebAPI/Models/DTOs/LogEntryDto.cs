namespace CountingWebAPI.Models.DTOs
{
    public class LogEntryDto
    {
        public int Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string Event { get; set; } = string.Empty;
        public string? ImageName { get; set; }
        public int? CameraIndex { get; set; }
    }
}

//