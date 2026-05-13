namespace ZoneHydrantEditor.Models
{
    public class ZonePoint
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public int OrderIndex { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
