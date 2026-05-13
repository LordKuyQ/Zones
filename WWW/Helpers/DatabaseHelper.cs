using GMap.NET;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp5.Models;
using ZoneHydrantEditor.Models;

namespace ZoneHydrantEditor.Helpers
{
    public class DatabaseService
    {
        private const string ZonesDbFile = "zones0815.db";
        private const string HydrantsDbFile = "hydrants0815.db";

        private SQLiteConnection _zonesConnection;
        private SQLiteConnection _hydrantsConnection;

        // Менеджер кэширования
        private readonly CacheManager _cacheManager;
        public DatabaseService()
        {
            _cacheManager = new CacheManager();
            InitializeDatabases();
        }
        // Доступ к кэш-менеджеру для внешнего использования
        internal CacheManager Cache => _cacheManager;

        #region Инициализация баз данных

        private void InitializeDatabases()
        {
            InitializeZonesDatabase();
            InitializeHydrantsDatabase();
        }

        private void InitializeZonesDatabase()
        {
            bool newDb = !File.Exists(ZonesDbFile);
            if (newDb)
                SQLiteConnection.CreateFile(ZonesDbFile);

            _zonesConnection = new SQLiteConnection($"Data Source={ZonesDbFile}");
            _zonesConnection.Open();

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS Zones (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );", _zonesConnection).ExecuteNonQuery();

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS ZonePoints (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ZoneId INTEGER,
                    OrderIndex INTEGER,
                    Latitude REAL,
                    Longitude REAL,
                    FOREIGN KEY (ZoneId) REFERENCES Zones(Id)
                );", _zonesConnection).ExecuteNonQuery();

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS ZoneBackups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ZoneId INTEGER NOT NULL,
                    BackupDate TEXT NOT NULL,
                    ZoneName TEXT,
                    BackupReason TEXT
                );", _zonesConnection).ExecuteNonQuery();

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS ZoneBackupPoints (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BackupId INTEGER,
                    OrderIndex INTEGER,
                    Latitude REAL,
                    Longitude REAL,
                    FOREIGN KEY (BackupId) REFERENCES ZoneBackups(Id)
                );", _zonesConnection).ExecuteNonQuery();

            _zonesConnection.Close();
        }

        private void InitializeHydrantsDatabase()
        {
            bool newDb = !File.Exists(HydrantsDbFile);
            if (newDb)
                SQLiteConnection.CreateFile(HydrantsDbFile);

            _hydrantsConnection = new SQLiteConnection($"Data Source={HydrantsDbFile};Version=3;");
            _hydrantsConnection.Open();

            string createMarkers = @"
                CREATE TABLE IF NOT EXISTS Markers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    GidrantNumber TEXT,
                    GidrantTruba TEXT,
                    GidrantAdres TEXT,
                    CompanyName TEXT,
                    ZoneId INTEGER,
                    Status TEXT DEFAULT 'Непроверенный',
                    BreakReason TEXT
                );";
            new SQLiteCommand(createMarkers, _hydrantsConnection).ExecuteNonQuery();

