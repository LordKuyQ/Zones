using ZoneHydrantEditor.Helpers;

namespace TestDbApp.Models;

public partial class Ewss
{
    public int MarkerId => Utility.GetStableMarkerId(EwsId);
    public double LatitudeD => (double)(EwsGeoCoordX ?? 0);
    public double LongitudeD => (double)(EwsGeoCoordY ?? 0);
    public int? ZoneId { get; set; }
    public string ZoneName { get; set; } = "Не в зоне";
    public string StatusName { get; set; } = "Непроверенный";
    public string OrganizationName { get; set; } = "";
    public string AddressText { get; set; } = "";
    public string PipeInfo { get; set; } = "";
    public string DisplayNumber { get; set; } = "";
}
