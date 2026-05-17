using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using GMap.NET.WindowsPresentation;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media.Imaging;

namespace ZoneHydrantEditor.Helpers
{
    public class MBTilesProvider : GMapProvider
    {
        public static readonly MBTilesProvider Instance;
        private SQLiteConnection _connection;
        private readonly object _lockObject = new();
        private readonly object _loadLock = new();
        private string _currentFilePath;
        private bool _isLoading = false;

        // Многоуровневый кэш: zoom -> (key -> image)
        private readonly Dictionary<int, ConcurrentDictionary<string, BitmapImage>> _multiZoomCache = [];
        private readonly object _multiCacheLock = new();

        // Кэш сырых байт (храним byte[] вместо BitmapImage — быстрее и меньше памяти)
        private readonly ConcurrentDictionary<string, byte[]> _rawTileCache = new();
        private readonly int _maxRawCacheSize = 3000;

        // Кэш последних использованных тайлов
        private readonly ConcurrentDictionary<string, BitmapImage> _recentCache = new();
        private readonly List<string> _recentCacheOrder = [];
        private readonly int _maxRecentCacheSize = 700;

        // Статистика
        private int _totalCacheHits = 0;
        private int _totalCacheMisses = 0;

        // Свойства для метаданных
        public string MapName { get; private set; } = "MBTiles Map";
        public string TileFormat { get; private set; } = "png";
        public string MapVersion { get; private set; } = "1.0";
        public string MapDescription { get; private set; } = "";
        public string MapAttribution { get; private set; } = "";
        public string MapType { get; private set; } = "baselayer";

        // Границы
        public double MinLat { get; private set; } = -90;
        public double MaxLat { get; private set; } = 90;
        public double MinLng { get; private set; } = -180;
        public double MaxLng { get; private set; } = 180;

        // Флаг загружен ли файл
        public bool IsLoaded { get; private set; } = false;
        public string CurrentFilePath => _currentFilePath;

        static MBTilesProvider()
        {
            Instance = new MBTilesProvider();
        }

        public override Guid Id
        {
            get { return Guid.Parse("A1B2C3D4-E5F6-7890-AB12-CD34EF567890"); }
        }

        public override string Name
        {
            get { return "MBTiles Provider"; }
        }

        public override PureProjection Projection
        {
            get { return MercatorProjection.Instance; }
        }

        public override GMapProvider[] Overlays
        {
            get { return [this]; }
        }

        public void ClearCache()
        {
            ClearAllCaches();
        }

