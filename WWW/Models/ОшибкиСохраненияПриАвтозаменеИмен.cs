using System;
using System.Collections.Generic;

namespace TestDbApp.Models;

public partial class ОшибкиСохраненияПриАвтозаменеИмен
{
    public byte[]? ИмяОбъекта { get; set; }

    public byte[]? ТипОбъекта { get; set; }

    public byte[]? ПричинаОшибки { get; set; }

    public byte[]? Время { get; set; }
}
