namespace ZoneHydrantEditor.Models
{
    public class ZoneBackup
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public DateTime BackupDate { get; set; }
        public string? ZoneName { get; set; }
        public string? BackupReason { get; set; }
    }

    public class ZoneBackupPoint
    {
        public int Id { get; set; }
        public int BackupId { get; set; }
        public int OrderIndex { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
