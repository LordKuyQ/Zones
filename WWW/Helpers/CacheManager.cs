using System;
using System.Collections.Generic;
using System.Linq;
using ZoneHydrantEditor.Models;
using GMap.NET;

namespace ZoneHydrantEditor.Helpers
{
    internal class CacheManager
    {
        // Словари для кэширования данных
        private Dictionary<int, string> _zonesCache = [];
        private readonly Dictionary<int, List<PointLatLng>> _zonePointsCache = [];
        private readonly Dictionary<int, ZoneInfo> _zoneBoundsCache = [];
        private List<MarkerInfo> _markersCache = [];
        private List<BindingInfo> _bindingsCache = [];
        private readonly Dictionary<int, List<int>> _markersByZoneCache = [];
        private readonly Dictionary<int, List<BindingInfo>> _bindingsByHydrantCache = [];

        // Флаги актуальности кэша
        private bool _zonesDirty = true;
        private bool _zonePointsDirty = true;
        private bool _zoneBoundsDirty = true;
        private bool _markersDirty = true;
        private bool _bindingsDirty = true;

        // Блокировки для потокобезопасности
        private readonly object _zonesLock = new();
        private readonly object _zonePointsLock = new();
        private readonly object _zoneBoundsLock = new();
        private readonly object _markersLock = new();
        private readonly object _bindingsLock = new();

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
            lock (_markersLock)
            {
                _markersCache.Clear();
                _markersByZoneCache.Clear();
                _markersDirty = true;
            }
            lock (_bindingsLock)
            {
                _bindingsCache.Clear();
                _bindingsByHydrantCache.Clear();
                _bindingsDirty = true;
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

        public void InvalidateMarkers()
        {
            lock (_markersLock)
            {
                _markersDirty = true;
                _markersByZoneCache.Clear();
            }
        }

        public void InvalidateBindings()
        {
            lock (_bindingsLock)
            {
                _bindingsDirty = true;
                _bindingsByHydrantCache.Clear();
            }
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

        #region Кэширование гидрантов
        public List<MarkerInfo> GetAllMarkers(Func<List<MarkerInfo>> loadFunc)
        {
            lock (_markersLock)
            {
                if (_markersDirty || _markersCache.Count == 0)
                {
                    _markersCache = loadFunc();
                    _markersDirty = false;
                    _markersByZoneCache.Clear();
                    foreach (var marker in _markersCache)
                    {
                        if (marker.ZoneId.HasValue)
                        {
                            if (!_markersByZoneCache.ContainsKey(marker.ZoneId.Value))
                                _markersByZoneCache[marker.ZoneId.Value] = [];

                            _markersByZoneCache[marker.ZoneId.Value].Add(marker.Id);
                        }
                    }
                }
                return [.. _markersCache];
            }
        }
        public MarkerInfo GetMarkerById(int markerId, Func<int, MarkerInfo> loadFunc)
        {
            lock (_markersLock)
            {
                var marker = _markersCache.FirstOrDefault(m => m.Id == markerId);
                if (marker != null && !_markersDirty)
                {
                    return marker;
                }
                marker = loadFunc(markerId);
                if (marker != null)
                {
                    var index = _markersCache.FindIndex(m => m.Id == markerId);
                    if (index >= 0)
                        _markersCache[index] = marker;
                    else
                        _markersCache.Add(marker);

                    _markersDirty = true;
                }
                return marker;
            }
        }
        public int GetMarkerIdByCoordinates(double lat, double lng)
        {
            lock (_markersLock)
            {
                var marker = _markersCache.FirstOrDefault(m =>
                    Math.Abs(m.Latitude - lat) < 0.000001 &&
                    Math.Abs(m.Longitude - lng) < 0.000001);
                return marker?.Id ?? -1;
            }
        }
        public List<MarkerInfo> GetMarkersInZone(int zoneId, Func<int, List<MarkerInfo>> loadFunc)
        {
            lock (_markersLock)
            {
                if (_markersDirty || !_markersByZoneCache.TryGetValue(zoneId, out List<int>? markerIds))
                {
                    var markers = loadFunc(zoneId);
                    foreach (var marker in markers)
                    {
                        var index = _markersCache.FindIndex(m => m.Id == marker.Id);
                        if (index >= 0)
                            _markersCache[index] = marker;
                        else
                            _markersCache.Add(marker);
                    }
                    markerIds = [.. markers.Select(m => m.Id)];
                    _markersByZoneCache[zoneId] = markerIds;
                    return markers;
                }
                return [.. _markersCache.Where(m => markerIds.Contains(m.Id))];
            }
        }
        #endregion

        #region Кэширование привязок
        public List<BindingInfo> GetAllBindings(Func<List<BindingInfo>> loadFunc)
        {
            lock (_bindingsLock)
            {
                if (_bindingsDirty || _bindingsCache.Count == 0)
                {
                    _bindingsCache = loadFunc();
                    _bindingsByHydrantCache.Clear();
                    foreach (var binding in _bindingsCache)
                    {
                        if (!_bindingsByHydrantCache.ContainsKey(binding.HydrantId))
                            _bindingsByHydrantCache[binding.HydrantId] = [];

                        _bindingsByHydrantCache[binding.HydrantId].Add(binding);
                    }
                    _bindingsDirty = false;
                }
                return [.. _bindingsCache];
            }
        }
        public List<BindingInfo> GetBindingsForHydrant(int hydrantId, Func<int, List<BindingInfo>> loadFunc)
        {
            lock (_bindingsLock)
            {
                if (_bindingsDirty || !_bindingsByHydrantCache.TryGetValue(hydrantId, out List<BindingInfo>? value))
                {
                    var bindings = loadFunc(hydrantId);
                    _bindingsCache.RemoveAll(b => b.HydrantId == hydrantId);
                    _bindingsCache.AddRange(bindings);
                    value = bindings;
                    _bindingsByHydrantCache[hydrantId] = value;
                    return bindings;
                }
                return [.. value];
            }
        }
        public BindingInfo GetBindingInfo(int bindingId, Func<int, BindingInfo> loadFunc)
        {
            lock (_bindingsLock)
            {
                var binding = _bindingsCache.FirstOrDefault(b => b.Id == bindingId);
                if (binding != null && !_bindingsDirty) return binding;
                binding = loadFunc(bindingId);
                if (binding != null)
                {
                    var index = _bindingsCache.FindIndex(b => b.Id == bindingId);
                    if (index >= 0)
                        _bindingsCache[index] = binding;
                    else
                        _bindingsCache.Add(binding);

                    _bindingsDirty = true;
                }
                return binding;
            }
        }
        public bool HasExistingBinding(int hydrantId)
        {
            lock (_bindingsLock)
            {
                if (_bindingsDirty)
                    return false;
                return _bindingsCache.Any(b => b.HydrantId == hydrantId);
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
        public void AddMarker(MarkerInfo marker)
        {
            lock (_markersLock)
            {
                _markersCache.Add(marker);
                if (marker.ZoneId.HasValue)
                {
                    if (!_markersByZoneCache.ContainsKey(marker.ZoneId.Value))
                        _markersByZoneCache[marker.ZoneId.Value] = [];
                    _markersByZoneCache[marker.ZoneId.Value].Add(marker.Id);
                }
            }
        }
        public void UpdateMarker(MarkerInfo marker)
        {
            lock (_markersLock)
            {
                var index = _markersCache.FindIndex(m => m.Id == marker.Id);
                if (index >= 0)
                {
                    var oldZone = _markersCache[index].ZoneId;
                    _markersCache[index] = marker;
                    if (oldZone != marker.ZoneId)
                    {
                        if (oldZone.HasValue && _markersByZoneCache.TryGetValue(oldZone.Value, out List<int>? value))
                            value.Remove(marker.Id);

                        if (marker.ZoneId.HasValue)
                        {
                            if (!_markersByZoneCache.ContainsKey(marker.ZoneId.Value))
                                _markersByZoneCache[marker.ZoneId.Value] = [];

                            _markersByZoneCache[marker.ZoneId.Value].Add(marker.Id);
                        }
                    }
                }
            }
        }
        public void RemoveMarker(int markerId)
        {
            lock (_markersLock)
            {
                var marker = _markersCache.FirstOrDefault(m => m.Id == markerId);
                if (marker != null)
                {
                    if (marker.ZoneId.HasValue && _markersByZoneCache.TryGetValue(marker.ZoneId.Value, out List<int>? value))
                        value.Remove(markerId);
                    _markersCache.Remove(marker);
                }
            }
            lock (_bindingsLock)
            {
                _bindingsCache.RemoveAll(b => b.HydrantId == markerId);
                _bindingsByHydrantCache.Remove(markerId);
            }
        }
        public void AddBinding(BindingInfo binding)
        {
            lock (_bindingsLock)
            {
                _bindingsCache.Add(binding);
                if (!_bindingsByHydrantCache.ContainsKey(binding.HydrantId))
                    _bindingsByHydrantCache[binding.HydrantId] = [];
                _bindingsByHydrantCache[binding.HydrantId].Add(binding);
            }
        }
        public void UpdateBinding(BindingInfo binding)
        {
            lock (_bindingsLock)
            {
                var index = _bindingsCache.FindIndex(b => b.Id == binding.Id);
                if (index >= 0)
                {
                    var oldHydrantId = _bindingsCache[index].HydrantId;
                    _bindingsCache[index] = binding;
                    if (oldHydrantId != binding.HydrantId)
                    {
                        if (_bindingsByHydrantCache.TryGetValue(oldHydrantId, out List<BindingInfo>? value))
                            value.RemoveAll(b => b.Id == binding.Id);

                        if (!_bindingsByHydrantCache.ContainsKey(binding.HydrantId))
                            _bindingsByHydrantCache[binding.HydrantId] = [];
                        _bindingsByHydrantCache[binding.HydrantId].Add(binding);
                    }
                }
            }
        }
        public void RemoveBinding(int bindingId)
        {
            lock (_bindingsLock)
            {
                var binding = _bindingsCache.FirstOrDefault(b => b.Id == bindingId);
                if (binding != null)
                {
                    if (_bindingsByHydrantCache.TryGetValue(binding.HydrantId, out List<BindingInfo>? value))
                        value.RemoveAll(b => b.Id == bindingId);
                    _bindingsCache.Remove(binding);
                }
            }
        }
        #endregion
    }
}