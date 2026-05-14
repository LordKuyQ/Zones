using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TestDbApp.Models;

namespace ZoneHydrantEditor.Helpers
{
    public class EwsMapDataService : IDisposable
    {
        private readonly SQLiteConnection _connection;

        public EwsMapDataService(string? dbPath = null)
        {
            var path = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ews_map.db");
            _connection = new SQLiteConnection($"Data Source={path};Version=3;");
            _connection.Open();
        }

        public void Dispose()
        {
            if (_connection.State != ConnectionState.Closed)
                _connection.Close();
            _connection.Dispose();
        }

        #region Query helpers
        public List<Ewss> GetAllEwss() => Query<Ewss>("EWSs");
        public List<_05Organization> GetAllOrganizations() => Query<_05Organization>("05_Organizations");
        public List<_04AdressObject> GetAllAdressObjects() => Query<_04AdressObject>("04_AdressObject");
        public List<_00Region> GetAllRegions() => Query<_00Region>("00_Region");
        public List<_000Setting> GetAllSettings000() => Query<_000Setting>("000_Settings");
        public List<Unit> GetAllUnits() => Query<Unit>("Units");
        public List<Setting> GetAllSettings() => Query<Setting>("Settings");
        public List<Street> GetAllStreets() => Query<Street>("Streets");
        public List<Streets1> GetAllStreets1() => Query<Streets1>("Streets1");
        public List<StreetPrefix> GetAllStreetPrefixes() => Query<StreetPrefix>("Street_Prefixes");
        public List<Fio> GetAllFios() => Query<Fio>("FIOs");
        public List<OwnerType> GetAllOwnerTypes() => Query<OwnerType>("OwnerTypes");
        public List<Thesauru> GetAllThesaurus() => Query<Thesauru>("Thesaurus");
        public List<SwitchboardItem> GetAllSwitchboardItems() => Query<SwitchboardItem>("Switchboard Items");
        public List<UserSetting> GetAllUserSettings() => Query<UserSetting>("UserSettings");
        public List<EwsType> GetAllEwsTypes() => Query<EwsType>("EWS_Type");
        public List<EwsDiameter> GetAllEwsDiameters() => Query<EwsDiameter>("EWS_Diameter");
        public List<EwsPkdiameter> GetAllEwsPkdiameters() => Query<EwsPkdiameter>("EWS_PKDiameter");
        public List<EwsPipeType> GetAllEwsPipeTypes() => Query<EwsPipeType>("EWS_PipeType");
        public List<EwsValue> GetAllEwsValues() => Query<EwsValue>("EWS_Value");
        public List<EwsListitem> GetAllEwsListitems() => Query<EwsListitem>("EWS_Listitems");
        public List<EwssCheck> GetAllEwssChecks() => Query<EwssCheck>("EWSs_Check");
        public List<CEwsCheckType> GetAllCEwsCheckTypes() => Query<CEwsCheckType>("cEWS_CheckType");
        public List<CEwsStatus> GetAllCEwsStatuses() => Query<CEwsStatus>("cEWS_Status");
        public List<CEwsTrouble> GetAllCEwsTroubles() => Query<CEwsTrouble>("cEWS_Trouble");
        public List<CEwsTroubleType> GetAllCEwsTroubleTypes() => Query<CEwsTroubleType>("cEWS_TroubleType");
        public List<_1TerritorialFireGarrison> GetAllTerritorialFireGarrisons() => Query<_1TerritorialFireGarrison>("1_TerritorialFireGarrison");
        public List<_2FireGroup> GetAllFireGroups() => Query<_2FireGroup>("2_FireGroup");
        public List<_3LocalFireGarrison> GetAllLocalFireGarrisons() => Query<_3LocalFireGarrison>("3_LocalFireGarrison");
        public List<_4FireUnit> GetAllFireUnits() => Query<_4FireUnit>("4_FireUnit");
        public List<_5Staff> GetAllStaff() => Query<_5Staff>("5_Staff");
        public List<_6StaffUser> GetAllStaffUsers() => Query<_6StaffUser>("6_StaffUser");
        public List<КопияEwss> GetAllCopyEwss() => Query<КопияEwss>("Копия EWSs");
        public List<КопияEwssCheck> GetAllCopyEwssChecks() => Query<КопияEwssCheck>("Копия EWSs_Checks");
        public List<ОшибкиВставки> GetAllInsertErrors() => Query<ОшибкиВставки>("Ошибки вставки");
        public List<ОшибкиСохраненияПриАвтозаменеИмен> GetAllAutoRenameErrors() => Query<ОшибкиСохраненияПриАвтозаменеИмен>("Ошибки сохранения при автозамене имен");
        #endregion

