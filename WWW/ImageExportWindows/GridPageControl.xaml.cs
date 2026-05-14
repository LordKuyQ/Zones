using GMap.NET;
using GMap.NET.WindowsPresentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TestDbApp.Models;
using ZoneHydrantEditor.GraphicElements;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor
{
    public partial class GridPageControl : UserControl
    {
        private const int Columns = 4;
        private const int Rows = 3;
        private const int CellWidth = 280;
        private const int CellHeight = 260;
        private readonly bool _isExportMode;

        public GridPageControl(bool isExportMode = false)
        {
            InitializeComponent();
            _isExportMode = isExportMode;

            if (_isExportMode)
            {
                SnapsToDevicePixels = true;
                UseLayoutRounding = true;
                Background = Brushes.White;
                CacheMode = new BitmapCache();
            }
        }

        public void AddCell(GridCellData cell, int index)
        {
            int col = index % Columns;
            int row = index / Columns;
            double x = col * CellWidth + 3;
            double y = row * CellHeight + 3;

            var cellBorder = new Border
            {
                Width = CellWidth - 12,
                Height = CellHeight - 12,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                CacheMode = _isExportMode ? new BitmapCache() : null
            };
            Canvas.SetLeft(cellBorder, x);
            Canvas.SetTop(cellBorder, y);
            CellsCanvas.Children.Add(cellBorder);

            var cellContent = CreateCellContent(cell);
            Canvas.SetLeft(cellContent, x);
            Canvas.SetTop(cellContent, y);
            CellsCanvas.Children.Add(cellContent);
        }

        private FrameworkElement CreateCellContent(GridCellData cell)
        {
            var grid = new Grid
            {
                Width = CellWidth - 12,
                Height = CellHeight - 12,
                CacheMode = _isExportMode ? new BitmapCache() : null
            };

            string titleText = string.IsNullOrWhiteSpace(cell.HydrantNumber) ? "Без номера" : cell.HydrantNumber;
            if (!string.IsNullOrWhiteSpace(cell.HydrantTruba))
                titleText += $" ({cell.HydrantTruba})";

            var title = new TextBlock
            {
                Text = titleText,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(5, 2, 5, 2),
                Height = 20,
                Margin = new Thickness(0, 2, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = CellWidth - 30
            };

            var miniMap = new GMapControl
            {
                Width = CellWidth - 30,
                Height = CellHeight - 55,
                CanDragMap = false,
                ShowCenter = false,
                MouseWheelZoomEnabled = false,
                MinZoom = 16,
                MaxZoom = 16,
                Margin = new Thickness(5, 5, 5, 20),
                DisableAltForSelection = true,
                CacheMode = _isExportMode ? new BitmapCache() : null,
            };
            miniMap.Loaded += (s, e) =>
            {
                miniMap.MapProvider = MBTilesProvider.Instance;
                miniMap.Position = new PointLatLng(cell.Latitude, cell.Longitude);
                miniMap.Zoom = 16;
                miniMap.Markers.Add(CreateMiniMapMarker(cell));
                if (!string.IsNullOrEmpty(cell.EwsPriviazka))
                {
                    var coord = Utility.ParseBindingCoord(cell.EwsPriviazka);
                    if (coord != null)
                        miniMap.Markers.Add(CreateMiniMapBinding(coord.Value.lat, coord.Value.lng, cell.EwsId));
                }
            };

            var overlay = CreateOverlayCanvas(cell);
            var container = new Grid();
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(title, 0);
            container.Children.Add(title);

            var mapOverlay = new Grid();
            mapOverlay.Children.Add(miniMap);
            mapOverlay.Children.Add(overlay);
            Grid.SetRow(mapOverlay, 1);
            container.Children.Add(mapOverlay);

            grid.Children.Add(container);
            return grid;
        }

        private Canvas CreateOverlayCanvas(GridCellData cell)
        {
            var canvas = new Canvas
            {
                Width = CellWidth - 30,
                Height = CellHeight - 55,
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };

            double size = Math.Min(CellWidth, CellHeight) * 0.2;

            var crossBorder = new Border
            {
                Width = size,
                Height = size,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = CreateCrossCanvas(cell, size)
            };

            Canvas.SetLeft(crossBorder, 10);
            Canvas.SetTop(crossBorder, 25);
            canvas.Children.Add(crossBorder);
            Canvas.SetLeft(canvas, 5);
            Canvas.SetTop(canvas, 5);

            return canvas;
        }

        private static Canvas CreateCrossCanvas(GridCellData cell, double size)
        {
            var canvas = new Canvas { Width = size, Height = size, SnapsToDevicePixels = true };
            double cx = size / 2, cy = size / 2, arm = size * 0.35;

            canvas.Children.Add(new Line { X1 = cx, Y1 = cy - arm, X2 = cx, Y2 = cy + arm, Stroke = Brushes.Black, StrokeThickness = 2, SnapsToDevicePixels = true });
            canvas.Children.Add(new Line { X1 = cx - arm, Y1 = cy, X2 = cx + arm, Y2 = cy, Stroke = Brushes.Black, StrokeThickness = 2, SnapsToDevicePixels = true });

            if (!string.IsNullOrEmpty(cell.EwsPriviazka))
            {
                var coord = Utility.ParseBindingCoord(cell.EwsPriviazka);
                if (coord != null)
                {
                    canvas.Children.Add(new Rectangle { Width = 6, Height = 6, Fill = Brushes.Black, Stroke = Brushes.White, StrokeThickness = 1, SnapsToDevicePixels = true });
                    Canvas.SetLeft(canvas.Children[^1], cx - 3);
                    Canvas.SetTop(canvas.Children[^1], cy - 3);

                    double offsetX = Math.Max(-arm, Math.Min(arm, -Utility.ParseBindingDistance(cell.EwsPriviazkaGeoY) * 0.05));
                    double offsetY = Math.Max(-arm, Math.Min(arm, Utility.ParseBindingDistance(cell.EwsPriviazkaGeoX) * 0.05));

                    var dot = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(15, 11, 227)), Stroke = Brushes.White, StrokeThickness = 1, SnapsToDevicePixels = true };
                    Canvas.SetLeft(dot, cx + offsetX - 3);
                    Canvas.SetTop(dot, cy + offsetY - 3);
                    canvas.Children.Add(dot);

                    AddDistanceInfo(canvas, cell, cx, cy, arm);
                }
            }
            return canvas;
        }

        private static void AddDistanceInfo(Canvas canvas, GridCellData cell, double cx, double cy, double arm)
        {
            double dx = Utility.ParseBindingDistance(cell.EwsPriviazkaGeoX);
            double dy = Utility.ParseBindingDistance(cell.EwsPriviazkaGeoY);

            AddDistanceText(canvas, Math.Abs(dx), cx - 8, dx > 0 ? cy + arm + 2 : cy - arm - 12);
            AddDistanceText(canvas, Math.Abs(dy), dy > 0 ? cx - arm - 20 : cx + arm + 2, cy - 8);

            if (dx > 0) AddArrow(canvas, new[] { new Point(cx - 3, cy + arm - 2), new Point(cx + 3, cy + arm - 2), new Point(cx, cy + arm + 2) });
            else if (dx < 0) AddArrow(canvas, new[] { new Point(cx - 3, cy - arm + 2), new Point(cx + 3, cy - arm + 2), new Point(cx, cy - arm - 2) });

            if (dy > 0) AddArrow(canvas, new[] { new Point(cx - arm + 2, cy - 3), new Point(cx - arm + 2, cy + 3), new Point(cx - arm - 2, cy) });
            else if (dy < 0) AddArrow(canvas, new[] { new Point(cx + arm - 2, cy - 3), new Point(cx + arm - 2, cy + 3), new Point(cx + arm + 2, cy) });
        }

        private static void AddDistanceText(Canvas canvas, double value, double x, double y)
        {
            if (value <= 0) return;
            var text = new TextBlock
            {
                Text = $"{value:F0}",
                FontSize = 7,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2, 1, 2, 1),
                SnapsToDevicePixels = true
            };
            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, y);
            canvas.Children.Add(text);
        }

        private static void AddArrow(Canvas canvas, Point[] points)
        {
            canvas.Children.Add(new Polygon { Points = new PointCollection(points), Fill = Brushes.Black, Stroke = Brushes.White, StrokeThickness = 1, SnapsToDevicePixels = true });
        }

        private static GMapMarker CreateMiniMapMarker(GridCellData cell)
        {
            var ewss = new Ewss
            {
                EwsId = cell.EwsId,
                EwsNumber = cell.HydrantNumber,
                EwsGeoCoordX = (decimal)cell.Latitude,
                EwsGeoCoordY = (decimal)cell.Longitude
            };
            ewss.StatusName = cell.Status;
            ewss.PipeInfo = cell.HydrantTruba;
            ewss.DisplayNumber = cell.HydrantNumber;
            return HydrantMarker.CreateSimpleMarker(ewss);
        }

        private static GMapMarker CreateMiniMapBinding(double lat, double lng, string ewsId) => BindingMarker.CreateSimpleBinding(lat, lng, ewsId);
    }
}
