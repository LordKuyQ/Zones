using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;

namespace ZoneHydrantEditor.GraphicElements
{
    public static class ZoneMarker
    {
        public static GMapMarker CreateVertexMarker(PointLatLng position, int vertexNumber,
            bool isSelected = false,
            MouseButtonEventHandler onLeftButtonDown = null,
            MouseButtonEventHandler onLeftButtonUp = null,
            MouseButtonEventHandler onRightButtonDown = null)
        {
            var canvas = new Canvas { Width = 24, Height = 24, IsHitTestVisible = true };
            var ellipse = new Ellipse
            {
                Width = isSelected ? 14 : 10,Height = isSelected ? 14 : 10,
                Fill = isSelected ? Brushes.Yellow : Brushes.Red,
                Stroke = Brushes.Black,StrokeThickness = 2,
                Cursor = Cursors.Hand
            };
            var numberText = new TextBlock
            {
                Text = vertexNumber.ToString(),
                FontSize = 8,FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,Background = Brushes.White,
                Padding = new Thickness(2, 1, 2, 1),Margin = new Thickness(8, -8, 0, 0)
            };
            canvas.Children.Add(ellipse);
            canvas.Children.Add(numberText);
            if (onLeftButtonDown != null) ellipse.MouseLeftButtonDown += onLeftButtonDown;
            if (onLeftButtonUp != null) ellipse.MouseLeftButtonUp += onLeftButtonUp;
            if (onRightButtonDown != null) ellipse.MouseRightButtonDown += onRightButtonDown;
            return new GMapMarker(position)
            {
                Shape = canvas,
                Offset = new Point(-6, -6)
            };
        }

        public static void UpdateVertexNumber(GMapMarker marker, int newNumber)
        {
            if (marker?.Shape is Canvas canvas)
            {
                var textBlock = canvas.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Background == Brushes.White);
                if (textBlock != null)
                {
                    textBlock.Text = newNumber.ToString();
                }
            }
        }
    }
}