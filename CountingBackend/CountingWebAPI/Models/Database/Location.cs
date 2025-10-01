namespace CountingWebAPI.Models.Database
{
    public class Location
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class LocationZone
    {
        public int LocationId { get; set; }
        public int ZoneId { get; set; }
    }
}