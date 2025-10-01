namespace CountingWebAPI.Models.DTOs
{
    public class ObjectCountDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CameraCountDto
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public int TotalTrackedCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class LocationCountDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
    }
}