            string createBindings = @"
                CREATE TABLE IF NOT EXISTS HydrantBindings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    DistanceToHydrantX REAL NOT NULL,
                    DistanceToHydrantY REAL NOT NULL,
                    HydrantId INTEGER NOT NULL,
                    FOREIGN KEY (HydrantId) REFERENCES Markers(Id)
                );";
            new SQLiteCommand(createBindings, _hydrantsConnection).ExecuteNonQuery();

            string createCompanies = @"
                CREATE TABLE IF NOT EXISTS Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );";
            new SQLiteCommand(createCompanies, _hydrantsConnection).ExecuteNonQuery();

            // Добавляем значения по умолчанию, если таблица пустая
            var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM Companies", _hydrantsConnection);
            int count = Convert.ToInt32(checkCmd.ExecuteScalar());
            if (count == 0)
            {
                new SQLiteCommand("INSERT INTO Companies (Name) VALUES ('Бесхозный')", _hydrantsConnection).ExecuteNonQuery();
                new SQLiteCommand("INSERT INTO Companies (Name) VALUES ('Горводоканал')", _hydrantsConnection).ExecuteNonQuery();
            }

            _hydrantsConnection.Close();
        }

        #endregion

        #region Управление соединениями

        public void OpenZonesConnection()
        {
            if (_zonesConnection.State != ConnectionState.Open)
                _zonesConnection.Open();
        }

        public void CloseZonesConnection()
        {
            if (_zonesConnection.State != ConnectionState.Closed)
                _zonesConnection.Close();
        }

        public void OpenHydrantsConnection()
        {
            if (_hydrantsConnection.State != ConnectionState.Open)
                _hydrantsConnection.Open();
        }

        public void CloseHydrantsConnection()
        {
            if (_hydrantsConnection.State != ConnectionState.Closed)
                _hydrantsConnection.Close();
        }

        public SQLiteConnection GetZonesConnection() => _zonesConnection;
        public SQLiteConnection GetHydrantsConnection() => _hydrantsConnection;

        #endregion

        #region Работа с зонами (с кэшированием)

        public Dictionary<int, string> LoadZonesToDictionary()
        {
            return _cacheManager.GetZones(() =>
            {
                var zones = new Dictionary<int, string>();

                try
                {
                    OpenZonesConnection();

                    var cmd = new SQLiteCommand("SELECT Id, Name FROM Zones ORDER BY Id", _zonesConnection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string name = reader.GetString(1);
                        zones[id] = name;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка загрузки списка зон: {ex.Message}");
                }
                finally
                {
                    CloseZonesConnection();
                }

                return zones;
            });
        }

        public bool ZoneNameExists(string zoneName)
        {
            if (string.IsNullOrWhiteSpace(zoneName))
                return false;

            try
            {
                OpenZonesConnection();

                var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Zones WHERE Name = @name", _zonesConnection);
                cmd.Parameters.AddWithValue("@name", zoneName.Trim());

                int count = Convert.ToInt32(cmd.ExecuteScalar());

                return count > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public int CreateZone(string zoneName)
        {
            try
            {
                OpenZonesConnection();

                var cmd = new SQLiteCommand("INSERT INTO Zones (Name) VALUES (@name)", _zonesConnection);
                cmd.Parameters.AddWithValue("@name", zoneName);
                cmd.ExecuteNonQuery();

                int newId = (int)_zonesConnection.LastInsertRowId;

                // Обновляем кэш
                _cacheManager.AddZone(newId, zoneName);

                return newId;
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public void RenameZone(int zoneId, string newName)
        {
            try
            {
                OpenZonesConnection();

                var cmd = new SQLiteCommand("UPDATE Zones SET Name = @name WHERE Id = @id", _zonesConnection);
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@id", zoneId);
                cmd.ExecuteNonQuery();

                // Обновляем кэш
                _cacheManager.RenameZone(zoneId, newName);
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public List<PointLatLng> GetZonePoints(int zoneId)
        {
            return _cacheManager.GetZonePoints(zoneId, (id) =>
            {
                var points = new List<PointLatLng>();

                try
                {
                    OpenZonesConnection();

                    var cmd = new SQLiteCommand(
                        "SELECT Latitude, Longitude FROM ZonePoints WHERE ZoneId=@z ORDER BY OrderIndex",
                        _zonesConnection);
                    cmd.Parameters.AddWithValue("@z", id);

                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        points.Add(new PointLatLng(r.GetDouble(0), r.GetDouble(1)));
                    }
                }
                finally
                {
                    CloseZonesConnection();
                }

                return points;
            });
        }

        public int GetZonePointCount(int zoneId)
        {
            try
            {
                OpenZonesConnection();

                var cmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM ZonePoints WHERE ZoneId=@z",
                    _zonesConnection);
                cmd.Parameters.AddWithValue("@z", zoneId);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public void SaveZonePoints(int zoneId, List<PointLatLng> points)
        {
            try
            {
                OpenZonesConnection();

                using var tr = _zonesConnection.BeginTransaction();

                // Удаляем старые точки
                new SQLiteCommand(
                    "DELETE FROM ZonePoints WHERE ZoneId=@z",
                    _zonesConnection, tr)
                {
                    Parameters = { new SQLiteParameter("@z", zoneId) }
                }.ExecuteNonQuery();

                // Сохраняем новые точки
                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    var cmd = new SQLiteCommand(
                        "INSERT INTO ZonePoints (ZoneId,OrderIndex,Latitude,Longitude) VALUES (@z,@o,@lat,@lng)",
                        _zonesConnection, tr);

                    cmd.Parameters.AddWithValue("@z", zoneId);
                    cmd.Parameters.AddWithValue("@o", i);
                    cmd.Parameters.AddWithValue("@lat", p.Lat);
                    cmd.Parameters.AddWithValue("@lng", p.Lng);
                    cmd.ExecuteNonQuery();
                }

                tr.Commit();

                // Обновляем кэш
                _cacheManager.UpdateZonePoints(zoneId);
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public void DeleteZonePoints(int zoneId)
        {
            try
            {
                OpenZonesConnection();

                new SQLiteCommand(
                    "DELETE FROM ZonePoints WHERE ZoneId=@z",
                    _zonesConnection)
                {
                    Parameters = { new SQLiteParameter("@z", zoneId) }
                }.ExecuteNonQuery();
            }
            finally
            {
                CloseZonesConnection();
            }
        }

        public List<Zone> GetAllZones()
        {
            var zones = new List<Zone>();

            try
            {
                OpenZonesConnection();

                var zonesCmd = new SQLiteCommand("SELECT Id, Name FROM Zones ORDER BY Id", _zonesConnection);
                using var zonesReader = zonesCmd.ExecuteReader();

                while (zonesReader.Read())
                {
                    zones.Add(new Zone
                    {
                        Id = zonesReader.GetInt32(0),
                        Name = zonesReader.GetString(1)
                    });
                }
            }
            finally
            {
                CloseZonesConnection();
            }

            return zones;
        }

        public ZoneInfo GetZoneBounds(int zoneId)
        {
            return _cacheManager.GetZoneBounds(zoneId, (id) =>
            {
                try
                {
                    OpenZonesConnection();

                    var cmd = new SQLiteCommand(
                        @"SELECT MIN(Latitude) as MinLat, MAX(Latitude) as MaxLat,
                     MIN(Longitude) as MinLng, MAX(Longitude) as MaxLng
              FROM ZonePoints WHERE ZoneId=@z", _zonesConnection);
                    cmd.Parameters.AddWithValue("@z", id);

                    using var r = cmd.ExecuteReader();
                    if (r.Read() && !r.IsDBNull(0))
                    {
                        return new ZoneInfo
                        {
                            ZoneId = id,
                            MinLat = r.GetDouble(0),
                            MaxLat = r.GetDouble(1),
                            MinLng = r.GetDouble(2),
                            MaxLng = r.GetDouble(3),
                            HasValidBounds = true
                        };
                    }
                }
                finally
                {
                    CloseZonesConnection();
                }

                return new ZoneInfo { ZoneId = id, HasValidBounds = false };
            });
        }

        public List<ZoneInfo> GetAllZoneBounds()
        {
            var zones = new List<ZoneInfo>();

            try
            {
                OpenZonesConnection();

                var cmd = new SQLiteCommand(@"
                    SELECT z.Id, z.Name,
                           MIN(zp.Latitude) as MinLat,
                           MAX(zp.Latitude) as MaxLat,
                           MIN(zp.Longitude) as MinLng,
                           MAX(zp.Longitude) as MaxLng
                    FROM Zones z
                    LEFT JOIN ZonePoints zp ON z.Id = zp.ZoneId
                    GROUP BY z.Id, z.Name
                    ORDER BY z.Id", _zonesConnection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(2))
                    {
                        zones.Add(new ZoneInfo
                        {
                            ZoneId = reader.GetInt32(0),
                            ZoneName = reader.GetString(1),
                            MinLat = reader.GetDouble(2),
                            MaxLat = reader.GetDouble(3),
                            MinLng = reader.GetDouble(4),
                            MaxLng = reader.GetDouble(5),
                            HasValidBounds = true
                        });
                    }
                }
            }
            finally
            {
                CloseZonesConnection();
            }

            return zones;
        }

        #endregion

        #region Работа с гидрантами (с кэшированием)

        public int SaveMarker(double lat, double lng, string number, string truba, string adres,
            string company, string status, string breakReason, int? zoneId)
        {
            try
            {
                OpenHydrantsConnection();

                string query = @"
                    INSERT INTO Markers (Latitude, Longitude, GidrantNumber, GidrantTruba, 
                                        GidrantAdres, CompanyName, Status, BreakReason,
                                        ZoneId)
                    VALUES (@lat, @lng, @num, @truba, @adres, @comp, @status, @breakReason, @zoneId)";

                using var cmd = new SQLiteCommand(query, _hydrantsConnection);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lng", lng);
                cmd.Parameters.AddWithValue("@num", number ?? "");
                cmd.Parameters.AddWithValue("@truba", truba ?? "");
                cmd.Parameters.AddWithValue("@adres", adres ?? "");
                cmd.Parameters.AddWithValue("@comp", company ?? "");
                cmd.Parameters.AddWithValue("@status", status ?? "Непроверенный");
                cmd.Parameters.AddWithValue("@breakReason", breakReason ?? "");
                cmd.Parameters.AddWithValue("@zoneId", zoneId.HasValue ? zoneId.Value : DBNull.Value);
                cmd.ExecuteNonQuery();

                using var lastIdCmd = new SQLiteCommand("SELECT last_insert_rowid()", _hydrantsConnection);
                var last = lastIdCmd.ExecuteScalar();
                int newId = last != null ? Convert.ToInt32(last) : -1;

                // Обновляем кэш
                var newMarker = new MarkerInfo
                {
                    Id = newId,
                    Latitude = lat,
                    Longitude = lng,
                    GidrantNumber = number ?? "",
                    GidrantTruba = truba ?? "",
                    GidrantAdres = adres ?? "",
                    CompanyName = company ?? "",
                    Status = status ?? "Непроверенный",
                    BreakReason = breakReason ?? "",
                    ZoneId = zoneId
                };

                _cacheManager.AddMarker(newMarker);

                return newId;
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public List<MarkerInfo> GetAllMarkers()
        {
            return _cacheManager.GetAllMarkers(() =>
            {
                var markers = new List<MarkerInfo>();

                try
                {
                    OpenHydrantsConnection();

                    const string select = "SELECT Id, Latitude, Longitude, GidrantNumber, " +
                                         "GidrantTruba, GidrantAdres, CompanyName, Status, BreakReason, ZoneId FROM Markers";
                    using var cmd = new SQLiteCommand(select, _hydrantsConnection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        markers.Add(new MarkerInfo
                        {
                            Id = reader.GetInt32(0),
                            Latitude = reader.GetDouble(1),
                            Longitude = reader.GetDouble(2),
                            GidrantNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            GidrantTruba = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            GidrantAdres = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            CompanyName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Status = reader.IsDBNull(7) ? "Непроверенный" : reader.GetString(7),
                            BreakReason = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            ZoneId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
                        });
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return markers;
            });
        }

        public MarkerInfo GetMarkerById(int markerId)
        {
            return _cacheManager.GetMarkerById(markerId, (id) =>
            {
                try
                {
                    OpenHydrantsConnection();

                    var cmd = new SQLiteCommand(
                        @"SELECT Latitude, Longitude, GidrantNumber, GidrantTruba, 
                      Status, BreakReason, ZoneId, GidrantAdres, CompanyName
                      FROM Markers WHERE Id = @id",
                        _hydrantsConnection);
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new MarkerInfo
                        {
                            Id = id,
                            Latitude = reader.GetDouble(0),
                            Longitude = reader.GetDouble(1),
                            GidrantNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            GidrantTruba = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Status = reader.IsDBNull(4) ? "Непроверенный" : reader.GetString(4),
                            BreakReason = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            ZoneId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            GidrantAdres = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            CompanyName = reader.IsDBNull(8) ? "" : reader.GetString(8)
                        };
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return null;
            });
        }

        public int GetMarkerIdByCoordinates(double lat, double lng)
        {
            // Используем кэш для поиска
            return _cacheManager.GetMarkerIdByCoordinates(lat, lng);
        }

        public void UpdateMarker(int markerId, double lat, double lng, string number, string truba,
            string adres, string company, string status, string breakReason, int? zoneId)
        {
            try
            {
                OpenHydrantsConnection();

                string query = @"UPDATE Markers SET 
                    Latitude = @lat, Longitude = @lng,
                    GidrantNumber = @num, GidrantTruba = @truba,
                    GidrantAdres = @adres, CompanyName = @comp,
                    Status = @status, BreakReason = @breakReason,
                    ZoneId = @zoneId
                    WHERE Id = @id";

                using var cmd = new SQLiteCommand(query, _hydrantsConnection);
                cmd.Parameters.AddWithValue("@id", markerId);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lng", lng);
                cmd.Parameters.AddWithValue("@num", number ?? "");
                cmd.Parameters.AddWithValue("@truba", truba ?? "");
                cmd.Parameters.AddWithValue("@adres", adres ?? "");
                cmd.Parameters.AddWithValue("@comp", company ?? "");
                cmd.Parameters.AddWithValue("@status", status ?? "Непроверенный");
                cmd.Parameters.AddWithValue("@breakReason", breakReason ?? "");
                cmd.Parameters.AddWithValue("@zoneId", zoneId.HasValue ? zoneId.Value : DBNull.Value);
                cmd.ExecuteNonQuery();

                // Обновляем кэш
                var updatedMarker = new MarkerInfo
                {
                    Id = markerId,
                    Latitude = lat,
                    Longitude = lng,
                    GidrantNumber = number ?? "",
                    GidrantTruba = truba ?? "",
                    GidrantAdres = adres ?? "",
                    CompanyName = company ?? "",
                    Status = status ?? "Непроверенный",
                    BreakReason = breakReason ?? "",
                    ZoneId = zoneId
                };

                _cacheManager.UpdateMarker(updatedMarker);
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public void UpdateMarkerZoneInfo(int markerId, int? zoneId)
        {
            try
            {
                OpenHydrantsConnection();

                var updateCmd = new SQLiteCommand(
                    @"UPDATE Markers SET ZoneId = @zoneId WHERE Id = @id",
                    _hydrantsConnection);
                updateCmd.Parameters.AddWithValue("@zoneId", zoneId.HasValue ? zoneId.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@id", markerId);
                updateCmd.ExecuteNonQuery();

                // Обновляем кэш
                var marker = GetMarkerById(markerId);
                if (marker != null)
                {
                    marker.ZoneId = zoneId;
                    _cacheManager.UpdateMarker(marker);
                }
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public void DeleteMarker(int markerId)
        {
            try
            {
                OpenHydrantsConnection();

                using (var transaction = _hydrantsConnection.BeginTransaction())
                {
                    // Сначала удаляем привязки из-за внешнего ключа
                    var deleteBindingCmd = new SQLiteCommand(
                        "DELETE FROM HydrantBindings WHERE HydrantId = @hydrantId",
                        _hydrantsConnection, transaction);
                    deleteBindingCmd.Parameters.AddWithValue("@hydrantId", markerId);
                    deleteBindingCmd.ExecuteNonQuery();

                    // Затем удаляем сам гидрант
                    var deleteHydrantCmd = new SQLiteCommand(
                        "DELETE FROM Markers WHERE Id = @id",
                        _hydrantsConnection, transaction);
                    deleteHydrantCmd.Parameters.AddWithValue("@id", markerId);
                    deleteHydrantCmd.ExecuteNonQuery();

                    transaction.Commit();
                }

                // Обновляем кэш
                _cacheManager.RemoveMarker(markerId);
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public List<MarkerInfo> GetMarkersInZone(int zoneId)
        {
            return _cacheManager.GetMarkersInZone(zoneId, (id) =>
            {
                var markers = new List<MarkerInfo>();

                try
                {
                    OpenHydrantsConnection();

                    var cmd = new SQLiteCommand(
                        @"SELECT Id, Latitude, Longitude, GidrantNumber, GidrantTruba, Status, BreakReason, GidrantAdres, CompanyName
                  FROM Markers WHERE ZoneId = @zoneId ORDER BY GidrantNumber",
                        _hydrantsConnection);
                    cmd.Parameters.AddWithValue("@zoneId", id);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        markers.Add(new MarkerInfo
                        {
                            Id = reader.GetInt32(0),
                            Latitude = reader.GetDouble(1),
                            Longitude = reader.GetDouble(2),
                            GidrantNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            GidrantTruba = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Status = reader.IsDBNull(5) ? "Непроверенный" : reader.GetString(5),
                            BreakReason = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            GidrantAdres = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            CompanyName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            ZoneId = id
                        });
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return markers;
            });
        }
        #endregion

        #region Работа с привязками (с кэшированием)

        public bool HasExistingBinding(int hydrantId)
        {
            return _cacheManager.HasExistingBinding(hydrantId);
        }

        public int SaveBinding(double lat, double lng, double distanceX, double distanceY, int hydrantId)
        {
            try
            {
                OpenHydrantsConnection();

                string query = @"INSERT INTO HydrantBindings (Latitude, Longitude, 
                                DistanceToHydrantX, DistanceToHydrantY, HydrantId)
                                VALUES (@lat, @lng, @distX, @distY, @hydrantId)";

                using var cmd = new SQLiteCommand(query, _hydrantsConnection);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lng", lng);
                cmd.Parameters.AddWithValue("@distX", distanceX);
                cmd.Parameters.AddWithValue("@distY", distanceY);
                cmd.Parameters.AddWithValue("@hydrantId", hydrantId);
                cmd.ExecuteNonQuery();

                using var lastIdCmd = new SQLiteCommand("SELECT last_insert_rowid()", _hydrantsConnection);
                var last = lastIdCmd.ExecuteScalar();
                int newId = last != null ? Convert.ToInt32(last) : -1;

                // Получаем информацию о гидранте для кэша
                var hydrant = GetMarkerById(hydrantId);

                // Обновляем кэш
                var newBinding = new BindingInfo
                {
                    Id = newId,
                    Latitude = lat,
                    Longitude = lng,
                    DistanceX = distanceX,
                    DistanceY = distanceY,
                    HydrantId = hydrantId,
                    HydrantNumber = hydrant?.GidrantNumber ?? "",
                    HydrantTruba = hydrant?.GidrantTruba ?? ""
                };

                _cacheManager.AddBinding(newBinding);

                return newId;
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public List<BindingInfo> GetAllBindings()
        {
            return _cacheManager.GetAllBindings(() =>
            {
                var bindings = new List<BindingInfo>();

                try
                {
                    OpenHydrantsConnection();

                    const string select = @"SELECT b.Id, b.Latitude, b.Longitude, 
                                               b.DistanceToHydrantX, b.DistanceToHydrantY,
                                               m.GidrantNumber, m.GidrantTruba, m.Latitude as HydrantLat, m.Longitude as HydrantLng,
                                               m.Id as HydrantId
                                        FROM HydrantBindings b
                                        INNER JOIN Markers m ON b.HydrantId = m.Id";

                    using var cmd = new SQLiteCommand(select, _hydrantsConnection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        bindings.Add(new BindingInfo
                        {
                            Id = reader.GetInt32(0),
                            Latitude = reader.GetDouble(1),
                            Longitude = reader.GetDouble(2),
                            DistanceX = reader.GetDouble(3),
                            DistanceY = reader.GetDouble(4),
                            HydrantNumber = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            HydrantTruba = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            HydrantId = reader.GetInt32(9)
                        });
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return bindings;
            });
        }

        public List<BindingInfo> GetBindingsForHydrant(int hydrantId)
        {
            return _cacheManager.GetBindingsForHydrant(hydrantId, (id) =>
            {
                var bindings = new List<BindingInfo>();

                try
                {
                    OpenHydrantsConnection();

                    string query = @"SELECT b.Latitude, b.Longitude, b.DistanceToHydrantX, b.DistanceToHydrantY,
                             m.GidrantNumber, m.GidrantTruba, m.Id as HydrantId, b.Id
                      FROM HydrantBindings b
                      INNER JOIN Markers m ON b.HydrantId = m.Id
                      WHERE m.Id = @hydrantId";

                    using var cmd = new SQLiteCommand(query, _hydrantsConnection);
                    cmd.Parameters.AddWithValue("@hydrantId", id);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        bindings.Add(new BindingInfo
                        {
                            Id = reader.GetInt32(7),
                            Latitude = reader.GetDouble(0),
                            Longitude = reader.GetDouble(1),
                            DistanceX = reader.GetDouble(2),
                            DistanceY = reader.GetDouble(3),
                            HydrantNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            HydrantTruba = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            HydrantId = reader.GetInt32(6)
                        });
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return bindings;
            });
        }


        public BindingInfo GetBindingInfo(int bindingId)
        {
            return _cacheManager.GetBindingInfo(bindingId, (id) =>
            {
                try
                {
                    OpenHydrantsConnection();

                    var infoCmd = new SQLiteCommand(@"
                    SELECT b.HydrantId, b.DistanceToHydrantX, b.DistanceToHydrantY,
                           m.Latitude, m.Longitude, m.GidrantNumber, m.GidrantTruba, b.Latitude, b.Longitude
                    FROM HydrantBindings b
                    INNER JOIN Markers m ON b.HydrantId = m.Id
                    WHERE b.Id = @id", _hydrantsConnection);
                    infoCmd.Parameters.AddWithValue("@id", id);

                    using var reader = infoCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new BindingInfo
                        {
                            Id = id,
                            HydrantId = reader.GetInt32(0),
                            DistanceX = reader.GetDouble(1),
                            DistanceY = reader.GetDouble(2),
                            HydrantNumber = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            HydrantTruba = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Latitude = reader.GetDouble(7),
                            Longitude = reader.GetDouble(8)
                        };
                    }
                }
                finally
                {
                    CloseHydrantsConnection();
                }

                return null;
            });
        }

        public void UpdateBinding(int bindingId, double lat, double lng, double distanceX, double distanceY)
        {
            try
            {
                OpenHydrantsConnection();

                var updateCmd = new SQLiteCommand(
                    "UPDATE HydrantBindings SET Latitude = @lat, Longitude = @lng, " +
                    "DistanceToHydrantX = @distX, DistanceToHydrantY = @distY WHERE Id = @id",
                    _hydrantsConnection);
                updateCmd.Parameters.AddWithValue("@lat", lat);
                updateCmd.Parameters.AddWithValue("@lng", lng);
                updateCmd.Parameters.AddWithValue("@distX", distanceX);
                updateCmd.Parameters.AddWithValue("@distY", distanceY);
                updateCmd.Parameters.AddWithValue("@id", bindingId);
                updateCmd.ExecuteNonQuery();

                // Обновляем кэш
                var binding = GetBindingInfo(bindingId);
                if (binding != null)
                {
                    binding.Latitude = lat;
                    binding.Longitude = lng;
                    binding.DistanceX = distanceX;
                    binding.DistanceY = distanceY;
                    _cacheManager.UpdateBinding(binding);
                }
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public void DeleteBinding(int bindingId)
        {
            try
            {
                OpenHydrantsConnection();

                var deleteCmd = new SQLiteCommand(
                    "DELETE FROM HydrantBindings WHERE Id = @id",
                    _hydrantsConnection);
                deleteCmd.Parameters.AddWithValue("@id", bindingId);
                deleteCmd.ExecuteNonQuery();

                // Обновляем кэш
                _cacheManager.RemoveBinding(bindingId);
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public void DeleteBindingForHydrant(int hydrantId)
        {
            try
            {
                OpenHydrantsConnection();

                var deleteCmd = new SQLiteCommand(
                    "DELETE FROM HydrantBindings WHERE HydrantId = @hydrantId",
                    _hydrantsConnection);
                deleteCmd.Parameters.AddWithValue("@hydrantId", hydrantId);
                deleteCmd.ExecuteNonQuery();

                // Инвалидируем кэш
                _cacheManager.InvalidateBindings();
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        #endregion

        #region Работа с принадлежностями

        public List<CompanyInfo> GetAllCompanies()
        {
            var companies = new List<CompanyInfo>();

            try
            {
                OpenHydrantsConnection();

                var cmd = new SQLiteCommand(
                    "SELECT Id, Name FROM Companies ORDER BY Name",
                    _hydrantsConnection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    companies.Add(new CompanyInfo
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }
            }
            finally
            {
                CloseHydrantsConnection();
            }

            return companies;
        }

        public void AddCompany(string name)
        {
            try
            {
                OpenHydrantsConnection();

                var cmd = new SQLiteCommand(
                    "INSERT INTO Companies (Name) VALUES (@name)",
                    _hydrantsConnection);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        public void DeleteCompany(int id)
        {
            try
            {
                OpenHydrantsConnection();

                var cmd = new SQLiteCommand(
                    "DELETE FROM Companies WHERE Id = @id",
                    _hydrantsConnection);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                CloseHydrantsConnection();
            }
        }

        #endregion
    }
}