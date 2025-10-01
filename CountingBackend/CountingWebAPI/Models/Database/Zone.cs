namespace CountingWebAPI.Models.Database
{
    public class Zone
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ZoneCamera
    {
        public int ZoneId { get; set; }
        public int CameraId { get; set; }
    }
}