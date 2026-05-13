using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class Unit
{
    public string? UnitId { get; set; }

    public string? UnitCityCod { get; set; }

    public string? UnitDistrictCod { get; set; }

    public string? UnitFullName { get; set; }

    public string? UnitShortName { get; set; }

    public string? UnitParentCod { get; set; }
}
