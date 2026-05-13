using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using ZoneHydrantEditor.Models;

namespace ZoneHydrantEditor.GraphicElements
{
    public static class BindingMarker
    {
        public static GMapMarker CreateMarker(BindingInfo binding, PointLatLng hydrantPosition, int currentZoom)
        {
            double markerScale = MarkerScaling.GetMarkerScale(currentZoom);
            bool showText = MarkerScaling.ShouldShowText(currentZoom);

            var canvas = CreateBindingCanvas(markerScale, showText, binding, hydrantPosition);

            var marker = new GMapMarker(new PointLatLng(binding.Latitude, binding.Longitude))
            {
                Shape = canvas,
                Offset = new Point(-3 * markerScale, -3 * markerScale),
                Tag = $"binding_{binding.Id}"
            };

            return marker;
        }

        public static GMapMarker CreateSimpleBinding(BindingInfo binding)
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Width = 200,Height = 100,ClipToBounds = false
            };
            var rectangle = new Rectangle
            {
                Width = 6,Height = 6,
                Fill = Brushes.Black,Stroke = Brushes.White,StrokeThickness = 1,IsHitTestVisible = false
            };
            Canvas.SetLeft(rectangle, 0);
            Canvas.SetTop(rectangle, 0);
            canvas.Children.Add(rectangle);
            var marker = new GMapMarker(new PointLatLng(binding.Latitude, binding.Longitude))
            {
                Shape = canvas,
                Offset = new Point(-3, -3),
                Tag = $"binding_{binding.Id}"
            };

            return marker;
        }

        public static void UpdateMarkerSize(GMapMarker marker, int zoomLevel)
        {
            if (marker?.Shape is not Canvas canvas) return;
            double markerScale = MarkerScaling.GetMarkerScale(zoomLevel);
            bool showText = MarkerScaling.ShouldShowText(zoomLevel);
            var rectangle = canvas.Children.OfType<Rectangle>().FirstOrDefault();
            if (rectangle != null)
            {
                rectangle.Width = 6 * markerScale;
                rectangle.Height = 6 * markerScale;
                rectangle.StrokeThickness = 1 * markerScale;
            }
            var textBlock = canvas.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null)
            {
                if (showText)
                {
                    textBlock.FontSize = 10 * markerScale;
                    textBlock.Visibility = Visibility.Visible;
                    Canvas.SetLeft(textBlock, 8 * markerScale);
                    Canvas.SetTop(textBlock, -12 * markerScale);
                }
                else
                {
                    textBlock.Visibility = Visibility.Collapsed;
                }
            }

            marker.Offset = new Point(-3 * markerScale, -3 * markerScale);
        }

        private static Canvas CreateBindingCanvas(double markerScale, bool showText,BindingInfo binding, PointLatLng hydrantPosition)
        {
            double dx = (binding.Latitude - hydrantPosition.Lat) * 111320;
            double dy = (binding.Longitude - hydrantPosition.Lng) *(111320 * Math.Cos(hydrantPosition.Lat * Math.PI / 180));
            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Width = 200,
                Height = 100,
                ClipToBounds = false
            };
            var rectangle = new Rectangle
            {
                Width = 6 * markerScale,Height = 6 * markerScale,
                Fill = Brushes.Black,Stroke = Brushes.White,StrokeThickness = 1 * markerScale,IsHitTestVisible = false
            };
            Canvas.SetLeft(rectangle, 0);
            Canvas.SetTop(rectangle, 0);
            canvas.Children.Add(rectangle);
            string hydrantName = string.IsNullOrEmpty(binding.HydrantNumber) ? "Гидрант" : binding.HydrantNumber;
            if (!string.IsNullOrEmpty(binding.HydrantTruba))
            {
                hydrantName += $" {binding.HydrantTruba}";
            }    

            var textBlock = new TextBlock
            {
                Text = $"{hydrantName}\nX: {(dx >= 0 ? "+" : "")}{dx:F0} м\nY: {(dy >= 0 ? "+" : "")}{dy:F0} м",
                FontSize = 10 * markerScale,Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2 * markerScale),
                IsHitTestVisible = false,Visibility = showText ? Visibility.Visible : Visibility.Collapsed
            };
            Canvas.SetLeft(textBlock, 8 * markerScale);
            Canvas.SetTop(textBlock, -12 * markerScale);
            canvas.Children.Add(textBlock);
            return canvas;
        }
    }
}