        public void LoadMBTilesFile(string filePath)
        {
            lock (_loadLock)
            {
                if (_isLoading)
                {
                    Console.WriteLine("Загрузка MBTiles уже выполняется...");
                    return;
                }

                if (IsLoaded && _currentFilePath == filePath)
                {
                    Console.WriteLine($"MBTiles файл уже загружен: {Path.GetFileName(filePath)}");
                    return;
                }

                _isLoading = true;

                try
                {
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"MBTiles файл не найден: {filePath}");
                        IsLoaded = false;
                        return;
                    }

                    // Закрываем предыдущее соединение
                    Close();

                    // Создаем новое соединение
                    _connection = new SQLiteConnection($"Data Source={filePath};Read Only=True;");
                    _connection.Open();

                    // PRAGMA-оптимизации для скорости чтения
                    using (var pragma = _connection.CreateCommand())
                    {
                        pragma.CommandText = @"
                            PRAGMA synchronous=OFF;
                            PRAGMA cache_size=-65536;
                            PRAGMA mmap_size=268435456;
                            PRAGMA journal_mode=OFF;
                            PRAGMA query_only=ON;
                            PRAGMA temp_store=MEMORY;
                            PRAGMA page_size=4096;";
                        pragma.ExecuteNonQuery();
                    }

                    _currentFilePath = filePath;

                    // Загружаем метаданные
                    LoadMetadata();

                    // Проверяем структуру
                    CheckStructure();

                    // Очищаем кэши
                    ClearAllCaches();

                    IsLoaded = true;

                    Console.WriteLine($"✅ MBTiles файл загружен: {Path.GetFileName(filePath)}");
                    Console.WriteLine($"   Название карты: {MapName}, формат: {TileFormat}, тайлов: {GetTileCount()}");
                }
                catch (Exception ex)
                {
                    IsLoaded = false;
                    Console.WriteLine($"❌ Ошибка загрузки MBTiles: {ex.Message}");
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        private int GetTileCount()
        {
            try
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                    return 0;

                var cmd = new SQLiteCommand("SELECT COUNT(*) FROM tiles", _connection);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch
            {
                return 0;
            }
        }

        public void ClearAllCaches()
        {
            lock (_multiCacheLock)
            {
                _multiZoomCache.Clear();
            }

            _rawTileCache.Clear();
            _recentCache.Clear();
            _recentCacheOrder.Clear();
            _totalCacheHits = 0;
            _totalCacheMisses = 0;

            Console.WriteLine("Все кэши MBTiles очищены");
        }

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            if (!IsLoaded || _connection == null)
            {
                string defaultPath = "NewLoadMap.mbtiles";

                if (File.Exists(defaultPath))
                {
                    try
                    {
                        Console.WriteLine($"Автоматическая загрузка MBTiles: {defaultPath}");
                        LoadMBTilesFile(defaultPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка автозагрузки MBTiles: {ex.Message}");
                        return null;
                    }
                }

                if (!IsLoaded || _connection == null)
                {
                    return null;
                }
            }

            try
            {
                string cacheKey = $"{zoom}_{pos.X}_{pos.Y}";

                // 1. Проверяем кэш сырых байт (самый быстрый)
                if (_rawTileCache.TryGetValue(cacheKey, out byte[] rawData))
                {
                    System.Threading.Interlocked.Increment(ref _totalCacheHits);
                    return CreatePureImageFromBytes(rawData);
                }

                // 2. Проверяем кэш последних использованных
                if (_recentCache.TryGetValue(cacheKey, out BitmapImage recentImage))
                {
                    System.Threading.Interlocked.Increment(ref _totalCacheHits);
                    return CreatePureImageFromBitmap(recentImage);
                }

                // 3. Проверяем многоуровневый кэш
                lock (_multiCacheLock)
                {
                    if (_multiZoomCache.TryGetValue(zoom, out var zoomCache) &&
                        zoomCache.TryGetValue(cacheKey, out BitmapImage cachedImage))
                    {
                        System.Threading.Interlocked.Increment(ref _totalCacheHits);
                        AddToRecentCache(cacheKey, cachedImage);
                        return CreatePureImageFromBitmap(cachedImage);
                    }
                }

                System.Threading.Interlocked.Increment(ref _totalCacheMisses);

                // 4. Загружаем из БД
                byte[] tileData = LoadTileBytesFromDatabase(pos, zoom);

                if (tileData != null)
                {
                    // Сохраняем raw-байты в кэш
                    if (_rawTileCache.Count < _maxRawCacheSize)
                        _rawTileCache[cacheKey] = tileData;

                    BitmapImage image = LoadBitmapImage(tileData);

                    lock (_multiCacheLock)
                    {
                        if (!_multiZoomCache.ContainsKey(zoom))
                            _multiZoomCache[zoom] = new ConcurrentDictionary<string, BitmapImage>();
                        if (_multiZoomCache[zoom].Count < 1000)
                            _multiZoomCache[zoom][cacheKey] = image;
                    }

                    AddToRecentCache(cacheKey, image);
                    return CreatePureImageFromBitmap(image);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки тайла {zoom}/{pos.X}/{pos.Y}: {ex.Message}");
                return null;
            }
        }

        private void AddToRecentCache(string key, BitmapImage image)
        {
            lock (_recentCacheOrder)
            {
                if (_recentCache.TryAdd(key, image))
                {
                    _recentCacheOrder.Add(key);

                    while (_recentCacheOrder.Count > _maxRecentCacheSize)
                    {
                        string oldestKey = _recentCacheOrder[0];
                        _recentCacheOrder.RemoveAt(0);
                        _recentCache.TryRemove(oldestKey, out _);
                    }
                }
            }
        }

        private byte[] LoadTileBytesFromDatabase(GPoint pos, int zoom)
        {
            long tmsY = (1L << zoom) - 1 - pos.Y;

            byte[] tileData = GetTileFromTilesTable(zoom, (int)pos.X, (int)tmsY);
            tileData ??= GetTileFromOldFormat(zoom, (int)pos.X, (int)tmsY);

            return tileData;
        }

        private byte[] GetTileFromTilesTable(int zoom, int x, int y)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                        return null;

                    var cmd = new SQLiteCommand(
                        "SELECT tile_data FROM tiles WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y",
                        _connection);
                    cmd.Parameters.AddWithValue("@zoom", zoom);
                    cmd.Parameters.AddWithValue("@x", x);
                    cmd.Parameters.AddWithValue("@y", y);

                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return (byte[])result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка GetTileFromTilesTable: {ex.Message}");
            }
            return null;
        }

