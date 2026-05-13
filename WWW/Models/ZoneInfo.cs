namespace ZoneHydrantEditor
{
    public class ZoneInfo
    {
        public int ZoneId { get; set; }
        public string ZoneName { get; set; } = "";
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
        public int HydrantCount { get; set; }
        public bool HasValidBounds { get; set; }
    }
}