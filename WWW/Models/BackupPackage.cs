using TestDbApp.Models;

namespace ZoneHydrantEditor.Models
{
    [Serializable]
    public class BackupPackage
    {
        public DateTime BackupDate { get; set; }
        public string BackupDescription { get; set; }
        public List<ZoneBackupData> Zones { get; set; } = new();
        public List<EwssBackupData> Hydrants { get; set; } = new();
    }

    [Serializable]
    public class EwssBackupData
    {
        public string EwsId { get; set; }
        public string EwsNumber { get; set; }
        public decimal? EwsGeoCoordX { get; set; }
        public decimal? EwsGeoCoordY { get; set; }
        public string EwsTypeCod { get; set; }
        public string EwsPipeTypeCod { get; set; }
        public string EwsDiameterCod { get; set; }
        public string EwsPkdiameterCod { get; set; }
        public string EwsAdressObjectCod { get; set; }
        public string EwsHouseNumber { get; set; }
        public string EwsAdressNote { get; set; }
        public string EwsOrganizationCod { get; set; }
        public string EwsStatusCod { get; set; }
        public string EwsPriviazka { get; set; }
        public string EwsPriviazkaGeoX { get; set; }
        public string EwsPriviazkaGeoY { get; set; }
        public string EwsNotes { get; set; }
        public string EwsMapId { get; set; }
        public DateTime? RecordCreated { get; set; }
        public string RecordUserCod { get; set; }
        public string RecordStatus { get; set; }
        public string EwsFireUnitCod { get; set; }

        public static EwssBackupData FromEwss(Ewss e) => new()
        {
            EwsId = e.EwsId,
            EwsNumber = e.EwsNumber,
            EwsGeoCoordX = e.EwsGeoCoordX,
            EwsGeoCoordY = e.EwsGeoCoordY,
            EwsTypeCod = e.EwsTypeCod,
            EwsPipeTypeCod = e.EwsPipeTypeCod,
            EwsDiameterCod = e.EwsDiameterCod,
            EwsPkdiameterCod = e.EwsPkdiameterCod,
            EwsAdressObjectCod = e.EwsAdressObjectCod,
            EwsHouseNumber = e.EwsHouseNumber,
            EwsAdressNote = e.EwsAdressNote,
            EwsOrganizationCod = e.EwsOrganizationCod,
            EwsStatusCod = e.EwsStatusCod,
            EwsPriviazka = e.EwsPriviazka,
            EwsPriviazkaGeoX = e.EwsPriviazkaGeoX,
            EwsPriviazkaGeoY = e.EwsPriviazkaGeoY,
            EwsNotes = e.EwsNotes,
            EwsMapId = e.EwsMapId,
            RecordCreated = e.RecordCreated,
            RecordUserCod = e.RecordUserCod,
            RecordStatus = e.RecordStatus,
            EwsFireUnitCod = e.EwsFireUnitCod
        };

        public Ewss ToEwss() => new()
        {
            EwsId = EwsId,
            EwsNumber = EwsNumber,
            EwsGeoCoordX = EwsGeoCoordX,
            EwsGeoCoordY = EwsGeoCoordY,
            EwsTypeCod = EwsTypeCod,
            EwsPipeTypeCod = EwsPipeTypeCod,
            EwsDiameterCod = EwsDiameterCod,
            EwsPkdiameterCod = EwsPkdiameterCod,
            EwsAdressObjectCod = EwsAdressObjectCod,
            EwsHouseNumber = EwsHouseNumber,
            EwsAdressNote = EwsAdressNote,
            EwsOrganizationCod = EwsOrganizationCod,
            EwsStatusCod = EwsStatusCod,
            EwsPriviazka = EwsPriviazka,
            EwsPriviazkaGeoX = EwsPriviazkaGeoX,
            EwsPriviazkaGeoY = EwsPriviazkaGeoY,
            EwsNotes = EwsNotes,
            EwsMapId = EwsMapId,
            RecordCreated = RecordCreated,
            RecordUserCod = RecordUserCod,
            RecordStatus = RecordStatus,
            EwsFireUnitCod = EwsFireUnitCod
        };
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