        #region Ewss CRUD
        public void InsertEwss(Ewss ewss)
        {
            using var cmd = new SQLiteCommand(@"
                INSERT INTO EWSs (EWS_ID, EWS_Number, EWS_GeoCoord_X, EWS_GeoCoord_Y,
                    EWS_Type_COD, EWS_PipeType_COD, EWS_Diameter_COD, EWS_PKDiameter_COD,
                    EWS_AdressObject_COD, EWS_HouseNumber, EWS_AdressNote,
                    EWS_Organization_COD, EWS_Status_COD,
                    EWS_Priviazka, EWS_Priviazka_GeoX, EWS_Priviazka_GeoY,
                    EWS_Notes, EWS_Map_ID, Record_Created, Record_User_COD, Record_Status, EWS_FireUnit_COD)
                VALUES (@id, @num, @lat, @lng,
                    @type, @pipe, @diam, @pkdiam,
                    @addrObj, @house, @addrNote,
                    @org, @status,
                    @priv, @privX, @privY,
                    @notes, @mapId, @created, @user, @recStatus, @fireUnit)",
                _connection);
            AddEwssParams(cmd, ewss);
            cmd.ExecuteNonQuery();
        }

        public void UpdateEwss(Ewss ewss)
        {
            using var cmd = new SQLiteCommand(@"
                UPDATE EWSs SET
                    EWS_Number = @num, EWS_GeoCoord_X = @lat, EWS_GeoCoord_Y = @lng,
                    EWS_Type_COD = @type, EWS_PipeType_COD = @pipe, EWS_Diameter_COD = @diam,
                    EWS_PKDiameter_COD = @pkdiam,
                    EWS_AdressObject_COD = @addrObj, EWS_HouseNumber = @house, EWS_AdressNote = @addrNote,
                    EWS_Organization_COD = @org, EWS_Status_COD = @status,
                    EWS_Priviazka = @priv, EWS_Priviazka_GeoX = @privX, EWS_Priviazka_GeoY = @privY,
                    EWS_Notes = @notes, EWS_Map_ID = @mapId, Record_User_COD = @user, Record_Status = @recStatus,
                    EWS_FireUnit_COD = @fireUnit
                WHERE EWS_ID = @id",
                _connection);
            AddEwssParams(cmd, ewss);
            cmd.ExecuteNonQuery();
        }

        public void DeleteEwss(string ewsId)
        {
            using var cmd = new SQLiteCommand("DELETE FROM EWSs WHERE EWS_ID = @id", _connection);
            cmd.Parameters.AddWithValue("@id", ewsId);
            cmd.ExecuteNonQuery();
        }

        public Ewss? GetEwssById(string ewsId)
        {
            var all = GetAllEwss();
            return all.FirstOrDefault(e => e.EwsId == ewsId);
        }

        public Ewss? GetEwssByMarkerId(int markerId)
        {
            var all = GetAllEwss();
            return all.FirstOrDefault(e => Utility.GetStableMarkerId(e.EwsId) == markerId);
        }

