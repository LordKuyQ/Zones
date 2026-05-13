using System.Windows;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace ZoneHydrantEditor.Helpers
{
    // Класс для построения маршрутов и определения расстояния
    public class RoutingService
    {
        private GMapRoute _currentRoute;
        private GMapMarker _startMarker;
        private GMapMarker _endMarker;
        private PointLatLng _startPoint;
        private PointLatLng _endPoint;
        private bool _isSelectingRouteEnd = false;

        public GMapRoute CurrentRoute => _currentRoute;
        public GMapMarker StartMarker => _startMarker;
        public GMapMarker EndMarker => _endMarker;
        public bool IsSelectingRouteEnd => _isSelectingRouteEnd;

        public event EventHandler<RouteBuiltEventArgs> RouteBuilt;
        public event EventHandler RouteRemoved;

        public class RouteBuiltEventArgs : EventArgs
        {
            public double DistanceKm { get; set; }
            public List<PointLatLng> Points { get; set; }
        }

        public void StartRouteFromPoint(GMapMarker marker, Action<GMapMarker> onStartMarkerHighlight = null)
        {
            RemoveRoute();

            _startMarker = marker;
            _startPoint = marker.Position;
            onStartMarkerHighlight?.Invoke(_startMarker);
            _isSelectingRouteEnd = true;
        }

        public void FinishRouteAtPoint(PointLatLng point)
        {
            if (_startMarker == null) return;
            _endPoint = point;
            BuildRoute(_startPoint, _endPoint);
            _isSelectingRouteEnd = false;
        }

        private void BuildRoute(PointLatLng start, PointLatLng end)
        {
            RemoveRoute();

            var route = OpenStreetMapProvider.Instance.GetRoute(start, end, false, false, 15);
            if (route == null)
            {
                MessageBox.Show("Не удалось построить маршрут.", "Ошибка");
                return;
            }
            _currentRoute = new GMapRoute(route.Points)
            {
                Shape = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.DarkBlue,StrokeThickness = 3,Opacity = 0.8
                }
            };
            double totalDistance = CalculateRouteDistance(route.Points);
            RouteBuilt?.Invoke(this, new RouteBuiltEventArgs
            {
                DistanceKm = totalDistance,
                Points = route.Points
            });
        }
        public void RemoveRoute(Action<GMapMarker> onStartMarkerRestore = null, Action<GMapMarker> onEndMarkerRestore = null)
        {
            if (_startMarker != null)
                onStartMarkerRestore?.Invoke(_startMarker);
            if (_endMarker != null)
                onEndMarkerRestore?.Invoke(_endMarker);
            _currentRoute = null;
            _startMarker = null;
            _endMarker = null;
            _startPoint = PointLatLng.Empty;
            _endPoint = PointLatLng.Empty;
            _isSelectingRouteEnd = false;
            RouteRemoved?.Invoke(this, EventArgs.Empty);
        }

        public static double GetDistanceKm(PointLatLng p1, PointLatLng p2)
        {
            const double R = 6371;
            double lat1 = p1.Lat * Math.PI / 180.0;
            double lon1 = p1.Lng * Math.PI / 180.0;
            double lat2 = p2.Lat * Math.PI / 180.0;
            double lon2 = p2.Lng * Math.PI / 180.0;
            double dlat = lat2 - lat1;
            double dlon = lon2 - lon1;
            double a = Math.Pow(Math.Sin(dlat / 2), 2) +Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double CalculateRouteDistance(List<PointLatLng> points)
        {
            double total = 0.0;
            for (int i = 1; i < points.Count; i++)
            {
                total += GetDistanceKm(points[i - 1], points[i]);
            }
            return total;
        }
        public static double GetEuclideanDistance(PointLatLng p1, PointLatLng p2) => Math.Sqrt(Math.Pow(p1.Lat - p2.Lat, 2) + Math.Pow(p1.Lng - p2.Lng, 2));
    }
}