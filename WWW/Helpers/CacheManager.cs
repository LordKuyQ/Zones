using System;
using System.Collections.Generic;
using System.Linq;
using ZoneHydrantEditor.Models;
using GMap.NET;

namespace ZoneHydrantEditor.Helpers
{
    internal class CacheManager
    {
        private Dictionary<int, string> _zonesCache = [];
        private readonly Dictionary<int, List<PointLatLng>> _zonePointsCache = [];
        private readonly Dictionary<int, ZoneInfo> _zoneBoundsCache = [];

        private bool _zonesDirty = true;
        private bool _zonePointsDirty = true;
        private bool _zoneBoundsDirty = true;

        private readonly object _zonesLock = new();
        private readonly object _zonePointsLock = new();
        private readonly object _zoneBoundsLock = new();

        #region Управление кэшем
        public void ClearAllCache()
        {
            lock (_zonesLock)
            {
                _zonesCache.Clear();
                _zonesDirty = true;
            }
            lock (_zonePointsLock)
            {
                _zonePointsCache.Clear();
                _zonePointsDirty = true;
            }
            lock (_zoneBoundsLock)
            {
                _zoneBoundsCache.Clear();
                _zoneBoundsDirty = true;
            }
        }
        public void InvalidateZone(int zoneId)
        {
            lock (_zonePointsLock)
            {
                _zonePointsCache.Remove(zoneId);
            }
            lock (_zoneBoundsLock)
            {
                _zoneBoundsCache.Remove(zoneId);
            }
            lock (_zonesLock) _zonesDirty = true;
        }
        #endregion

        #region Кэширование зон
        public Dictionary<int, string> GetZones(Func<Dictionary<int, string>> loadFunc)
        {
            lock (_zonesLock)
            {
                if (_zonesDirty || _zonesCache.Count == 0)
                {
                    _zonesCache = loadFunc();
                    _zonesDirty = false;
                }
                return new Dictionary<int, string>(_zonesCache);
            }
        }
        public List<PointLatLng> GetZonePoints(int zoneId, Func<int, List<PointLatLng>> loadFunc)
        {
            lock (_zonePointsLock)
            {
                if (!_zonePointsCache.TryGetValue(zoneId, out var points) || _zonePointsDirty)
                {
                    points = loadFunc(zoneId);
                    _zonePointsCache[zoneId] = points;
                }
                return [.. points];
            }
        }
        public ZoneInfo GetZoneBounds(int zoneId, Func<int, ZoneInfo> loadFunc)
        {
            lock (_zoneBoundsLock)
            {
                if (!_zoneBoundsCache.TryGetValue(zoneId, out var bounds) || _zoneBoundsDirty)
                {
                    bounds = loadFunc(zoneId);
                    _zoneBoundsCache[zoneId] = bounds;
                }
                return bounds;
            }
        }
        #endregion

        #region Обновление кэша
        public void AddZone(int zoneId, string zoneName)
        {
            lock (_zonesLock)
            {
                _zonesCache[zoneId] = zoneName;
            }
        }
        public void RenameZone(int zoneId, string newName)
        {
            lock (_zonesLock)
            {
                if (_zonesCache.ContainsKey(zoneId))
                    _zonesCache[zoneId] = newName;
            }
        }
        public void UpdateZonePoints(int zoneId)
        {
            lock (_zonePointsLock)
            {
                _zonePointsCache[zoneId] = [];
            }
            lock (_zoneBoundsLock)
            {
                _zoneBoundsCache.Remove(zoneId);
            }
        }
        #endregion
    }
}