        public List<Ewss> GetAllEwssWithDisplay()
        {
            var ewss = GetAllEwss();
            var orgs = GetAllOrganizations().ToDictionary(o => o.OrganizationId, o => o.OrganizationNameShort ?? "");
            var statuses = GetAllCEwsStatuses().ToDictionary(s => s.EwsStatusId, s => s.EwsStatusName ?? "Непроверенный");
            var types = GetAllEwsTypes().ToDictionary(t => t.EwsTypeId, t => t.EwsTypeNameShort ?? "");
            var diameters = GetAllEwsDiameters().ToDictionary(d => d.EwsDiameterId, d => d.EwsDiameter1 ?? "");
            var pipeTypes = GetAllEwsPipeTypes().ToDictionary(p => p.EwsPipeTypeId, p => p.EwsPipeTypeName ?? "");
            var addresses = GetAllAdressObjects().ToDictionary(a => a.AdressObjectId, a => a.AdressObjectName ?? "");

            foreach (var e in ewss)
            {
                e.OrganizationName = e.EwsOrganizationCod != null && orgs.TryGetValue(e.EwsOrganizationCod, out var org) ? org : "Бесхозный";
                e.StatusName = e.EwsStatusCod != null && statuses.TryGetValue(e.EwsStatusCod, out var st) ? st : "Непроверенный";

                var addrParts = new List<string>();
                if (e.EwsAdressObjectCod != null && addresses.TryGetValue(e.EwsAdressObjectCod, out var addr))
                    addrParts.Add(addr);
                if (!string.IsNullOrWhiteSpace(e.EwsHouseNumber))
                    addrParts.Add(e.EwsHouseNumber);
                if (!string.IsNullOrWhiteSpace(e.EwsAdressNote))
                    addrParts.Add(e.EwsAdressNote);
                e.AddressText = string.Join(", ", addrParts);

                var trubaParts = new List<string>();
                if (e.EwsPipeTypeCod != null && pipeTypes.TryGetValue(e.EwsPipeTypeCod, out var pt))
                    trubaParts.Add(pt);
                if (e.EwsDiameterCod != null && diameters.TryGetValue(e.EwsDiameterCod, out var d))
                    trubaParts.Add(d);
                e.PipeInfo = string.Join(" ", trubaParts);

                e.DisplayNumber = e.EwsNumber ?? "";
            }
            return ewss;
        }

        private static void AddEwssParams(SQLiteCommand cmd, Ewss ewss)
        {
            cmd.Parameters.AddWithValue("@id", (object)ewss.EwsId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@num", (object)ewss.EwsNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lat", (object)ewss.EwsGeoCoordX ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lng", (object)ewss.EwsGeoCoordY ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@type", (object)ewss.EwsTypeCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pipe", (object)ewss.EwsPipeTypeCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@diam", (object)ewss.EwsDiameterCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pkdiam", (object)ewss.EwsPkdiameterCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@addrObj", (object)ewss.EwsAdressObjectCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@house", (object)ewss.EwsHouseNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@addrNote", (object)ewss.EwsAdressNote ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@org", (object)ewss.EwsOrganizationCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (object)ewss.EwsStatusCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@priv", (object)ewss.EwsPriviazka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@privX", (object)ewss.EwsPriviazkaGeoX ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@privY", (object)ewss.EwsPriviazkaGeoY ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)ewss.EwsNotes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mapId", (object)ewss.EwsMapId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", (object)ewss.RecordCreated ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@user", (object)ewss.RecordUserCod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recStatus", (object)ewss.RecordStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fireUnit", (object)ewss.EwsFireUnitCod ?? DBNull.Value);
        }
        #endregion

