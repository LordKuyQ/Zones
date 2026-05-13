using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class Street
{
    public string? StreetId { get; set; }

    public string? StreetPrefixCod { get; set; }

    public string? StreetName { get; set; }

    public string? StreetTerritorialUnitCod { get; set; }

    public string? StreetMunicipalFormationCod { get; set; }

    public string? StreetMunicipalityOfRegionCod { get; set; }

    public string? StreetRegionCod { get; set; }
}
