using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class КопияEwss
{
    public string? EwsArchiveId { get; set; }
    public string? EwsMapId { get; set; }

    public string? EwsCityMunicipalityOfRegionCod { get; set; }

    public string? EwsUnitCod { get; set; }

    public string? EwsCod { get; set; }

    public string? EwsDistrictCod { get; set; }

    public string? EwsCoordX { get; set; }

    public string? EwsCoordY { get; set; }

    public decimal? EwsGeoCoordX { get; set; }

    public decimal? EwsGeoCoordY { get; set; }

    public string? EwsStreetCod { get; set; }

    public string? EwsBuilding { get; set; }

    public string? EwsType { get; set; }

    public string? EwsNumber { get; set; }

    public string? EwsOrganizationCod { get; set; }

    public string? EwsPipeType { get; set; }

    public string? EwsDiameterCod { get; set; }

    public string? EwsPkdiameter { get; set; }

    public string? EwsValue { get; set; }

    public string? EwsPacount { get; set; }

    public string? EwsDirLeft { get; set; }

    public string? EwsDirRight { get; set; }

    public string? EwsDirStright { get; set; }

    public string? EwsPriviazka { get; set; }

    public string? EwsStatus { get; set; }

    public string? EwsNotes { get; set; }

    public DateTime? RecordCreated { get; set; }

    public DateTime? RecordModyfied { get; set; }

    public string? RecordUser { get; set; }

    public string? RecordComputer { get; set; }

    public string? RecordStatus { get; set; }

    public string? RecordNote { get; set; }

    public string? EwsFireUnitCod { get; set; }

    public string? EwsStatusCod { get; set; }

    public string? EwsAdressObjectCod { get; set; }

    public string? EwsHouseNumber { get; set; }

    public string? EwsAdressNote { get; set; }

    public string? EwsPipeTypeCod { get; set; }

    public string? EwsPkdiameterCod { get; set; }

    public string? EwsValueCod { get; set; }

    public string? EwsPrLeft { get; set; }

    public string? EwsPrRight { get; set; }

    public string? EwsPrStright { get; set; }

    public string? EwsPriviazkaGeoX { get; set; }

    public string? EwsPriviazkaGeoY { get; set; }

    public string? RecordUserCod { get; set; }

    public string? EwsTypeCod { get; set; }

    public string? ChangeDate { get; set; }

    public string? ChangeDescription { get; set; }
}
