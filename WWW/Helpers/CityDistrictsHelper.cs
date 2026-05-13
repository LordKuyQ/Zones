using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Newtonsoft.Json.Linq;

namespace ZoneHydrantEditor
{
    public class CityDistrictsHelper
    {
        private readonly GMapControl _map;
        private readonly Dictionary<GMapPolygon, string> _districtPolygons = new();
        private GMapMarker _tooltipMarker;
        private System.Windows.Controls.TextBlock _tooltipText;
        private bool _tooltipVisible = false;

        public CityDistrictsHelper(GMapControl map)
        {
            _map = map;
            InitializeTooltip();
        }

        private void InitializeTooltip()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _tooltipText = new System.Windows.Controls.TextBlock
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    Foreground = Brushes.Black,
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Text = "",
                    Visibility = Visibility.Collapsed
                };

                _tooltipMarker = new GMapMarker(new PointLatLng(0, 0))
                {
                    Shape = _tooltipText,
                    Offset = new Point(10, -30),
                    ZIndex = 10000
                };

                _map.Markers.Add(_tooltipMarker);
                _map.MouseMove += Map_MouseMove;
            });
        }

        private void Map_MouseMove(object sender, MouseEventArgs e)
        {
            if (_districtPolygons.Count == 0) return;

            var point = e.GetPosition(_map);
            var latLng = _map.FromLocalToLatLng((int)point.X, (int)point.Y);

            string foundDistrict = null;
            foreach (var kvp in _districtPolygons)
            {
                if (IsPointInPolygon(latLng, kvp.Key.Points))
                {
                    foundDistrict = kvp.Value;
                    break;
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(foundDistrict))
                {
                    _tooltipMarker.Position = latLng;
                    _tooltipText.Text = foundDistrict;
                    _tooltipText.Visibility = Visibility.Visible;
                    _tooltipVisible = true;
                }
                else if (_tooltipVisible)
                {
                    _tooltipText.Visibility = Visibility.Collapsed;
                    _tooltipVisible = false;
                }
            });
        }

        private bool IsPointInPolygon(PointLatLng point, List<PointLatLng> polygon)
        {
            if (polygon.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Lat > point.Lat) != (polygon[j].Lat > point.Lat)) &&
                    (point.Lng < (polygon[j].Lng - polygon[i].Lng) * (point.Lat - polygon[i].Lat) /
                    (polygon[j].Lat - polygon[i].Lat) + polygon[i].Lng))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private string GetShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "Неизвестно";

            string[] parts = fullName.Split(',');
            if (parts.Length > 0)
            {
                string name = parts[0].Trim();
                name = name.Replace(" район", " р-н")
                           .Replace("Район", "р-н")
                           .Replace(" сельсовет", "")
                           .Replace("Сельсовет", "")
                           .Replace(" город", " г.")
                           .Replace("Город", "г.");
                return name;
            }
            return fullName;
        }

        public async Task LoadDistrictsAsync()
        {
            try
            {
                // Городские районы Новосибирска
                string[] cityDistricts = new[]
                {
                    "Ленинский район, Новосибирск",
                    "Кировский район, Новосибирск",
                    "Октябрьский район, Новосибирск",
                    "Центральный район, Новосибирск",
                    "Дзержинский район, Новосибирск",
                    "Калининский район, Новосибирск",
                    "Заельцовский район, Новосибирск",
                    "Железнодорожный район, Новосибирск",
                    "Первомайский район, Новосибирск",
                    "Советский район, Новосибирск"
                };

                // Города-спутники и крупные посёлки
                string[] satelliteCities = new[]
                {
                    "Бердск, Новосибирская область",
                    "Обь, Новосибирская область",
                    "Искитим, Новосибирская область",
                    "Кольцово, Новосибирская область",
                    "Краснообск, Новосибирская область",
                    "Барабинск, Новосибирская область",
                    "Болотное, Новосибирская область",
                    "Карасук, Новосибирская область",
                    "Каргат, Новосибирская область",
                    "Куйбышев, Новосибирская область",
                    "Купино, Новосибирская область",
                    "Татарск, Новосибирская область",
                    "Тогучин, Новосибирская область",
                    "Черепаново, Новосибирская область",
                    "Чулым, Новосибирская область"
                };

                // Сельсоветы и посёлки Новосибирского района
                string[] suburbanAreas = new[]
                {
                    "Мичуринский, Новосибирский район, Новосибирская область",
                    "Верх-Тула, Новосибирский район, Новосибирская область",
                    "Криводановка, Новосибирский район, Новосибирская область",
                    "Кудряшовский, Новосибирский район, Новосибирская область",
                    "Мочище, Новосибирский район, Новосибирская область",
                    "Барышево, Новосибирский район, Новосибирская область",
                    "Боровое, Новосибирский район, Новосибирская область",
                    "Раздольное, Новосибирский район, Новосибирская область",
                    "Толмачёво, Новосибирский район, Новосибирская область",
                    "Ярково, Новосибирский район, Новосибирская область",
                    "Каменка, Новосибирский район, Новосибирская область",
                    "Кубовая, Новосибирский район, Новосибирская область",
                    "Новолуговое, Новосибирский район, Новосибирская область",
                    "Плотниково, Новосибирский район, Новосибирская область",
                    "Станционный, Новосибирский район, Новосибирская область",
                    "Берёзовка, Новосибирский район, Новосибирская область",
                    "Морской, Новосибирский район, Новосибирская область",
                    "Элитный, Новосибирский район, Новосибирская область",
                    "Садовый, Новосибирский район, Новосибирская область",
                    "Приобский, Новосибирский район, Новосибирская область",
                    "Красный Восток, Новосибирский район, Новосибирская область",
                    "Озёрный, Новосибирский район, Новосибирская область",
                    "Восход, Новосибирский район, Новосибирская область",
                    "Железнодорожный, Новосибирский район, Новосибирская область",
                    "Ленинский, Новосибирский район, Новосибирская область",
                    "Степной, Новосибирский район, Новосибирская область",
                    "Тулинский, Новосибирский район, Новосибирская область",
                    "Юный Ленинец, Новосибирский район, Новосибирская область",
                    "Пашино, Новосибирский район, Новосибирская область",
                    "Снегири, Новосибирский район, Новосибирская область",
                    "Ложок, Новосибирский район, Новосибирская область",
                    "Издревая, Новосибирский район, Новосибирская область",
                    "Гусиный Брод, Новосибирский район, Новосибирская область",
                    "Каинская Заимка, Новосибирский район, Новосибирская область",
                    "Матвеевка, Новосибирский район, Новосибирская область",
                    "Колывань, Новосибирская область",
                    "Коченёво, Новосибирская область",
                    "Маслянино, Новосибирская область",
                    "Мошково, Новосибирская область",
                    "Ордынское, Новосибирская область",
                    "Сузун, Новосибирская область",
                    "Чаны, Новосибирская область",
                    "Чистоозёрное, Новосибирская область",
                    "Баган, Новосибирская область",
                    "Венгерово, Новосибирская область",
                    "Довольное, Новосибирская область",
                    "Здвинск, Новосибирская область",
                    "Кочки, Новосибирская область",
                    "Краснозёрское, Новосибирская область",
                    "Кыштовка, Новосибирская область",
                    "Северное, Новосибирская область",
                    "Убинское, Новосибирская область",
                    "Усть-Тарка, Новосибирская область"
                };

                // Микрорайоны
                string[] microDistricts = new[]
                {
                    "Академгородок, Новосибирск",
                    "ОбьГЭС, Новосибирск",
                    "Нижняя Ельцовка, Новосибирск",
                    "Пашино, Новосибирск"
                };

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "HydrantEditor/1.0");
                client.Timeout = TimeSpan.FromSeconds(15);

                await LoadDistrictList(client, cityDistricts, "городской район");
                await LoadDistrictList(client, satelliteCities, "город");
                await LoadDistrictList(client, suburbanAreas, "посёлок");
                await LoadDistrictList(client, microDistricts, "микрорайон");

                Console.WriteLine($"Всего загружено районов/поселений: {_districtPolygons.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private async Task LoadDistrictList(HttpClient client, string[] districts, string type)
        {
            foreach (var district in districts)
            {
                try
                {
                    string url = $"https://nominatim.openstreetmap.org/search" +
                                 $"?q={Uri.EscapeDataString(district)}" +
                                 $"&format=json&polygon_geojson=1&limit=1";

                    var response = await client.GetStringAsync(url);
                    var results = JArray.Parse(response);

                    if (results.Count > 0)
                    {
                        var geometry = results[0]["geojson"];
                        var displayName = results[0]["display_name"]?.ToString() ?? district;

                        if (geometry["type"]?.ToString() == "Polygon" &&
                            geometry["coordinates"]?[0] is JArray outerRing)
                        {
                            var points = new List<PointLatLng>();
                            foreach (var coord in outerRing)
                            {
                                if (coord is JArray arr && arr.Count >= 2)
                                {
                                    points.Add(new PointLatLng(
                                        arr[1].Value<double>(),
                                        arr[0].Value<double>()));
                                }
                            }

                            if (points.Count > 3)
                            {
                                string shortName = GetShortName(displayName);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Brush strokeColor = type switch
                                    {
                                        "городской район" => Brushes.Black,
                                        "город" => Brushes.DarkBlue,
                                        "посёлок" => Brushes.DarkGreen,
                                        "микрорайон" => Brushes.DarkRed,
                                        _ => Brushes.Gray
                                    };

                                    var path = new System.Windows.Shapes.Path
                                    {
                                        Stroke = strokeColor,
                                        StrokeThickness = 1,
                                        StrokeDashArray = new DoubleCollection(new double[] { 6, 4 }),
                                        Fill = Brushes.Transparent,
                                        IsHitTestVisible = false
                                    };

                                    var polygon = new GMapPolygon(points)
                                    {
                                        Shape = path,
                                        ZIndex = 50
                                    };

                                    _map.Markers.Add(polygon);
                                    _districtPolygons[polygon] = shortName;
                                });

                                Console.WriteLine($"Загружен: {shortName} ({type})");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Не найден: {district}");
                    }

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки {district}: {ex.Message}");
                }
            }
        }
    }
}