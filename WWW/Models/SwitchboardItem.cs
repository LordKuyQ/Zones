using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class SwitchboardItem
{
    public string? SwitchboardId { get; set; }

    public string? ItemNumber { get; set; }

    public string? ItemText { get; set; }

    public string? Command { get; set; }

    public string? Argument { get; set; }
}
