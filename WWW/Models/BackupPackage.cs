namespace ZoneHydrantEditor.Models
{
    [Serializable]
    public class BackupPackage
    {
        public DateTime BackupDate { get; set; }
        public string BackupDescription { get; set; }
        public List<ZoneBackupData> Zones { get; set; } = new();
        public List<MarkerInfo> Hydrants { get; set; } = new();
        public List<BindingInfo> Bindings { get; set; } = new();
    }

    [Serializable]
    public class ZoneBackupData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<PointLatLngBackup> Points { get; set; } = new();
    }

    [Serializable]
    public class PointLatLngBackup
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public PointLatLngBackup(double lat, double lng)
        {
            Lat = lat;
            Lng = lng;
        }
    }
}