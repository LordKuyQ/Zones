using System.Globalization;

namespace ZoneHydrantEditor.Helpers
{
    internal static class Utility
    {
        public static int GetStableMarkerId(string? ewsId)
        {
            if (string.IsNullOrEmpty(ewsId)) return 0;
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in ewsId)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        public static (double lat, double lng)? ParseBindingCoord(string? priviazka)
        {
            if (string.IsNullOrEmpty(priviazka)) return null;
            var parts = priviazka.Split(',');
            if (parts.Length != 2) return null;
            if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
                return (lat, lng);
            return null;
        }

        public static double ParseBindingDistance(string? val) =>
            double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }
}
