using GMap.NET;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using TestDbApp.Models;
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

        #region Работа с принадлежностями

        public List<_05Organization> GetAllCompanies()
        {
            var companies = new List<_05Organization>();

            try
            {
                OpenHydrantsConnection();

                var cmd = new SQLiteCommand(
                    "SELECT Id, Name FROM Companies ORDER BY Name",
                    _hydrantsConnection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    companies.Add(new _05Organization
                    {
                        OrganizationId = reader.GetInt32(0).ToString(),
                        OrganizationNameShort = reader.GetString(1)
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