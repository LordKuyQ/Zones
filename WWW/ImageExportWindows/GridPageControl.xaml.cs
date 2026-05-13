using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ZoneHydrantEditor.GraphicElements;
using ZoneHydrantEditor.Helpers;
using ZoneHydrantEditor.Models;

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

        public void AddCell(GridCellData cell, Dictionary<int, List<BindingInfo>> bindingCache, int index)
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

            var cellContent = CreateCellContent(cell, bindingCache);
            Canvas.SetLeft(cellContent, x);
            Canvas.SetTop(cellContent, y);
            CellsCanvas.Children.Add(cellContent);
        }

        private FrameworkElement CreateCellContent(GridCellData cell, Dictionary<int, List<BindingInfo>> bindingCache)
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
                MapProvider = MBTilesProvider.Instance,
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
                Position = new PointLatLng(cell.Latitude, cell.Longitude),
                Zoom = 16
            };

            miniMap.Markers.Add(CreateMiniMapMarker(cell));

            if (bindingCache.TryGetValue(cell.HydrantId, out var bindings))
                foreach (var binding in bindings)
                    miniMap.Markers.Add(CreateMiniMapBinding(binding));

            var overlay = CreateOverlayCanvas(cell, bindingCache);
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

        private static Canvas CreateOverlayCanvas(GridCellData cell, Dictionary<int, List<BindingInfo>> bindingCache)
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
                Child = CreateCrossCanvas(cell, bindingCache, size)
            };

            Canvas.SetLeft(crossBorder, 10);
            Canvas.SetTop(crossBorder, 25);
            canvas.Children.Add(crossBorder);
            Canvas.SetLeft(canvas, 5);
            Canvas.SetTop(canvas, 5);

            return canvas;
        }

        private static Canvas CreateCrossCanvas(GridCellData cell, Dictionary<int, List<BindingInfo>> bindingCache, double size)
        {
            var canvas = new Canvas { Width = size, Height = size, SnapsToDevicePixels = true };
            double cx = size / 2, cy = size / 2, arm = size * 0.35;


            canvas.Children.Add(new Line { X1 = cx, Y1 = cy - arm, X2 = cx, Y2 = cy + arm, Stroke = Brushes.Black, StrokeThickness = 2, SnapsToDevicePixels = true });
            canvas.Children.Add(new Line { X1 = cx - arm, Y1 = cy, X2 = cx + arm, Y2 = cy, Stroke = Brushes.Black, StrokeThickness = 2, SnapsToDevicePixels = true });

            if (bindingCache.TryGetValue(cell.HydrantId, out var bindings) && bindings.Count > 0)
            {
                var binding = bindings.First();

                canvas.Children.Add(new Rectangle { Width = 6, Height = 6, Fill = Brushes.Black, Stroke = Brushes.White, StrokeThickness = 1, SnapsToDevicePixels = true });
                Canvas.SetLeft(canvas.Children[^1], cx - 3);
                Canvas.SetTop(canvas.Children[^1], cy - 3);

                double offsetX = Math.Max(-arm, Math.Min(arm, -binding.DistanceY * 0.05));
                double offsetY = Math.Max(-arm, Math.Min(arm, binding.DistanceX * 0.05));

                var dot = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(15, 11, 227)), Stroke = Brushes.White, StrokeThickness = 1, SnapsToDevicePixels = true };
                Canvas.SetLeft(dot, cx + offsetX - 3);
                Canvas.SetTop(dot, cy + offsetY - 3);
                canvas.Children.Add(dot);

                AddDistanceInfo(canvas, binding, cx, cy, arm);
            }
            return canvas;
        }

        private static void AddDistanceInfo(Canvas canvas, BindingInfo binding, double cx, double cy, double arm)
        {
            AddDistanceText(canvas, Math.Abs(binding.DistanceX), cx - 8, binding.DistanceX > 0 ? cy + arm + 2 : cy - arm - 12);
            AddDistanceText(canvas, Math.Abs(binding.DistanceY), binding.DistanceY > 0 ? cx - arm - 20 : cx + arm + 2, cy - 8);

            if (binding.DistanceX > 0) AddArrow(canvas, new[] { new Point(cx - 3, cy + arm - 2), new Point(cx + 3, cy + arm - 2), new Point(cx, cy + arm + 2) });
            else if (binding.DistanceX < 0) AddArrow(canvas, new[] { new Point(cx - 3, cy - arm + 2), new Point(cx + 3, cy - arm + 2), new Point(cx, cy - arm - 2) });

            if (binding.DistanceY > 0) AddArrow(canvas, new[] { new Point(cx - arm + 2, cy - 3), new Point(cx - arm + 2, cy + 3), new Point(cx - arm - 2, cy) });
            else if (binding.DistanceY < 0) AddArrow(canvas, new[] { new Point(cx + arm - 2, cy - 3), new Point(cx + arm - 2, cy + 3), new Point(cx + arm + 2, cy) });
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

        private static GMapMarker CreateMiniMapMarker(GridCellData cell) =>
            HydrantMarker.CreateSimpleMarker(new MarkerInfo
            {
                Id = cell.HydrantId,
                Latitude = cell.Latitude,
                Longitude = cell.Longitude,
                GidrantNumber = cell.HydrantNumber,
                GidrantTruba = cell.HydrantTruba,
                Status = cell.Status,
                BreakReason = cell.BreakReason
            });

        private static GMapMarker CreateMiniMapBinding(BindingInfo binding) => BindingMarker.CreateSimpleBinding(binding);
    }
}