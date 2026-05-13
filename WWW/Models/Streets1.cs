using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class Streets1
{
    public byte[]? StreetId { get; set; }

    public byte[]? StreetPrefixCod { get; set; }

    public byte[]? StreetName { get; set; }

    public byte[]? StreetTerritorialUnitCod { get; set; }

    public byte[]? StreetMunicipalFormationCod { get; set; }

    public byte[]? StreetMunicipalityOfRegionCod { get; set; }

    public byte[]? StreetRegionCod { get; set; }
}
