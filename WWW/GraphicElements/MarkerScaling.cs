namespace ZoneHydrantEditor.GraphicElements
{
    internal  class MarkerScaling
    {
        public static double GetMarkerScale(int zoomLevel)
        {
            return zoomLevel switch
            {
                <= 10 => 0.5,  
                11 => 0.6,
                12 => 0.7,
                13 => 0.8,
                14 => 0.9,
                15 => 1.0,      
                16 => 1.1,
                17 => 1.2,
                18 => 1.3,
                >= 19 => 1.4   
            };
        }
        public static double GetTextScale(int zoomLevel)
        {
            return zoomLevel switch
            {
                <= 12 => 0.7,   
                13 => 0.8,
                14 => 0.9,
                15 => 1.0,     
                16 => 1.0,
                17 => 1.0,
                18 => 1.0,
                _ => 1.0
            };
        }
        public static bool ShouldShowText(int zoomLevel)
        {
            return zoomLevel >= 15;
        }
        public static double GetMarkerSize(int zoomLevel)
        {
            return zoomLevel switch
            {
                <= 10 => 8,
                11 => 10,
                12 => 12,
                13 => 14,
                14 => 16,
                15 => 16,      
                16 => 18,
                17 => 20,
                18 => 22,
                >= 19 => 24
            };
        }
        public static double GetStrokeThickness(int zoomLevel)
        {
            return zoomLevel switch
            {
                <= 12 => 1,
                13 => 1.5,
                14 => 1.5,
                15 => 2,
                16 => 2,
                17 => 2,
                18 => 2.5,
                >= 19 => 3
            };
        }
    }
}
