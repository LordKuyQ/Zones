using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TestDbApp.Models;
using ZoneHydrantEditor.Models;

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

        public List<MarkerInfo> GetAllHydrantsAsMarkers()
        {
            var ewss = GetAllEwss();
            var orgs = GetAllOrganizations().ToDictionary(o => o.OrganizationId, o => o.OrganizationNameShort ?? "");
            var statuses = GetAllCEwsStatuses().ToDictionary(s => s.EwsStatusId, s => s.EwsStatusName ?? "Непроверенный");
            var types = GetAllEwsTypes().ToDictionary(t => t.EwsTypeId, t => t.EwsTypeNameShort ?? "");
            var diameters = GetAllEwsDiameters().ToDictionary(d => d.EwsDiameterId, d => d.EwsDiameter1 ?? "");
            var pipeTypes = GetAllEwsPipeTypes().ToDictionary(p => p.EwsPipeTypeId, p => p.EwsPipeTypeName ?? "");
            var addresses = GetAllAdressObjects().ToDictionary(a => a.AdressObjectId, a => a.AdressObjectName ?? "");

            return ewss.Select(e =>
            {
                var truba = new List<string>();
                if (e.EwsPipeTypeCod != null && pipeTypes.TryGetValue(e.EwsPipeTypeCod, out var pt))
                    truba.Add(pt);
                if (e.EwsDiameterCod != null && diameters.TryGetValue(e.EwsDiameterCod, out var d))
                    truba.Add(d);

                var address = new List<string>();
                if (e.EwsAdressObjectCod != null && addresses.TryGetValue(e.EwsAdressObjectCod, out var addr))
                    address.Add(addr);
                if (!string.IsNullOrWhiteSpace(e.EwsHouseNumber))
                    address.Add(e.EwsHouseNumber);
                if (!string.IsNullOrWhiteSpace(e.EwsAdressNote))
                    address.Add(e.EwsAdressNote);

                return new MarkerInfo
                {
                    Id = int.TryParse(e.EwsId, out var id) ? id : 0,
                    Latitude = (double)(e.EwsGeoCoordX ?? 0),
                    Longitude = (double)(e.EwsGeoCoordY ?? 0),
                    GidrantNumber = e.EwsNumber ?? "",
                    GidrantTruba = string.Join(" ", truba),
                    GidrantAdres = string.Join(", ", address),
                    CompanyName = e.EwsOrganizationCod != null && orgs.TryGetValue(e.EwsOrganizationCod, out var org) ? org : "Бесхозный",
                    Status = e.EwsStatusCod != null && statuses.TryGetValue(e.EwsStatusCod, out var st) ? st : "Непроверенный",
                    BreakReason = ""
                };
            }).ToList();
        }

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
    }
}
