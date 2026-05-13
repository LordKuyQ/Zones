using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class UserSetting
{
    public string? UserSettingId { get; set; }

    public string? UserSettingCaption { get; set; }

    public string? UserSettingValue { get; set; }
}
