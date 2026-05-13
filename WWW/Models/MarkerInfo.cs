namespace ZoneHydrantEditor.Models
{
    public class MarkerInfo
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string GidrantNumber { get; set; } = "";
        public string GidrantTruba { get; set; } = "";
        public string GidrantAdres { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Status { get; set; } = "Непроверенный";
        public string BreakReason { get; set; } = "";
        public int? ZoneId { get; set; }
        public string ZoneName { get; set; } = "Не в зоне";
    }
}