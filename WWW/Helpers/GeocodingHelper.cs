using System.Net.Http;
using GMap.NET;
using Newtonsoft.Json;

namespace ZoneHydrantEditor.Helpers
{
    // Сервис для геокодирования 
    public class GeocodingHelper
    {
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/";
        private const string PhotonUrl = "https://photon.komoot.io/api/";
        private const string BigDataCloudUrl = "https://api.bigdatacloud.net/data/reverse-geocode-client";

        private readonly HttpClient _httpClient;
        public GeocodingHelper()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HydrantEditor/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }
        public async Task<PointLatLng?> SearchAddressAsync(string address, string region = "Новосибирск", string viewbox = "82.7,55.1,83.5,54.7")
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var result = await SearchUsingNominatimAsync(address, region, viewbox);
            if (result.HasValue)
                return result;

            result = await SearchUsingPhotonAsync(address, region);
            if (result.HasValue)
                return result;

            return null;
        }
        public async Task<PointLatLng?> SearchUsingNominatimAsync(string address, string region = "Новосибирск", string viewbox = null)
        {
            try
            {
                string searchQuery = string.IsNullOrEmpty(region) ? address : $"{address}, {region}";
                string url = $"{NominatimUrl}search?" +$"q={Uri.EscapeDataString(searchQuery)}" +$"&format=json&addressdetails=1&limit=1";
                if (!string.IsNullOrEmpty(viewbox))
                {
                    url += $"&bounded=1&viewbox={viewbox}";
                }
                var response = await _httpClient.GetStringAsync(url);
                var results = JsonConvert.DeserializeObject<List<dynamic>>(response);

                if (results?.Count > 0)
                {
                    double lat = Convert.ToDouble(results[0].lat);
                    double lon = Convert.ToDouble(results[0].lon);
                    return new PointLatLng(lat, lon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nominatim ошибка: {ex.Message}");
            }
            return null;
        }
        public async Task<PointLatLng?> SearchUsingPhotonAsync(string address, string region = "Новосибирск")
        {
            try
            {
                string searchQuery = string.IsNullOrEmpty(region) ? address : $"{address}, {region}";
                string url = $"{PhotonUrl}?q={Uri.EscapeDataString(searchQuery)}&limit=1";

                var response = await _httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data?.features.Count > 0)
                {
                    double lat = data.features[0].geometry.coordinates[1];
                    double lon = data.features[0].geometry.coordinates[0];
                    return new PointLatLng(lat, lon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Photon ошибка: {ex.Message}");
            }
            return null;
        }
        public async Task<string> GetAddressFromCoordinatesAsync(double lat, double lng)
        {
            string address = await ReverseGeocodeNominatimAsync(lat, lng);
            if (!string.IsNullOrEmpty(address))
                return address;

            address = await ReverseGeocodeBigDataCloudAsync(lat, lng);
            if (!string.IsNullOrEmpty(address))
                return address;

            return "Адрес не найден";
        }
        public async Task<string> ReverseGeocodeNominatimAsync(double lat, double lng)
        {
            try
            {
                string url = $"{NominatimUrl}reverse?" +$"lat={lat}&lon={lng}" +$"&format=json&addressdetails=1&accept-language=ru";

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<dynamic>(response);

                var displayName = json["display_name"]?.ToString();
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nominatim reverse ошибка: {ex.Message}");
            }
            return null;
        }
        public async Task<string> ReverseGeocodeBigDataCloudAsync(double lat, double lng)
        {
            try
            {
                string url = $"{BigDataCloudUrl}?" +$"latitude={lat}&longitude={lng}" +$"&localityLanguage=ru";

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<dynamic>(response);

                var locality = json["locality"]?.ToString();
                var city = json["city"]?.ToString();
                var countryName = json["countryName"]?.ToString();

                if (!string.IsNullOrEmpty(locality) || !string.IsNullOrEmpty(city))
                {
                    return $"{locality}, {city}, {countryName}".TrimStart(',', ' ').TrimEnd(',', ' ');
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BigDataCloud ошибка: {ex.Message}");
            }
            return null;
        }
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}