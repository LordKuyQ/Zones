using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class КопияEwssCheck
{
    public string? CheckId { get; set; }

    public string? CheckCityCod { get; set; }

    public string? CheckUnitCod { get; set; }

    public string? CheckEwsCod { get; set; }

    public DateTime? CheckDate { get; set; }

    public string? CheckCheckTypeCod { get; set; }

    public string? CheckFio { get; set; }

    public string? CheckStatus { get; set; }

    public string? CheckTroubleList { get; set; }

    public string? CheckFixAction { get; set; }

    public string? CheckNotes { get; set; }

    public DateTime? RecordCreated { get; set; }

    public DateTime? RecordModyfied { get; set; }

    public string? RecordUser { get; set; }

    public string? RecordComputer { get; set; }

    public string? RecordStatus { get; set; }

    public string? RecordNote { get; set; }
}
