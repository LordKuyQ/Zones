namespace ZoneHydrantEditor.Models
{
    public class BindingInfo
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceX { get; set; }
        public double DistanceY { get; set; }
        public int HydrantId { get; set; }
        public string HydrantNumber { get; set; } = "";
        public string HydrantTruba { get; set; } = "";
    }
}