using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using TestDbApp.Models;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor.GraphicElements
{
    public static class BindingMarker
    {
        public static GMapMarker CreateMarkerFromEwss(Ewss ewss, PointLatLng hydrantPosition, int currentZoom)
        {
            double markerScale = MarkerScaling.GetMarkerScale(currentZoom);
            bool showText = MarkerScaling.ShouldShowText(currentZoom);

            double bindingLat = Utility.ParseBindingDistance(ewss.EwsPriviazkaGeoX);
            double bindingLng = Utility.ParseBindingDistance(ewss.EwsPriviazkaGeoY);

            if (bindingLat == 0 && bindingLng == 0)
            {
                var fallback = new GMapMarker(hydrantPosition)
                {
                    Shape = new Canvas { Width = 1, Height = 1 },
                    Tag = $"binding_{ewss.EwsId}"
                };
                return fallback;
            }

            double dx = bindingLat - hydrantPosition.Lat;
            double dy = bindingLng - hydrantPosition.Lng;

            string hydrantNumber = ewss.DisplayNumber ?? ewss.EwsNumber ?? "Без номера";
            string pipeInfo = ewss.PipeInfo ?? "";
            string displayText = $"{hydrantNumber}";
            if (!string.IsNullOrEmpty(pipeInfo))
            {
                displayText += $" ({pipeInfo})";
            }

            var canvas = CreateBindingCanvas(markerScale, showText, displayText, ewss);

            var marker = new GMapMarker(new PointLatLng(bindingLat, bindingLng))
            {
                Shape = canvas,
                Offset = new Point(-3 * markerScale, -3 * markerScale),
                Tag = $"binding_{ewss.EwsId}"
            };

            return marker;
        }

        public static GMapMarker CreateSimpleBinding(double lat, double lng, string ewsId, string hydrantNumber = "")
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Width = 200,
                Height = 100,
                ClipToBounds = false
            };
            var rectangle = new Rectangle
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.Black,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rectangle, 0);
            Canvas.SetTop(rectangle, 0);
            canvas.Children.Add(rectangle);

            if (!string.IsNullOrEmpty(hydrantNumber))
            {
                var textBlock = new TextBlock
                {
                    Text = hydrantNumber,
                    FontSize = 8,
                    Foreground = Brushes.Black,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    Padding = new Thickness(2),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(textBlock, 8);
                Canvas.SetTop(textBlock, -10);
                canvas.Children.Add(textBlock);
            }

            var marker = new GMapMarker(new PointLatLng(lat, lng))
            {
                Shape = canvas,
                Offset = new Point(-3, -3),
                Tag = $"binding_{ewsId}"
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

        private static Canvas CreateBindingCanvas(double markerScale, bool showText,
            string displayText, Ewss ewss)
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Width = 200,
                Height = 100,
                ClipToBounds = false
            };
            var rectangle = new Rectangle
            {
                Width = 6 * markerScale,
                Height = 6 * markerScale,
                Fill = Brushes.Black,
                Stroke = Brushes.White,
                StrokeThickness = 1 * markerScale,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rectangle, 0);
            Canvas.SetTop(rectangle, 0);
            canvas.Children.Add(rectangle);

            string left = !string.IsNullOrEmpty(ewss.EwsPrLeft) ? ewss.EwsPrLeft : "?";
            string right = !string.IsNullOrEmpty(ewss.EwsPrRight) ? ewss.EwsPrRight : "?";
            string straight = !string.IsNullOrEmpty(ewss.EwsPrStright) ? ewss.EwsPrStright : "?";

            var textBlock = new TextBlock
            {
                Text = $"{displayText}\n← {left} м | {straight} м | → {right} м",
                FontSize = 10 * markerScale,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2 * markerScale),
                IsHitTestVisible = false,
                Visibility = showText ? Visibility.Visible : Visibility.Collapsed
            };
            Canvas.SetLeft(textBlock, 8 * markerScale);
            Canvas.SetTop(textBlock, -12 * markerScale);
            canvas.Children.Add(textBlock);
            return canvas;
        }
    }
}
