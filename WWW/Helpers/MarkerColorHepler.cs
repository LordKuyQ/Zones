using System.Windows.Media;

namespace ZoneHydrantEditor.Helpers
{
    public static class MarkerColorHelper
    {
        public static (Color fill, Color stroke) GetColorsForStatus(string status)
        {
            return status switch
            {
                "неисправен" => (
                    fill: Color.FromRgb(220, 20, 20),   // красный
                    stroke: Color.FromRgb(140, 0, 0)
                ),
                "не найден" or "не существует" => (
                    fill: Color.FromRgb(255, 69, 0),    // оранжево-красный
                    stroke: Color.FromRgb(200, 50, 0)
                ),
                "требует проверки" => (
                    fill: Color.FromRgb(169, 169, 169), // серый
                    stroke: Color.FromRgb(105, 105, 105)
                ),
                "исправен" => (
                    fill: Color.FromRgb(15, 11, 227),   // синий
                    stroke: Color.FromRgb(14, 12, 168)
                ),
                _ => (
                    fill: Color.FromRgb(169, 169, 169), // серый (для неизвестных статусов)
                    stroke: Color.FromRgb(105, 105, 105)
                )
            };
        }
    }
}