        private byte[] GetTileFromOldFormat(int zoom, int x, int y)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                        return null;

                    var mapCmd = new SQLiteCommand(
                        "SELECT tile_id FROM map WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y",
                        _connection);
                    mapCmd.Parameters.AddWithValue("@zoom", zoom);
                    mapCmd.Parameters.AddWithValue("@x", x);
                    mapCmd.Parameters.AddWithValue("@y", y);

                    var tileId = mapCmd.ExecuteScalar();
                    if (tileId != null && tileId != DBNull.Value)
                    {
                        var imgCmd = new SQLiteCommand(
                            "SELECT tile_data FROM images WHERE tile_id = @tileId",
                            _connection);
                        imgCmd.Parameters.AddWithValue("@tileId", tileId.ToString());

                        var result = imgCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (byte[])result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка GetTileFromOldFormat: {ex.Message}");
            }
            return null;
        }

        private static BitmapImage LoadBitmapImage(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(imageData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            return bitmap;
        }

        private static PureImage CreatePureImageFromBitmap(BitmapImage bitmap)
        {
            var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            stream.Position = 0;

            var pureImage = new GMapImage
            {
                Data = stream
            };

            return pureImage;
        }

        private static PureImage CreatePureImageFromBytes(byte[] imageData)
        {
            var stream = new MemoryStream(imageData, writable: false);
            var pureImage = new GMapImage
            {
                Data = stream
            };
            return pureImage;
        }

        private void LoadMetadata()
        {
            try
            {
                var cmd = new SQLiteCommand("SELECT name, value FROM metadata", _connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string name = reader.GetString(0).ToLower();
                    string value = reader.GetString(1);

                    switch (name)
                    {
                        case "name":
                            MapName = value;
                            break;
                        case "format":
                            TileFormat = value.ToLower();
                            break;
                        case "version":
                            MapVersion = value;
                            break;
                        case "description":
                            MapDescription = value;
                            break;
                        case "attribution":
                            MapAttribution = value;
                            break;
                        case "type":
                            MapType = value;
                            break;
                        case "bounds":
                            var parts = value.Split(',');
                            if (parts.Length == 4)
                            {
                                MinLng = double.Parse(parts[0]);
                                MinLat = double.Parse(parts[1]);
                                MaxLng = double.Parse(parts[2]);
                                MaxLat = double.Parse(parts[3]);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки метаданных: {ex.Message}");
            }
        }

        private void CheckStructure()
        {
            try
            {
                var cmd = new SQLiteCommand(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name='tiles'",
                    _connection);
                var result = cmd.ExecuteScalar();

                if (result != null)
                {
                    Console.WriteLine("   Используется современный формат MBTiles (таблица tiles)");
                }
                else
                {
                    var cmdMap = new SQLiteCommand(
                        "SELECT name FROM sqlite_master WHERE type='table' AND name='map'",
                        _connection);
                    var cmdImages = new SQLiteCommand(
                        "SELECT name FROM sqlite_master WHERE type='table' AND name='images'",
                        _connection);

                    bool hasMap = cmdMap.ExecuteScalar() != null;
                    bool hasImages = cmdImages.ExecuteScalar() != null;

                    if (hasMap && hasImages)
                    {
                        Console.WriteLine("   Используется старый формат MBTiles (таблицы map и images)");
                    }
                    else
                    {
                        Console.WriteLine("   ⚠ Внимание: Структура MBTiles не распознана!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка проверки структуры: {ex.Message}");
            }
        }

        public void Close()
        {
            lock (_lockObject)
            {
                if (_connection != null)
                {
                    _connection.Close();
                    _connection.Dispose();
                    _connection = null;
                }
                ClearAllCaches();
                IsLoaded = false;
                _currentFilePath = null;
                Console.WriteLine("MBTiles соединение закрыто");
            }
        }
    }
}