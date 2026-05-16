using GMap.NET.WindowsPresentation;
using System.Windows;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor
{
    public partial class OverviewCaptureWindow : Window
    {
        public GMapControl ZoneMapControl => ZoneMap;

        public OverviewCaptureWindow()
        {
            InitializeComponent();
            InitializeMap();
        }

        private void InitializeMap()
        {
            ZoneMap.Bearing = 0;
            ZoneMap.CanDragMap = false;
            ZoneMap.DragButton = System.Windows.Input.MouseButton.Left;
            ZoneMap.MaxZoom = 18;
            ZoneMap.MinZoom = 2;
            ZoneMap.MouseWheelZoomEnabled = false;
            ZoneMap.ShowTileGridLines = false;
            ZoneMap.RetryLoadTile = 2;           // <-- было 0, исправлено
            ZoneMap.LevelsKeepInMemory = 20;      // <-- было 5, исправлено
            ZoneMap.MapProvider = MBTilesProvider.Instance;

            if (!MBTilesProvider.Instance.IsLoaded && System.IO.File.Exists("NewLoadMap.mbtiles"))
            {
                MBTilesProvider.Instance.LoadMBTilesFile("NewLoadMap.mbtiles");
            }
        }

        public void SetTitle(string zoneName, int hydrantCount)
        {
            Dispatcher.Invoke(() =>
            {
                TitleText.Text = $" Общий план: {zoneName}\nГидрантов: {hydrantCount}";
            });
        }
    }
}