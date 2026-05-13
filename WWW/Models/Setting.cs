using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class Setting
{
    public string? SettingId { get; set; }

    public string? ГлавныйГород { get; set; }

    public string? SettingMainUnitCod { get; set; }

    public string? SettingMainDistrictCod { get; set; }

    public string? SettingVersion { get; set; }

    public string? SettingUseGeoCoord { get; set; }
}
