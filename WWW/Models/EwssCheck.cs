using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class EwssCheck
{
    public DateTime? RecordCreated { get; set; }

    public string? RecordUserCod { get; set; }

    public string? RecordNote { get; set; }

    public string? CheckId { get; set; }

    public DateTime? CheckDate { get; set; }

    public string? CheckEwsCod { get; set; }

    public string? CheckCheckTypeCod { get; set; }

    public string? CheckStaffCod { get; set; }

    public string? CheckStatusCod { get; set; }

    public string? CheckTroubleCod { get; set; }

    public string? CheckFixActionCod { get; set; }

    public string? CheckNotes { get; set; }
}