        #region Binding operations (Ewss fields)
        public void UpdateEwssBinding(string ewsId, string? priviazka, string? geoX, string? geoY)
        {
            using var cmd = new SQLiteCommand(
                "UPDATE EWSs SET EWS_Priviazka = @priv, EWS_Priviazka_GeoX = @geoX, EWS_Priviazka_GeoY = @geoY WHERE EWS_ID = @id",
                _connection);
            cmd.Parameters.AddWithValue("@id", ewsId);
            cmd.Parameters.AddWithValue("@priv", (object)priviazka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@geoX", (object)geoX ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@geoY", (object)geoY ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void ClearEwssBinding(string ewsId)
        {
            using var cmd = new SQLiteCommand(
                "UPDATE EWSs SET EWS_Priviazka = NULL, EWS_Priviazka_GeoX = NULL, EWS_Priviazka_GeoY = NULL WHERE EWS_ID = @id",
                _connection);
            cmd.Parameters.AddWithValue("@id", ewsId);
            cmd.ExecuteNonQuery();
        }
        #endregion

        #region Organization operations
        public void AddOrganization(string name)
        {
            var maxId = GetAllOrganizations()
                .Select(o => int.TryParse(o.OrganizationId, out var id) ? id : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            using var cmd = new SQLiteCommand(
                "INSERT INTO 05_Organizations (Organization_ID, Organization_NameShort, Organization_NameFull) VALUES (@id, @short, @full)",
                _connection);
            cmd.Parameters.AddWithValue("@id", maxId.ToString());
            cmd.Parameters.AddWithValue("@short", name);
            cmd.Parameters.AddWithValue("@full", name);
            cmd.ExecuteNonQuery();
        }
        #endregion

        #region Generic Query
        private List<T> Query<T>(string tableName) where T : new()
        {
            var result = new List<T>();
            var type = typeof(T);

            var dbColumns = GetTableColumns(tableName);
            var propertyMap = BuildPropertyMap(type, dbColumns);

            if (propertyMap.Count == 0)
                return result;

            var columnList = string.Join(", ", propertyMap.Keys.Select(EscapeName));
            using var cmd = new SQLiteCommand($"SELECT {columnList} FROM {EscapeName(tableName)}", _connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var item = new T();
                foreach (var kvp in propertyMap)
                {
                    var colName = kvp.Key;
                    var prop = kvp.Value;
                    try
                    {
                        var ordinal = reader.GetOrdinal(colName);
                        if (reader.IsDBNull(ordinal))
                            continue;

                        var value = reader.GetValue(ordinal);
                        SetPropertyValue(item, prop, value);
                    }
                    catch
                    {
                    }
                }
                result.Add(item);
            }

            return result;
        }

        private static string EscapeName(string name)
        {
            if (name.Length > 0 && (char.IsDigit(name[0]) || !name.All(c => char.IsLetterOrDigit(c) || c == '_')))
                return $"[{name}]";
            return name;
        }

        private List<string> GetTableColumns(string tableName)
        {
            var columns = new List<string>();
            using var cmd = new SQLiteCommand($"PRAGMA table_info({EscapeName(tableName)})", _connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
            return columns;
        }

        private static Dictionary<string, PropertyInfo> BuildPropertyMap(Type type, List<string> dbColumns)
        {
            var map = new Dictionary<string, PropertyInfo>();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var matched = FindMatchingColumn(prop.Name, dbColumns);
                if (matched != null)
                    map[matched] = prop;
            }

            return map;
        }

        private static string? FindMatchingColumn(string propName, List<string> dbColumns)
        {
            var propNorm = NormalizePropertyName(propName);

            foreach (var col in dbColumns)
            {
                var colNorm = NormalizeColumnName(col);
                if (string.Equals(propNorm, colNorm, StringComparison.OrdinalIgnoreCase))
                    return col;
            }

            return null;
        }

        private static string NormalizePropertyName(string name)
        {
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        private static string NormalizeColumnName(string name)
        {
            var raw = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    raw.Append(c);
            }

            var parts = raw.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                if (part.All(char.IsUpper) && part.Length > 1)
                {
                    sb.Append(part[0]);
                    sb.Append(part.Substring(1).ToLowerInvariant());
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                        sb.Append(part.Substring(1));
                }
            }
            return sb.ToString();
        }

        private static void SetPropertyValue<T>(T item, PropertyInfo prop, object value)
        {
            try
            {
                var targetType = prop.PropertyType;
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                var effectiveType = underlyingType ?? targetType;

                if (value == DBNull.Value || value == null)
                {
                    if (!effectiveType.IsValueType || underlyingType != null)
                        prop.SetValue(item, null);
                    return;
                }

                if (effectiveType == typeof(string))
                {
                    prop.SetValue(item, Convert.ToString(value));
                }
                else if (effectiveType == typeof(decimal))
                {
                    prop.SetValue(item, Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                }
                else if (effectiveType == typeof(DateTime))
                {
                    if (value is string s)
                        prop.SetValue(item, DateTime.Parse(s, CultureInfo.InvariantCulture));
                    else
                        prop.SetValue(item, Convert.ToDateTime(value));
                }
                else if (effectiveType == typeof(int))
                {
                    prop.SetValue(item, Convert.ToInt32(value));
                }
                else if (effectiveType == typeof(double))
                {
                    prop.SetValue(item, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else if (effectiveType == typeof(long))
                {
                    prop.SetValue(item, Convert.ToInt64(value));
                }
                else if (effectiveType == typeof(byte[]))
                {
                    if (value is byte[] bytes)
                        prop.SetValue(item, bytes);
                    else if (value is string s)
                        prop.SetValue(item, Encoding.UTF8.GetBytes(s));
                }
                else if (effectiveType == typeof(bool))
                {
                    prop.SetValue(item, Convert.ToBoolean(value));
                }
                else if (effectiveType.IsEnum)
                {
                    prop.SetValue(item, Enum.ToObject(effectiveType, value));
                }
                else
                {
                    prop.SetValue(item, Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture));
                }
            }
            catch
            {
            }
        }
        #endregion
    }
}
