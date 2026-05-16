using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TestDbApp.Models;
using ZoneHydrantEditor.GraphicElements;
using ZoneHydrantEditor.Helpers;
using ZoneHydrantEditor.Models;
using ZoneHydrantEditor.UserInput;
using Drawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;

namespace ZoneHydrantEditor
{
    public partial class MainWindow : Window
    {
        #region ПОЛЯ И СВОЙСТВА
        private readonly DatabaseService _dbService;
        private readonly EwsMapDataService _ewsService;
        private readonly BackupService _backupService;
        private const string ZonesDbFile = "zones0815.db";
        private bool isEditMode = false;
        private int currentZoneId = 1;
        private readonly Dictionary<int, GMapPolygon> allZonesInEditor = [];
        private readonly List<GMapMarker> vertexMarkers = [];
        private GMapMarker draggedVertex;
        private GMapRoute currentRoute;
        private GMapMarker temporaryMarker;
        private GMapMarker movingMarker;
        private bool isMovingMarker = false;
        private readonly List<GMapMarker> bindingMarkers = [];
        private bool isCreatingBinding = false;
        private GMapMarker currentHydrantForBinding;
        private bool isMovingBinding = false;
        private GMapMarker movingBindingMarker;
        private bool isAddMarkerDialogOpen = false;
        private Dictionary<int, string> zonesDictionary = [];
        private readonly RoutingService _routingService;
        private readonly GeocodingHelper _geocodingService;
        private readonly string _currentMBTilesPath = "NewLoadMap.mbtiles";
        private bool _isMbtilesLoaded = false;
        private readonly DispatcherTimer _updateTimer;
        private bool _needsZoomUpdate = false;
        private CityDistrictsHelper _cityDistrictsHelper;
        private Fio _currentUser;
        private const int SyncPageSize = 50;
        private int _historyOffset;
        private int _checkOffset;
        private bool _isHistoryLoading;
        private bool _isCheckLoading;
        private bool _historyHasMore = true;
        private bool _checkHasMore = true;
        private readonly DispatcherTimer _historyDebounce;
        private readonly DispatcherTimer _checkDebounce;
        #endregion

        #region КОНСТРУКТОР И ПОДГОТОВКА ПРИЛОЖЕНИЯ
        public MainWindow()
        {
            InitializeComponent();

            // Сначала показываем выбор пользователя
            if (!ShowUserSelectionDialog())
            {
                Close(); // Закрываем приложение, если пользователь не выбран
                return;
            }

            _dbService = new DatabaseService();
            _ewsService = new EwsMapDataService();
            _backupService = new BackupService(_dbService, _ewsService, ZonesDbFile);
            _routingService = new RoutingService();
            _geocodingService = new GeocodingHelper();
            _routingService.RouteBuilt += OnRouteBuilt;
            _routingService.RouteRemoved += OnRouteRemoved;
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _updateTimer.Tick += (s, e) => { if (_needsZoomUpdate) PerformZoomUpdate(); };

            _historyDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _historyDebounce.Tick += (s, e) => { _historyDebounce.Stop(); LoadHistoryData(reset: true); };

            _checkDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _checkDebounce.Tick += (s, e) => { _checkDebounce.Stop(); LoadCheckData(reset: true); };
            LoadMarkersToDataGrid();
            ApplicationPreparing();

            HistorySearchTextBox.TextChanged += (s, e) => { _historyDebounce.Stop(); _historyDebounce.Start(); };
            HistoryDateFromPicker.SelectedDateChanged += (s, e) => { _historyDebounce.Stop(); _historyDebounce.Start(); };
            HistoryDateToPicker.SelectedDateChanged += (s, e) => { _historyDebounce.Stop(); _historyDebounce.Start(); };

            CheckSearchTextBox.TextChanged += (s, e) => { _checkDebounce.Stop(); _checkDebounce.Start(); };
            CheckDateFromPicker.SelectedDateChanged += (s, e) => { _checkDebounce.Stop(); _checkDebounce.Start(); };
            CheckDateToPicker.SelectedDateChanged += (s, e) => { _checkDebounce.Stop(); _checkDebounce.Start(); };

            Loaded += (s, e) =>
            {
                LoadHistoryData();
                LoadCheckData();
            };
        }
        private bool ShowUserSelectionDialog()
        {
            var dialog = new UserSelectDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedUser != null)
            {
                _currentUser = dialog.SelectedUser;

                // Обновляем заголовок окна с именем пользователя
                string userName = $"{_currentUser.FioFamily} {_currentUser.FioName}";
                if (!string.IsNullOrEmpty(_currentUser.FioSurname))
                    userName += $" {_currentUser.FioSurname}";

                this.Title = $"Zone Hydrant Editor - Пользователь: {userName}";
                return true;
            }
            return false;
        }

        // Добавьте публичное свойство для доступа к текущему пользователю
        public Fio CurrentUser => _currentUser;

        private async void ApplicationPreparing()
        {
            InitializeMaps();
            _cityDistrictsHelper = new CityDistrictsHelper(HydrantMap);

            if (!_isMbtilesLoaded && File.Exists(_currentMBTilesPath))
            {
                MBTilesProvider.Instance.LoadMBTilesFile(_currentMBTilesPath);
                _isMbtilesLoaded = true;
            }
            LoadZonesToSelector();

            if (zonesDictionary.Count > 0)
            {
                currentZoneId = zonesDictionary.First().Key;
                LoadAllZonesInEditor();
                HighlightCurrentZoneInEditor();
            }
            UpdateMarkersZoneInfo();
            LoadHydrantsFromDatabase();
            LoadBindingsFromDatabase();
            DrawAllZonesOnHydrantMap();
        }
        #endregion

        #region ИНИЦИАЛИЗАЦИЯ КАРТ
        private void InitializeMaps()
        {
            ConfigureMap(ZoneMap, 54.8800, 83.0415);
            ConfigureMap(HydrantMap, 54.8800, 83.0415);
            ZoneMap.MouseMove += ZoneMap_MouseMove;
            HydrantMap.MouseLeftButtonUp += HydrantMap_MouseLeftButtonUp;
            HydrantMap.MouseRightButtonDown += HydrantMap_MouseRightButtonDown;
            _cityDistrictsHelper = new CityDistrictsHelper(HydrantMap);

        }

        private void ConfigureMap(GMapControl map, double lat, double lng)
        {
            map.DragButton = MouseButton.Left;
            map.MaxZoom = 18;
            map.MinZoom = 12;
            map.Zoom = 12;
            map.MapProvider = MBTilesProvider.Instance;
            map.Manager.Mode = AccessMode.ServerAndCache;
            map.Position = new PointLatLng(lat, lng);
            map.OnMapDrag += () =>
            {
                if (map == ZoneMap)
                {
                    foreach (var vertex in vertexMarkers)
                    {
                        if (vertex.Shape is FrameworkElement element)
                            element.Visibility = Visibility.Collapsed;
                    }
                }
                _needsZoomUpdate = true;
                _updateTimer.Stop();
                _updateTimer.Start();
            };
            map.OnMapDrag += () =>
            {
                if (map == HydrantMap)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        map.InvalidateVisual();
                    }), DispatcherPriority.Background);
                }
            };
        }
        #endregion

        #region НЕБОЛЬШАЯ ОПТИМИЗАЦИЯ ПРИ ЗУМИРОВАНИИ И МАСШТАБИРОВАНИЕ МАРКЕРОВ
        private void PerformZoomUpdate()
        {
            _needsZoomUpdate = false;

            if (isEditMode)
            {
                foreach (var vertex in vertexMarkers)
                {
                    if (vertex.Shape is FrameworkElement element)
                        element.Visibility = Visibility.Visible;
                }
            }

            if (MainTabControl.SelectedIndex == 0)
            {
                ZoneMap.InvalidateVisual();
            }
            else if (MainTabControl.SelectedIndex == 1)
            {
                UpdateMarkersScaling();
                HydrantMap.InvalidateVisual();
            }
        }

        private void UpdateMarkersScaling()
        {
            int zoomLevel = (int)HydrantMap.Zoom;
            foreach (var marker in HydrantMap.Markers)
            {
                if (marker is GMapRoute || marker is GMapPolygon) continue;
                if (bindingMarkers.Contains(marker) || marker == temporaryMarker || marker == movingMarker || marker == movingBindingMarker) continue;
                HydrantMarker.UpdateMarkerSize(marker, zoomLevel);
            }
            foreach (var bindingMarker in bindingMarkers)
            {
                BindingMarker.UpdateMarkerSize(bindingMarker, zoomLevel);
            }
        }
        #endregion

        #region ЗАГРУЗКА ДАННЫХ О ЗОНАХ В ПОЛЯ ПРОГРАММЫ И ПЕРЕКЛЮЧЕНИЕ МЕЖДУ ЗОНАМИ(ВКЛ1)
        private void LoadZonesToSelector()
        {
            zonesDictionary = _dbService.LoadZonesToDictionary();
            ZoneSelector.ItemsSource = zonesDictionary;
            ZoneSelector.DisplayMemberPath = "Value";
            ZoneSelector.SelectedValuePath = "Key";
        }

        private void ZoneSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoneSelector.SelectedValue == null) return;

            int newZoneId = (int)ZoneSelector.SelectedValue;
            if (newZoneId == currentZoneId) return;

            if (isEditMode)
            {
                if (MessageBox.Show("Выйти из режима редактирования и переключить общий план?", "Режим редактирования",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    ZoneSelector.SelectedValue = currentZoneId;
                    return;
                }
                ExitEditMode();
            }
            currentZoneId = newZoneId;
            HighlightCurrentZoneInEditor();
        }

        private void LoadAllZonesInEditor()
        {
            foreach (var zone in allZonesInEditor.Values)
            {
                ZoneMap.Markers.Remove(zone);
            }
            allZonesInEditor.Clear();

            foreach (var zone in _dbService.GetAllZones())
            {
                var points = _dbService.GetZonePoints(zone.Id);
                if (points.Count < 3) continue;

                var zonePolygon = CreateZonePolygon(points, zone.Id, Brushes.DimGray, strokeThickness: 3);

                ZoneMap.Markers.Add(zonePolygon);
                allZonesInEditor[zone.Id] = zonePolygon;
            }
        }

        private GMapPolygon CreateZonePolygon(List<PointLatLng> points, object tag,
    Brush stroke, Brush fill = null, double strokeThickness = 2, int zIndex = -1000)
        {
            var path = new System.Windows.Shapes.Path
            {
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                Fill = fill ?? Brushes.Transparent,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(path, zIndex);
            return new GMapPolygon(points) { Shape = path, Tag = tag };
        }

        private void HighlightCurrentZoneInEditor()
        {
            foreach (var zone in allZonesInEditor)
            {
                bool isCurrent = zone.Key == currentZoneId;
                if (zone.Value.Shape is System.Windows.Shapes.Path path)
                {
                    path.Stroke = isCurrent ? Brushes.Red : Brushes.DimGray;
                    path.Fill = isCurrent ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 0, 0)) : null;
                    Panel.SetZIndex(path, isCurrent ? -500 : -1000);
                }
            }
            if (isEditMode)
            {
                LoadEditingZone(currentZoneId);
            }
        }
        #endregion

        #region РЕЖИМ РЕДАКТИРОВАНИЯ ЗОН(ВКЛ1)
        private void EnterEditMode()
        {
            if (zonesDictionary == null || zonesDictionary.Count == 0)
            {
                MessageBox.Show("Нет доступных общих планов для редактирования. Сначала создайте общий план", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!zonesDictionary.ContainsKey(currentZoneId))
            {
                currentZoneId = zonesDictionary.First().Key;
                ZoneSelector.SelectedValue = currentZoneId;
            }
            int pointCount = _dbService.GetZonePointCount(currentZoneId);

            if (pointCount < 3)
            {
                if (MessageBox.Show("Создать общий план?", "Создание общего плана", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                if (pointCount > 0)
                {
                    _dbService.DeleteZonePoints(currentZoneId);
                }
            }
            isEditMode = true;
            ZoneMap.DragButton = MouseButton.Right;

            ZoneMap.MouseLeftButtonDown += ZoneMap_MouseLeftButtonDown;

            HighlightCurrentZoneInEditor();
        }
        private void ZoneMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isEditMode) return;
            var point = ZoneMap.FromLocalToLatLng((int)e.GetPosition(ZoneMap).X, (int)e.GetPosition(ZoneMap).Y);
            AddVertex(point);
            e.Handled = true;
        }
        private void ExitEditMode()
        {
            if (!isEditMode) return;
            SaveZone();
            ZoneMap.DragButton = MouseButton.Left;
            ZoneMap.MouseLeftButtonDown -= ZoneMap_MouseLeftButtonDown;
            isEditMode = false;
            ClearEditingZone();
            RefreshZoneDisplay();
        }

        private void SaveZone()
        {
            try
            {
                if (vertexMarkers.Count < 3)
                {
                    MessageBox.Show("У общего плана должно быть минимум 3 точки для сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var points = vertexMarkers.Select(m => m.Position).ToList();
                _dbService.SaveZonePoints(currentZoneId, points);
                RefreshZoneDisplay();
                MessageBox.Show($"Общий план '{zonesDictionary[currentZoneId]}' сохранен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения общего плана: {ex.Message}");
            }
        }

        private void RefreshZoneDisplay()
        {
            UpdateMarkersZoneInfo();
            DrawAllZonesOnHydrantMap();
            LoadAllZonesInEditor();
        }

        private void LoadEditingZone(int zoneId)
        {
            ClearEditingZone();
            var points = _dbService.GetZonePoints(zoneId);
            if (points.Count < 3) return;

            foreach (var point in points)
            {
                var marker = CreateVertexMarkerInternal(point);
                vertexMarkers.Add(marker);
                ZoneMap.Markers.Add(marker);
            }
        }

        private void ClearEditingZone()
        {
            foreach (var marker in vertexMarkers)
                ZoneMap.Markers.Remove(marker);
            vertexMarkers.Clear();
        }
        #endregion

        #region РАБОТА C ВЕРШИНАМИ(ВКЛ1)
        private void ZoneMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedVertex == null) return;
            draggedVertex.Position = ZoneMap.FromLocalToLatLng((int)e.GetPosition(ZoneMap).X, (int)e.GetPosition(ZoneMap).Y);
            UpdateEditingPolygon();
        }

        private GMapMarker CreateVertexMarkerInternal(PointLatLng position)
        {
            var marker = ZoneMarker.CreateVertexMarker(position, vertexMarkers.Count + 1, false,
                OnVertexMouseLeftButtonDown, OnVertexMouseLeftButtonUp, OnVertexMouseRightButtonDown);
            if (marker.Shape is FrameworkElement element) element.Tag = marker;
            return marker;
        }

        private void AddVertex(PointLatLng p, int? insertIndex = null)
        {
            var marker = CreateVertexMarkerInternal(p);
            if (insertIndex.HasValue) vertexMarkers.Insert(insertIndex.Value, marker);
            else vertexMarkers.Add(marker);
            ZoneMap.Markers.Add(marker);
            UpdateAllVertexNumbers();
        }

        private void DeleteVertex(GMapMarker marker)
        {
            if (!isEditMode || marker == null) return;
            if (vertexMarkers.Count <= 3)
            {
                MessageBox.Show("Нельзя удалить точку. У общего плана должно быть минимум 3 точки.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("Удалить эту точку?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            ZoneMap.Markers.Remove(marker);
            vertexMarkers.Remove(marker);
            UpdateEditingPolygon();
            UpdateAllVertexNumbers();
        }

        private void UpdateEditingPolygon()
        {
            if (allZonesInEditor.TryGetValue(currentZoneId, out var zone))
            {
                zone.Points.Clear();
                foreach (var point in vertexMarkers.Select(m => m.Position)) zone.Points.Add(point);
            }
        }

        private void UpdateAllVertexNumbers()
        {
            for (int i = 0; i < vertexMarkers.Count; i++)
                ZoneMarker.UpdateVertexNumber(vertexMarkers[i], i + 1);
        }

        private void OnVertexMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isEditMode) return;
            var marker = GetMarkerFromSender(sender);
            if (marker == null) return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                int index = vertexMarkers.IndexOf(marker);
                if (index >= 0)
                {
                    var pos = ZoneMap.FromLocalToLatLng((int)e.GetPosition(ZoneMap).X, (int)e.GetPosition(ZoneMap).Y);
                    AddVertex(pos, index + 1);
                }
            }
            else draggedVertex = marker;
            e.Handled = true;
        }

        private void OnVertexMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => draggedVertex = null;

        private void OnVertexMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isEditMode) return;
            e.Handled = true;
            var marker = GetMarkerFromSender(sender);
            if (marker != null) ShowVertexContextMenu(marker);
        }

        private GMapMarker GetMarkerFromSender(object sender)
        {
            var element = sender as FrameworkElement;
            while (element != null)
            {
                if (element.Tag is GMapMarker marker) return marker;
                element = element.Parent as FrameworkElement;
            }
            return null;
        }

        private void ShowVertexContextMenu(GMapMarker marker)
        {
            var menu = new ContextMenu();
            int index = vertexMarkers.IndexOf(marker);

            void AddMenuItem(string header, Action action, Brush foreground = null)
            {
                var item = new MenuItem { Header = header };
                if (foreground != null) item.Foreground = foreground;
                item.Click += (s, args) => action();
                menu.Items.Add(item);
            }

            AddMenuItem("Удалить точку", () => DeleteVertex(marker), Brushes.Red);
            AddMenuItem("Вставить точку после", () =>
            {
                if (index >= 0) AddVertex(CalculateOffsetPosition(marker.Position, 0.00009, 0.00009), index + 1);
            });
            AddMenuItem("Вставить точку перед", () =>
            {
                if (index >= 0) AddVertex(CalculateOffsetPosition(marker.Position, -0.00009, -0.00009), index);
            });

            if (index >= 0)
            {
                menu.Items.Add(new MenuItem
                {
                    Header = $"Точка #{index + 1}\nШирота: {marker.Position.Lat:F7}\nДолгота: {marker.Position.Lng:F7}",
                    IsEnabled = false
                });
            }

            if (marker.Shape is FrameworkElement element) element.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private PointLatLng CalculateOffsetPosition(PointLatLng original, double offsetLat, double offsetLng)
        {
            double adjustedOffsetLng = offsetLng / Math.Cos(original.Lat * Math.PI / 180.0);
            return new PointLatLng(original.Lat + offsetLat, original.Lng + adjustedOffsetLng);
        }
        #endregion

        #region ОБРАБОТЧИКИ ДЕЙСТВИЙ РЕДАКТОР ЗОН НА ПАНЕЛЕ(ВКЛ1)
        private void SaveZoneButton_Click(object sender, RoutedEventArgs e)
        {
            CollapsingButtonsVisibility();
            ExitEditMode();
        }

        private void ResetZoneButton_Click(object sender, RoutedEventArgs e)
        {
            CollapsingButtonsVisibility();
            if (MessageBox.Show("Отменить изменения общего плана?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ClearEditingZone();
                LoadEditingZone(currentZoneId);
            }
        }

        private void AddZoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (isEditMode)
            {
                MessageBox.Show("Выйдите из режима редактирования перед созданием нового общего плана");
                return;
            }

            var dialog = new InputDialog("Введите название нового общего плана:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer))
            {
                string newZoneName = dialog.Answer.Trim();
                if (_dbService.ZoneNameExists(newZoneName))
                {
                    MessageBox.Show($"Общий план с названием '{newZoneName}' уже существует\nВведите другое название", "Ошибка создания зоны", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    int newZoneId = _dbService.CreateZone(newZoneName);
                    LoadZonesToSelector();
                    ZoneSelector.SelectedValue = newZoneId;
                    LoadAllZonesInEditor();
                    currentZoneId = newZoneId;
                    HighlightCurrentZoneInEditor();
                    DrawAllZonesOnHydrantMap();
                    UpdateMarkersZoneInfo();
                    MessageBox.Show($"Общий план '{newZoneName}' успешно создан");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания общего плана: {ex.Message}");
                }
            }
        }

        private void RenameZoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentZoneId <= 0) return;
            if (isEditMode)
            {
                MessageBox.Show("Выйдите из режима редактирования перед переименованием общего плана");
                return;
            }

            string currentName = zonesDictionary.TryGetValue(currentZoneId, out string? value) ? value : $"Общий план {currentZoneId}";
            var dialog = new InputDialog("Введите новое название зоны:", "Переименование зоны", currentName);

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer))
            {
                string newZoneName = dialog.Answer.Trim();
                if (_dbService.ZoneNameExists(newZoneName))
                {
                    MessageBox.Show($"Общий план с названием '{newZoneName}' уже существует\nВведите другое название", "Ошибка создания общего плана", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    _dbService.RenameZone(currentZoneId, newZoneName);
                    LoadZonesToSelector();
                    ZoneSelector.SelectedValue = currentZoneId;
                    LoadAllZonesInEditor();
                    HighlightCurrentZoneInEditor();
                    DrawAllZonesOnHydrantMap();
                    UpdateMarkersZoneInfo();
                    MessageBox.Show($"Общий план переименован в '{newZoneName}'!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка переименования общего плана: {ex.Message}");
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (MainTabControl.SelectedIndex != 0) return;

            if (e.Key == Key.E && !isEditMode)
            {
                var result = MessageBox.Show("Режим редактирования общего плана активирован\nЛКМ — добавить точку\nУдержание ЛКМ на точке и передвижение — переместить маркер гидранта\n" + "Esc — сохранить и выйти", "Внимание", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.OK)
                {
                    AcceptChangeZoneBtn.Visibility = Visibility.Visible;
                    CancelChangeZoneBtn.Visibility = Visibility.Visible;
                    EnterEditMode();
                }
            }

            if (e.Key == Key.Escape && isEditMode)
            {
                CollapsingButtonsVisibility();
                ExitEditMode();
            }
        }

        private void CollapsingButtonsVisibility()
        {
            AcceptChangeZoneBtn.Visibility = Visibility.Collapsed;
            CancelChangeZoneBtn.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region ОТОБРАЖЕНИЕ ЗОН НА (ВКЛ2)
        private void DrawAllZonesOnHydrantMap()
        {
            var currentPosition = HydrantMap.Position;
            var currentZoom = HydrantMap.Zoom;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var markersToRemove = new List<GMapMarker>();
                foreach (var marker in HydrantMap.Markers)
                {
                    if (marker is GMapPolygon polygon && polygon.Tag is int)
                    {
                        markersToRemove.Add(marker);
                    }
                }

                foreach (var marker in markersToRemove)
                {
                    HydrantMap.Markers.Remove(marker);
                }

                var zonesList = _dbService.GetAllZones();
                foreach (var zone in zonesList)
                {
                    var points = _dbService.GetZonePoints(zone.Id);
                    if (points.Count < 3) continue;

                    var zonePolygon = CreateZonePolygon(points, zone.Id, Brushes.DarkBlue, strokeThickness: 2);
                    HydrantMap.Markers.Add(zonePolygon);
                }

                HydrantMap.Position = currentPosition;
                HydrantMap.Zoom = currentZoom;
            });

            UpdateMarkersScaling();
        }
        #endregion

        #region ОПРЕДЕЛЕНИЕ НАХОЖДЕНИЯ ГИДРАНТА В ЗОНЕ(ВКЛ2)
        private void UpdateMarkersZoneInfo()
        {
            try
            {
                var ewssList = _ewsService.GetAllEwssWithDisplay();
                AssignZoneIds(ewssList);
                LoadMarkersToDataGrid(ewssList);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении зон маркеров: {ex.Message}");
            }
        }

        private void AssignZoneIds(List<Ewss> ewssList)
        {
            var allZones = _dbService.GetAllZoneBounds();
            foreach (var ewss in ewssList)
            {
                int? zoneId = null;
                foreach (var zone in allZones)
                {
                    if (zone.HasValidBounds && IsPointInZone(zone.ZoneId, ewss.LatitudeD, ewss.LongitudeD))
                    {
                        zoneId = zone.ZoneId;
                        break;
                    }
                }
                ewss.ZoneId = zoneId;
            }
        }

        private List<Ewss> GetHydrantsInZone(int zoneId)
        {
            var all = _ewsService.GetAllEwssWithDisplay();
            var zonePoints = _dbService.GetZonePoints(zoneId);
            if (zonePoints.Count < 3) return new List<Ewss>();
            return all.Where(e => IsPointInPolygon(new PointLatLng(e.LatitudeD, e.LongitudeD), zonePoints)).ToList();
        }

        private bool IsPointInZone(int zoneId, double lat, double lng)
        {
            var points = _dbService.GetZonePoints(zoneId);
            return IsPointInPolygon(new PointLatLng(lat, lng), points);
        }

        private static bool IsPointInPolygon(PointLatLng point, List<PointLatLng> polygon)
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
        #endregion

        #region ДОБАВЛЕНИЕ И РЕДАКТИРОВАНИЕ ГИДРАНТОВ(ВКЛ2)

        private (int? zoneId, string zoneName) GetZoneInfoForPoint(double lat, double lng)
        {
            var zones = _dbService.GetAllZoneBounds();
            foreach (var zone in zones.Where(z => IsPointInZone(z.ZoneId, lat, lng)))
            {
                return (zone.ZoneId,
                        zonesDictionary.GetValueOrDefault(zone.ZoneId, $"Общий план {zone.ZoneId}"));
            }
            return (null, "Вне общего плана");
        }

        private void HydrantMap_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isAddMarkerDialogOpen)
            {
                e.Handled = true;
                return;
            }
            var point = HydrantMap.FromLocalToLatLng((int)e.GetPosition(HydrantMap).X, (int)e.GetPosition(HydrantMap).Y);
            var clickedMarker = HydrantMap.Markers.OfType<GMapMarker>().FirstOrDefault(m => m != temporaryMarker && !bindingMarkers.Contains(m) && Math.Abs(m.Position.Lat - point.Lat) < 0.00002 && Math.Abs(m.Position.Lng - point.Lng) < 0.00002);

            if (clickedMarker != null)
            {
                ShowMarkerContextMenu(clickedMarker);
                e.Handled = true;
                return;
            }
            if (isAddMarkerDialogOpen) return;
            isAddMarkerDialogOpen = true;

            try
            {
                var addWindow = new AddMarkerWindow { Owner = this, Latitude = point.Lat, Longitude = point.Lng };
                addWindow.Closed += (s, args) =>
                {
                    isAddMarkerDialogOpen = false;
                };

                if (addWindow.ShowDialog() == true)
                {
                    var ewss = addWindow.EditEwss;
                    ewss.EwsGeoCoordX = (decimal)point.Lat;
                    ewss.EwsGeoCoordY = (decimal)point.Lng;
                    string newId = _ewsService.GetNextEwsId();
                    ewss.EwsId = newId;
                    ewss.EwsMapId = newId;
                    ewss.EwsFireUnitCod = _currentUser?.FioUnitCod;
                    ewss.RecordUserCod = _currentUser?.FioUnitCod;
                    ewss.RecordCreated = DateTime.Now;
                    ewss.RecordStatus = "активна";
                    ewss.EwsValueCod = "1";
                    ewss.EwsPacount = "1";
                    _ewsService.InsertEwss(ewss);

                    // Перезагружаем гидрант из БД чтобы получить DisplayNumber, StatusName, PipeInfo
                    var reloaded = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.EwsId == ewss.EwsId);
                    if (reloaded == null) reloaded = ewss;

                    var (zoneId, zoneName) = GetZoneInfoForPoint(point.Lat, point.Lng);
                    reloaded.ZoneId = zoneId;
                    AddOrUpdateHydrantMarker(reloaded);
                    LoadMarkersToDataGrid();
                    MessageBox.Show($"Маркер гидранта добавлен\n{(zoneId.HasValue ? $"Общий план: {zoneName}" : "Находится вне общего плана")}");
                }
            }
            finally
            {
                isAddMarkerDialogOpen = false;
            }
            e.Handled = true;
        }
        private void AddOrUpdateHydrantMarker(Ewss ewss)
        {
            var id = ewss.MarkerId;
            var existing = HydrantMap.Markers.FirstOrDefault(m => m.Tag is int markerId && markerId == id);
            if (existing != null) HydrantMap.Markers.Remove(existing);
            var marker = HydrantMarker.CreateMarker(ewss, (int)HydrantMap.Zoom);

            if (marker.Shape is Canvas canvas)
            {
                canvas.MouseRightButtonDown += (s, e) => { e.Handled = true; ShowMarkerContextMenu(marker); };
            }
            HydrantMap.Markers.Add(marker);
            HydrantMap.InvalidateVisual();
        }

        private void ShowMarkerContextMenu(GMapMarker marker)
        {
            if (marker.Tag is not int markerId) return;
            var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == markerId);
            if (ewss == null) return;

            var menu = HydrantContextMenu.Create(
                onShowInfo: () => ShowMarkerInfo(marker),
                onEdit: () => EditMarkerInfo(marker),
                onMove: () => StartMovingMarker(marker),
                onDelete: () => DeleteHydrant(marker),
                onAddBinding: () => StartBindingProcess(marker),
                onEditBinding: () => EditBindingForMarker(marker),
                onMoveBinding: () => StartMovingBindingForMarker(marker),
                onDeleteBinding: () => DeleteBindingForMarker(marker),
                onStartRoute: () => StartRouteFromMarker(marker),
                hasBinding: !string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX));

            if (marker.Shape is FrameworkElement element) element.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void HydrantMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MainTabControl.SelectedIndex != 1) return;
            var latLng = HydrantMap.FromLocalToLatLng((int)e.GetPosition(HydrantMap).X, (int)e.GetPosition(HydrantMap).Y);

            if (isCreatingBinding) FinishBindingCreation(latLng);
            else if (isMovingMarker) CompleteMarkerMove(latLng);
            else if (isMovingBinding) CompleteBindingMove(latLng);
            else if (_routingService.IsSelectingRouteEnd) _routingService.FinishRouteAtPoint(latLng);
        }

        private void DeleteHydrant(GMapMarker hydrantMarker)
        {
            if (hydrantMarker?.Tag is not int hydrantId) { MessageBox.Show("У гидранта отсутствует ID."); return; }

            string displayInfo = hydrantMarker.Shape is Canvas canvas
                ? canvas.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Tag as string == "hydrant_text")?.Text ?? "неизвестный" : "неизвестный";

            if (MessageBox.Show($"Удалить гидрант {displayInfo}?\nПривязка будет удалена автоматически.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == hydrantId);
                if (ewss == null) return;

                HydrantMap.Markers.Remove(hydrantMarker);
                _ewsService.DeleteEwss(ewss.EwsId);

                var bindingToRemove = bindingMarkers.FirstOrDefault(b => GetEwssIdForBinding(b) == ewss.EwsId);
                if (bindingToRemove != null)
                {
                    HydrantMap.Markers.Remove(bindingToRemove);
                    bindingMarkers.Remove(bindingToRemove);
                }

                // Принудительно перерисовываем карту
                HydrantMap.InvalidateVisual();

                LoadMarkersToDataGrid();
                MessageBox.Show("Гидрант удален");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void ShowMarkerInfo(GMapMarker marker)
        {
            if (marker?.Tag is not int id) return;
            var ewss = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.MarkerId == id);
            if (ewss == null) return;

            string zoneName = ewss.ZoneId.HasValue ? zonesDictionary.GetValueOrDefault(ewss.ZoneId.Value, $"Зона {ewss.ZoneId}") : "Вне общего плана";
            string bindingInfo = !string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX)
                ? $"Да\n  Комментарий: {ewss.EwsPriviazka}\n  Координаты: {ewss.EwsPriviazkaGeoX}, {ewss.EwsPriviazkaGeoY}"
                : "Нет";
            string info1 = $"Номер: {ewss.DisplayNumber}\nДиаметр: {ewss.PipeInfo}\nАдрес: {ewss.AddressText}\n" +
                         $"Принадлежность: {ewss.OrganizationName}\nОбщий план: {zoneName}\n" +
                         $"Статус: {ewss.StatusName}\n" +
                         $"Привязка: {bindingInfo}";
            MessageBox.Show(info1, "Информация о гидранте");
        }

        private void EditMarkerInfo(GMapMarker marker)
        {
            if (marker?.Tag is not int id) return;
            var existing = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == id);
            if (existing == null) return;

            var editWindow = new AddMarkerWindow
            {
                Owner = this,
                EditEwss = existing,
                Latitude = marker.Position.Lat,
                Longitude = marker.Position.Lng
            };

            if (editWindow.ShowDialog() != true) return;

            var updated = editWindow.EditEwss;
            updated.EwsGeoCoordX = (decimal)marker.Position.Lat;
            updated.EwsGeoCoordY = (decimal)marker.Position.Lng;

            _ewsService.InsertEwssHistory(existing, editWindow.ChangeReason);
            _ewsService.InsertEwssCheck(updated);
            _ewsService.UpdateEwss(updated);

            // ПОЛНОСТЬЮ ПЕРЕЗАГРУЖАЕМ ГИДРАНТ ИЗ БД ЧЕРЕЗ GetAllEwssWithDisplay
            var refreshedEwss = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.MarkerId == id);
            if (refreshedEwss == null) return;

            // Удаляем старый маркер
            var oldMarker = HydrantMap.Markers.FirstOrDefault(m => m.Tag is int mid && mid == id);
            if (oldMarker != null)
                HydrantMap.Markers.Remove(oldMarker);

            // Удаляем старую привязку
            var existingBinding = bindingMarkers.FirstOrDefault(b => GetEwssIdForBinding(b) == refreshedEwss.EwsId);
            if (existingBinding != null)
            {
                HydrantMap.Markers.Remove(existingBinding);
                bindingMarkers.Remove(existingBinding);
            }

            // Создаем новый маркер с обновленными данными
            var newMarker = HydrantMarker.CreateMarker(refreshedEwss, (int)HydrantMap.Zoom);
            if (newMarker.Shape is Canvas canvas)
            {
                canvas.MouseRightButtonDown += (s, e) => { e.Handled = true; ShowMarkerContextMenu(newMarker); };
            }
            HydrantMap.Markers.Add(newMarker);

            // Добавляем привязку если есть
            if (!string.IsNullOrEmpty(refreshedEwss.EwsPriviazkaGeoX))
            {
                var bindingMarker = BindingMarker.CreateMarkerFromEwss(refreshedEwss,
                    new PointLatLng(refreshedEwss.LatitudeD, refreshedEwss.LongitudeD),
                    (int)HydrantMap.Zoom);
                HydrantMap.Markers.Add(bindingMarker);
                bindingMarkers.Add(bindingMarker);
            }

            // ПОЛНОСТЬЮ ПЕРЕЗАГРУЖАЕМ СПИСОК ГИДРАНТОВ В DataGrid
            LoadMarkersToDataGrid();

            // Принудительно перерисовываем карту
            HydrantMap.InvalidateVisual();

            MessageBox.Show("Информация обновлена");
        }
        #endregion

        #region ПОИСК АДРЕСА(ВКЛ2)
        private async void SearchAddressButton_Click(object sender, RoutedEventArgs e)
        {
            string address = AddressTextBox.Text.Trim();
            if (string.IsNullOrEmpty(address))
            {
                MessageBox.Show("Введите адрес");
                return;
            }
            var latLng = await _geocodingService.SearchAddressAsync(address);

            if (latLng.HasValue)
            {
                HydrantMap.Position = latLng.Value;
                HydrantMap.Zoom = 16;
                AddTemporaryMarker(latLng.Value.Lat, latLng.Value.Lng);
            }
        }

        private void AddTemporaryMarker(double lat, double lon)
        {
            if (temporaryMarker != null)
                HydrantMap.Markers.Remove(temporaryMarker);

            var ellipse = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Blue,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            temporaryMarker = new GMapMarker(new PointLatLng(lat, lon))
            {
                Shape = ellipse,
                Offset = new Point(-7, -7)
            };

            ellipse.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowMarkerInfo(temporaryMarker);
            };

            ellipse.MouseRightButtonDown += (s, e) =>
            {
                e.Handled = true;
                var menu = new ContextMenu();
                var routeFromHere = new MenuItem { Header = "Начать маршрут отсюда" };
                routeFromHere.Click += (sender, args) => StartRouteFromMarker(temporaryMarker);
                var removeItem = new MenuItem { Header = "Удалить временный маркер" };
                removeItem.Click += (sender, args) =>
                {
                    if (temporaryMarker != null)
                    {
                        HydrantMap.Markers.Remove(temporaryMarker);
                        temporaryMarker = null;
                    }
                };
                menu.Items.Add(routeFromHere);
                menu.Items.Add(removeItem);
                ellipse.ContextMenu = menu;
                menu.IsOpen = true;
            };

            HydrantMap.Markers.Add(temporaryMarker);
        }
        #endregion

        #region ПОСТРОЕНИЕ МАРШРУТОВ(ВКЛ2)
        private void StartRouteFromMarker(GMapMarker marker)
        {
            _routingService.StartRouteFromPoint(marker);
            MessageBox.Show("Начальная точка маршрута выбрана. Для выбора конца маршрута нажмите ЛКМ");
        }

        private void OnRouteBuilt(object sender, RoutingService.RouteBuiltEventArgs e)
        {
            currentRoute = _routingService.CurrentRoute;
            HydrantMap.Markers.Add(currentRoute);
            RouteLengthText.Text = $"Длина маршрута: {e.DistanceKm:F2} км";
            RouteLengthText.Visibility = Visibility.Visible;
            RemoveRouteBtn.Visibility = Visibility.Visible;
        }

        private void OnRouteRemoved(object sender, EventArgs e)
        {
            if (currentRoute != null)
                HydrantMap.Markers.Remove(currentRoute);
            currentRoute = null;
            RouteLengthText.Visibility = Visibility.Collapsed;
        }

        private void RemoveRoute_Click(object sender, RoutedEventArgs e)
        {
            _routingService.RemoveRoute();
            RemoveRouteBtn.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region ЗАГРУЗКА ДАННЫХ ДЛЯ КАРТЫ ГИДРАНТОВ (ВКЛ2)
        private void LoadHydrantsFromDatabase()
        {
            var ewssList = _ewsService.GetAllEwssWithDisplay();
            foreach (var ewss in ewssList)
            {
                AddOrUpdateHydrantMarker(ewss);
            }
        }

        private void LoadBindingsFromDatabase()
        {
            var ewssList = _ewsService.GetAllEwssWithDisplay();
            var hydrantsOnMap = HydrantMap.Markers.OfType<GMapMarker>().Where(m => m.Tag is int && !bindingMarkers.Contains(m)).ToDictionary(m => (int)m.Tag, m => m.Position);

            var newBindingMarkers = new List<GMapMarker>();
            foreach (var ewss in ewssList)
            {
                if (string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX)) continue;
                if (!hydrantsOnMap.TryGetValue(ewss.MarkerId, out var hydrantPos)) continue;

                var bindingMarker = BindingMarker.CreateMarkerFromEwss(ewss, hydrantPos, (int)HydrantMap.Zoom);
                newBindingMarkers.Add(bindingMarker);
            }

            foreach (var bindingMarker in bindingMarkers)
            {
                HydrantMap.Markers.Remove(bindingMarker);
            }
            bindingMarkers.Clear();

            foreach (var bindingMarker in newBindingMarkers)
            {
                HydrantMap.Markers.Add(bindingMarker);
                bindingMarkers.Add(bindingMarker);
            }
        }
        #endregion

        #region ПЕРЕМЕЩЕНИЕ МАРКЕРОВ ГИДРАНТОВ(ВКЛ2)
        private void StartMovingMarker(GMapMarker marker)
        {
            _routingService.RemoveRoute();
            isMovingMarker = true;
            movingMarker = marker;
            MessageBox.Show("Режим перемещения активен. Нажмите ЛКМ на карте для перемещения.");
        }

        private void CompleteMarkerMove(PointLatLng newPosition)
        {
            if (!isMovingMarker || movingMarker == null) return;
            try
            {
                int markerId = (int)movingMarker.Tag;
                var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == markerId);
                if (ewss == null) return;

                var (newZoneId, newZoneName) = GetZoneInfoForPoint(newPosition.Lat, newPosition.Lng);

                // Сохраняем старую привязку, если она есть
                string oldGeoX = ewss.EwsPriviazkaGeoX;
                string oldGeoY = ewss.EwsPriviazkaGeoY;

                ewss.ZoneId = newZoneId;
                ewss.EwsGeoCoordX = (decimal)newPosition.Lat;
                ewss.EwsGeoCoordY = (decimal)newPosition.Lng;

                // Координаты привязки остаются те же, ничего пересчитывать не нужно
                ewss.EwsPriviazkaGeoX = oldGeoX;
                ewss.EwsPriviazkaGeoY = oldGeoY;

                _ewsService.UpdateEwss(ewss);

                // ПОЛНОСТЬЮ ПЕРЕЗАГРУЖАЕМ ГИДРАНТ ИЗ БД
                var updatedEwss = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.MarkerId == markerId);
                if (updatedEwss == null) return;

                // Удаляем старую привязку
                var oldBindingMarkers = bindingMarkers
                    .Where(b => GetEwssIdForBinding(b) == updatedEwss.EwsId)
                    .ToList();
                foreach (var bm in oldBindingMarkers)
                {
                    HydrantMap.Markers.Remove(bm);
                    bindingMarkers.Remove(bm);
                }

                // Удаляем старый маркер
                var oldMarker = HydrantMap.Markers.FirstOrDefault(m => m.Tag is int id && id == markerId);
                if (oldMarker != null)
                    HydrantMap.Markers.Remove(oldMarker);

                // Создаем новый маркер с обновленными данными
                var newMarker = HydrantMarker.CreateMarker(updatedEwss, (int)HydrantMap.Zoom);
                if (newMarker.Shape is Canvas canvas)
                {
                    canvas.MouseRightButtonDown += (s, e) => { e.Handled = true; ShowMarkerContextMenu(newMarker); };
                }
                HydrantMap.Markers.Add(newMarker);

                // Добавляем обновленную привязку если есть
                if (!string.IsNullOrEmpty(updatedEwss.EwsPriviazkaGeoX))
                {
                    var bindingMarker = BindingMarker.CreateMarkerFromEwss(updatedEwss,
                        new PointLatLng(updatedEwss.LatitudeD, updatedEwss.LongitudeD),
                        (int)HydrantMap.Zoom);
                    HydrantMap.Markers.Add(bindingMarker);
                    bindingMarkers.Add(bindingMarker);
                }

                // ПОЛНОСТЬЮ ПЕРЕЗАГРУЖАЕМ СПИСОК ГИДРАНТОВ В DataGrid
                LoadMarkersToDataGrid();

                // Принудительно перерисовываем карту
                HydrantMap.InvalidateVisual();

                MessageBox.Show($"Маркер гидранта перемещён\n{(newZoneId.HasValue ? $"Зона: {newZoneName}" : "Находится вне зоны")}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перемещении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isMovingMarker = false;
                movingMarker = null;
            }
        }

        private void UpdateBindingsForMovedMarker(Ewss ewss, PointLatLng newPosition)
        {
            // Координаты привязки остаются неизменными, обновляется только позиция гидранта
        }
        #endregion

        #region РАБОТА С ПРИВЯЗКАМИ(ВКЛ2)
        private void StartBindingProcess(GMapMarker hydrantMarker)
        {
            int markerId = (int)hydrantMarker.Tag;
            var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == markerId);
            if (ewss == null) return;
            if (!string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX))
            {
                MessageBox.Show("У этого гидранта уже есть привязка");
                return;
            }
            MessageBox.Show("Нажмите ЛКМ на карте для установки привязки");
            isCreatingBinding = true;
            currentHydrantForBinding = hydrantMarker;
        }

        private void EditBindingForMarker(GMapMarker hydrantMarker)
        {
            if (hydrantMarker?.Tag is not int hydrantId) return;
            var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == hydrantId);
            if (ewss == null || string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX)) return;

            double lat = Utility.ParseBindingDistance(ewss.EwsPriviazkaGeoX);
            double lng = Utility.ParseBindingDistance(ewss.EwsPriviazkaGeoY);

            var dialog = new BindingEditDialog(lat, lng,
                ewss.EwsPriviazka, ewss.EwsPrLeft, ewss.EwsPrRight, ewss.EwsPrStright);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            _ewsService.UpdateEwssBinding(ewss.EwsId,
                dialog.BindingComment,
                dialog.BindingLat,
                dialog.BindingLng,
                dialog.BindingLeft,
                dialog.BindingRight,
                dialog.BindingStraight);

            // Перезагружаем гидрант из БД
            var reloaded = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.EwsId == ewss.EwsId);
            if (reloaded == null) reloaded = ewss;

            var existingMarker = bindingMarkers.FirstOrDefault(b => GetEwssIdForBinding(b) == reloaded.EwsId);
            if (existingMarker != null)
            {
                HydrantMap.Markers.Remove(existingMarker);
                bindingMarkers.Remove(existingMarker);
            }

            var hydrantPos = new PointLatLng(reloaded.LatitudeD, reloaded.LongitudeD);
            var newMarker = BindingMarker.CreateMarkerFromEwss(reloaded, hydrantPos, (int)HydrantMap.Zoom);
            HydrantMap.Markers.Add(newMarker);
            bindingMarkers.Add(newMarker);

            HydrantMap.InvalidateVisual();
            LoadMarkersToDataGrid();

            MessageBox.Show("Привязка обновлена");
        }

        private void FinishBindingCreation(PointLatLng bindingPoint)
        {
            try
            {
                if (!isCreatingBinding || currentHydrantForBinding == null) return;
                int markerId = (int)currentHydrantForBinding.Tag;
                var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == markerId);
                if (ewss == null) return;

                var dialog = new BindingEditDialog(bindingPoint.Lat, bindingPoint.Lng);
                dialog.Owner = this;
                if (dialog.ShowDialog() != true) return;

                _ewsService.UpdateEwssBinding(ewss.EwsId,
                    dialog.BindingComment,
                    dialog.BindingLat,
                    dialog.BindingLng,
                    dialog.BindingLeft,
                    dialog.BindingRight,
                    dialog.BindingStraight);

                // Перезагружаем гидрант из БД чтобы получить DisplayNumber, StatusName, PipeInfo
                var reloaded = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.EwsId == ewss.EwsId);
                if (reloaded == null) reloaded = ewss;

                var bindingMarker = BindingMarker.CreateMarkerFromEwss(reloaded, currentHydrantForBinding.Position, (int)HydrantMap.Zoom);
                HydrantMap.Markers.Add(bindingMarker);
                bindingMarkers.Add(bindingMarker);

                HydrantMap.InvalidateVisual();
                LoadMarkersToDataGrid();

                MessageBox.Show("Привязка установлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при установке привязки: {ex.Message}");
            }
            finally
            {
                isCreatingBinding = false;
                currentHydrantForBinding = null;
            }
        }

        private void StartMovingBindingForMarker(GMapMarker hydrantMarker)
        {
            if (hydrantMarker?.Tag is not int hydrantId)
            {
                MessageBox.Show("У маркера отсутствует ID.");
                return;
            }
            var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == hydrantId);
            if (ewss == null || string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX)) return;
            var bindingMarker = bindingMarkers.FirstOrDefault(b => b.Tag != null && b.Tag.ToString() == $"binding_{ewss.EwsId}");
            StartMovingBinding(bindingMarker);
        }

        private string GetEwssIdForBinding(GMapMarker bindingMarker)
        {
            if (bindingMarker?.Tag == null || !bindingMarker.Tag.ToString().StartsWith("binding_")) return "";
            return bindingMarker.Tag.ToString().Replace("binding_", "");
        }

        private void DeleteBindingForMarker(GMapMarker hydrantMarker)
        {
            if (hydrantMarker?.Tag is not int hydrantId)
            {
                MessageBox.Show("У маркера отсутствует ID.");
                return;
            }
            var ewss = _ewsService.GetAllEwss().FirstOrDefault(e => e.MarkerId == hydrantId);
            if (ewss == null || string.IsNullOrEmpty(ewss.EwsPriviazkaGeoX)) return;
            var bindingMarker = bindingMarkers.FirstOrDefault(b => b.Tag != null && b.Tag.ToString() == $"binding_{ewss.EwsId}");
            DeleteBinding(bindingMarker);
        }

        private void StartMovingBinding(GMapMarker bindingMarker)
        {
            if (bindingMarker?.Tag == null || !bindingMarker.Tag.ToString().StartsWith("binding_")) return;
            _routingService.RemoveRoute();
            isMovingBinding = true;
            movingBindingMarker = bindingMarker;
            MessageBox.Show("Режим перемещения привязки активен. Нажмите ЛКМ на карте для перемещения");
        }

        private void CompleteBindingMove(PointLatLng newPosition)
        {
            if (!isMovingBinding || movingBindingMarker == null) return;
            try
            {
                string tagStr = movingBindingMarker.Tag.ToString();
                if (!tagStr.StartsWith("binding_")) return;
                string ewsId = tagStr.Replace("binding_", "");
                var ewss = _ewsService.GetAllEwssWithDisplay().FirstOrDefault(e => e.EwsId == ewsId);
                if (ewss == null)
                {
                    MessageBox.Show("Привязка не найдена в базе данных.");
                    return;
                }

                var dialog = new BindingEditDialog(newPosition.Lat, newPosition.Lng,
                    ewss.EwsPriviazka, ewss.EwsPrLeft, ewss.EwsPrRight, ewss.EwsPrStright);
                dialog.Owner = this;
                if (dialog.ShowDialog() != true) return;

                _ewsService.UpdateEwssBinding(ewss.EwsId,
                    dialog.BindingComment,
                    dialog.BindingLat,
                    dialog.BindingLng,
                    dialog.BindingLeft,
                    dialog.BindingRight,
                    dialog.BindingStraight);
                ewss.EwsPriviazka = dialog.BindingComment;
                ewss.EwsPriviazkaGeoX = dialog.BindingLat;
                ewss.EwsPriviazkaGeoY = dialog.BindingLng;
                ewss.EwsPrLeft = dialog.BindingLeft;
                ewss.EwsPrRight = dialog.BindingRight;
                ewss.EwsPrStright = dialog.BindingStraight;

                // Удаляем старую привязку
                HydrantMap.Markers.Remove(movingBindingMarker);
                bindingMarkers.Remove(movingBindingMarker);

                // Создаем новую привязку
                var bindingMarker = BindingMarker.CreateMarkerFromEwss(ewss,
                    new PointLatLng(ewss.LatitudeD, ewss.LongitudeD),
                    (int)HydrantMap.Zoom);
                HydrantMap.Markers.Add(bindingMarker);
                bindingMarkers.Add(bindingMarker);

                // Принудительно перерисовываем карту
                HydrantMap.InvalidateVisual();

                // Обновляем список гидрантов в DataGrid
                LoadMarkersToDataGrid();

                MessageBox.Show("Привязка успешно перемещена!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перемещении привязки: {ex.Message}");
            }
            finally
            {
                isMovingBinding = false;
                movingBindingMarker = null;
            }
        }

        private void DeleteBinding(GMapMarker bindingMarker)
        {
            if (bindingMarker?.Tag == null || !bindingMarker.Tag.ToString().StartsWith("binding_")) return;
            string tagStr = bindingMarker.Tag.ToString();
            string ewsId = tagStr.Replace("binding_", "");
            var result = MessageBox.Show("Вы уверены, что хотите удалить эту привязку?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                _ewsService.ClearEwssBinding(ewsId);
                HydrantMap.Markers.Remove(bindingMarker);
                bindingMarkers.Remove(bindingMarker);

                // Принудительно перерисовываем карту
                HydrantMap.InvalidateVisual();

                // Обновляем список гидрантов в DataGrid
                LoadMarkersToDataGrid();

                MessageBox.Show("Привязка успешно удалена.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении привязки: {ex.Message}");
            }
        }
        #endregion

        #region СОХРАНЕНИЕ КАРТ пнг
        private async void SaveAllZonesWithHydrants_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folderName = $"Зоны_экспорт_{DateTime.Now:dd_MM_yyyy}_Время_{DateTime.Now:HH_mm}";
                var folderDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Выберите место для сохранения папки с изображениями",
                    FileName = folderName,
                    Filter = "Папка|*.folder"
                };
                bool? folderResult = folderDialog.ShowDialog();
                if (folderResult != true)
                {
                    MessageBox.Show("Сохранение отменено.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string baseFolder = System.IO.Path.GetDirectoryName(folderDialog.FileName);
                string exportFolder = System.IO.Path.Combine(baseFolder, folderName);
                Directory.CreateDirectory(exportFolder);
                LoadZonesToSelector();
                var zonesList = new List<ZoneInfo>();
                foreach (var zone in zonesDictionary)
                {
                    int zoneId = zone.Key;
                    var bounds = _dbService.GetZoneBounds(zoneId);
                    if (bounds != null && bounds.HasValidBounds)
                    {
                        int hydrantCount = GetHydrantsInZone(zoneId).Count;
                        zonesList.Add(new ZoneInfo
                        {
                            ZoneId = zoneId,
                            ZoneName = zone.Value,
                            MinLat = bounds.MinLat,
                            MaxLat = bounds.MaxLat,
                            MinLng = bounds.MinLng,
                            MaxLng = bounds.MaxLng,
                            HydrantCount = hydrantCount,
                            HasValidBounds = true
                        });
                    }
                }
                if (zonesList.Count == 0)
                {
                    MessageBox.Show("Не найдено ни одной зоны.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var progressWindow = new ExportProgressWindow
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                progressWindow.SetMaxProgress(zonesList.Count * 2);
                progressWindow.Show();
                int completedOperations = 0;
                int errorCount = 0;
                foreach (var zone in zonesList)
                {
                    try
                    {
                        string zoneFolderName = $"{zone.ZoneId:00}_{SanitizeFileName(zone.ZoneName)}";
                        string zoneFolder = System.IO.Path.Combine(exportFolder, zoneFolderName);
                        Directory.CreateDirectory(zoneFolder);
                        progressWindow.SetProgressText($"Общий план: {zone.ZoneName}");
                        progressWindow.SetStatusText($"Сохранение общего плана зоны {zone.ZoneName}");
                        progressWindow.UpdateLayout();
                        string overviewFileName = System.IO.Path.Combine(zoneFolder, $"00_{SanitizeFileName(zone.ZoneName)}_общий_план.png");
                        await SaveZoneOverviewAsync(zone, overviewFileName, progressWindow);
                        completedOperations++;
                        progressWindow.SetProgressValue(completedOperations);
                        await Task.Delay(100);
                        if (zone.HydrantCount > 0)
                        {
                            progressWindow.SetProgressText($"Сетка: {zone.ZoneName}");
                            progressWindow.SetStatusText($"Сохранение сетки зоны {zone.ZoneName}...");
                            progressWindow.UpdateLayout();
                            string gridBaseFileName = System.IO.Path.Combine(zoneFolder, $"01_{SanitizeFileName(zone.ZoneName)}_сетка_4x3.png");
                            await SaveZoneGridAsync(zone, gridBaseFileName, progressWindow);
                            completedOperations++;
                            progressWindow.SetProgressValue(completedOperations);
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        progressWindow.SetStatusText($"Ошибка в зоне {zone.ZoneName}: {ex.Message}");
                        progressWindow.UpdateLayout();
                        await Task.Delay(500);
                    }
                }
                progressWindow.Close();
                MessageBox.Show($"Сохранение завершено\n\nОшибок: {errorCount}\nПапка с изображениями:\n{exportFolder}",
                "Сохранение завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                var result = MessageBox.Show("Открыть папку с сохранёнными изображениями?", "Открыть папку", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", exportFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveZoneOverviewAsync(ZoneInfo zone, string fileName, Window progressWindow)
        {
            bool needsRetry = true;
            while (needsRetry)
            {
                needsRetry = false;
                OverviewCaptureWindow captureWindow = null;
                try
                {
                    captureWindow = new OverviewCaptureWindow
                    {
                        Title = $"Общий план: {zone.ZoneName}",
                        Width = 1024,
                        Height = 768,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        WindowStyle = WindowStyle.SingleBorderWindow,
                        ShowInTaskbar = false,
                        Topmost = true,
                        ResizeMode = ResizeMode.NoResize,
                        Owner = progressWindow
                    };
                    var zoneMap = captureWindow.ZoneMapControl;
                    if (!MBTilesProvider.Instance.IsLoaded && File.Exists(_currentMBTilesPath)) MBTilesProvider.Instance.LoadMBTilesFile(_currentMBTilesPath);

                    zoneMap.MapProvider = MBTilesProvider.Instance;
                    zoneMap.Zoom = 12;
                    zoneMap.DragButton = MouseButton.Left;
                    zoneMap.CanDragMap = false;
                    zoneMap.MouseWheelZoomEnabled = false;

                    double latRange = zone.MaxLat - zone.MinLat;
                    double lngRange = zone.MaxLng - zone.MinLng;
                    double latPadding = latRange * 0.03;
                    double lngPadding = lngRange * 0.03;
                    double displayMinLat = zone.MinLat - latPadding;
                    double displayMaxLat = zone.MaxLat + latPadding;
                    double displayMinLng = zone.MinLng - lngPadding;
                    double displayMaxLng = zone.MaxLng + lngPadding;
                    double displayCenterLat = (displayMinLat + displayMaxLat) / 2;
                    double displayCenterLng = (displayMinLng + displayMaxLng) / 2;
                    zoneMap.Position = new PointLatLng(displayCenterLat, displayCenterLng);
                    double requiredZoom = CalculateOptimalZoom(displayMinLat, displayMaxLat, displayMinLng, displayMaxLng);
                    zoneMap.Zoom = requiredZoom;
                    captureWindow.SetTitle(zone.ZoneName, zone.HydrantCount);

                    var tcs = new TaskCompletionSource<bool>();
                    captureWindow.Loaded += (s, e) => tcs.TrySetResult(true);
                    captureWindow.Show();
                    await tcs.Task;
                    await Task.Delay(1500);

                    zoneMap.Markers.Clear();
                    var zonePoints = _dbService.GetZonePoints(zone.ZoneId);
                    if (zonePoints.Count >= 3)
                    {
                        var zonePolygon = new GMapPolygon(zonePoints)
                        {
                            Shape = new System.Windows.Shapes.Path
                            {
                                Stroke = Brushes.DarkBlue,
                                StrokeThickness = 3,
                                Fill = Brushes.Transparent,
                                StrokeDashArray = null
                            }
                        };
                        zoneMap.Markers.Add(zonePolygon);
                    }
                    var freshHydrants = GetHydrantsInZone(zone.ZoneId);
                    foreach (var hydrant in freshHydrants)
                    {
                        var marker = HydrantMarker.CreateSimpleMarker(hydrant);
                        zoneMap.Markers.Add(marker);
                    }
                    zoneMap.InvalidateVisual();
                    captureWindow.UpdateLayout();
                    await Task.Delay(500);

                    var dialog = new GridSaveDialog(zone, zone.HydrantCount, fileName)
                    {
                        Owner = captureWindow
                    };
                    var dialogResult = dialog.ShowDialog();
                    if (dialogResult == false || dialog.IsCancelled)
                    {
                        captureWindow.Close();
                        return;
                    }
                    if (dialog.NeedsRetry)
                    {
                        needsRetry = true;
                        captureWindow.Close();
                        continue;
                    }
                    string finalFileName = dialog.FileName;
                    await Task.Delay(300);
                    RenderTargetBitmap renderBitmap = new((int)captureWindow.ActualWidth, (int)captureWindow.ActualHeight, 96d, 96d, PixelFormats.Pbgra32);
                    renderBitmap.Render(captureWindow);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    using (FileStream fileStream = new(finalFileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }
                    captureWindow.Close();

                    // Очистка памяти между зонами
                    MBTilesProvider.Instance.ClearCache();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения общего плана зоны {zone.ZoneName}: {ex.Message}");
                    captureWindow?.Close();
                }
                await Task.Delay(100);
            }
        }

        private async Task SaveZoneGridAsync(ZoneInfo zone, string baseFileName, Window progressWindow)
        {
            var tcs = new TaskCompletionSource<bool>();
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var freshHydrants = GetHydrantsInZone(zone.ZoneId);
                    if (freshHydrants.Count == 0)
                    {
                        tcs.SetResult(false);
                        return;
                    }

                    const int columns = 4, rows = 3, cellsPerPage = columns * rows;
                    int cellWidth = 280, cellHeight = 260, headerHeight = 60;
                    int totalWidth = columns * cellWidth + 40;
                    int totalHeight = headerHeight + rows * cellHeight + 30;

                    var allPages = new List<List<GridCellData>>();
                    var currentPage = new List<GridCellData>();

                    var orderedHydrants = freshHydrants.OrderBy(h => h.DisplayNumber).ToList();
                    foreach (var hydrant in orderedHydrants)
                    {
                        currentPage.Add(new GridCellData
                        {
                            HydrantId = hydrant.MarkerId,
                            HydrantNumber = hydrant.DisplayNumber,
                            HydrantTruba = hydrant.PipeInfo,
                            Latitude = hydrant.LatitudeD,
                            Longitude = hydrant.LongitudeD,
                            Status = hydrant.StatusName,
                            EwsId = hydrant.EwsId,
                            EwsPriviazka = hydrant.EwsPriviazka ?? "",
                            EwsPriviazkaGeoX = hydrant.EwsPriviazkaGeoX ?? "",
                            EwsPriviazkaGeoY = hydrant.EwsPriviazkaGeoY ?? ""
                        });
                        if (currentPage.Count >= cellsPerPage)
                        {
                            allPages.Add([.. currentPage]);
                            currentPage.Clear();
                        }
                    }
                    if (currentPage.Count != 0)
                        allPages.Add(currentPage);

                    //PrewarmTilesParallel(allPages.SelectMany(p => p).ToList());
                    PrewarmTilesDirect(allPages.SelectMany(p => p).ToList());

                    int totalPages = allPages.Count;
                    int retryDelay = 500;

                    for (int page = 0; page < totalPages; page++)
                    {
                        var pageCells = allPages[page];
                        var captureWindow = new Window
                        {
                            Title = $"Сетка зоны {zone.ZoneName} - Страница {page + 1} из {totalPages}",
                            Width = totalWidth + 50,
                            Height = totalHeight + 50,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            WindowStyle = WindowStyle.SingleBorderWindow,
                            ShowInTaskbar = false,
                            Topmost = true,
                            ResizeMode = ResizeMode.NoResize,
                            Owner = progressWindow,
                            Background = Brushes.White
                        };
                        if (!MBTilesProvider.Instance.IsLoaded)
                            MBTilesProvider.Instance.LoadMBTilesFile("NewLoadMap.mbtiles");

                        var gridPageControl = new GridPageControl(isExportMode: true);
                        for (int i = 0; i < pageCells.Count; i++)
                            gridPageControl.AddCell(pageCells[i], i);

                        captureWindow.Content = gridPageControl;
                        var loadedTcs = new TaskCompletionSource<bool>();
                        captureWindow.Loaded += (s, e) => loadedTcs.TrySetResult(true);
                        captureWindow.Show();
                        await loadedTcs.Task;
                        await Task.Delay(retryDelay);

                        var dialog = new GridSaveDialog(zone.ZoneName, page + 1, totalPages, pageCells, baseFileName)
                        {
                            Owner = captureWindow
                        };
                        var dialogResult = dialog.ShowDialog();

                        if (dialogResult == false || dialog.IsCancelled)
                        {
                            captureWindow.Close();
                            tcs.SetResult(false);
                            return;
                        }

                        if (dialog.NeedsRetry)
                        {
                            retryDelay = Math.Min(retryDelay + 500, 3000);
                            MBTilesProvider.Instance.ClearCache();
                            page--;
                            captureWindow.Close();
                            continue;
                        }

                        retryDelay = 500;

                        InvalidateAllMiniMaps(gridPageControl);

                        string fileName = dialog.FileName;

                        captureWindow.UpdateLayout();
                        await Task.Delay(300);
                        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                        await Task.Delay(200);

                        captureWindow.UpdateLayout();

                        RenderTargetBitmap renderBitmap = new(
                            (int)captureWindow.ActualWidth,
                            (int)captureWindow.ActualHeight,
                            96d, 96d, PixelFormats.Pbgra32);
                        renderBitmap.Render(captureWindow);

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                        using (FileStream fileStream = new(fileName, FileMode.Create))
                            encoder.Save(fileStream);

                        captureWindow.Close();

                        // Очистка после каждой страницы
                        MBTilesProvider.Instance.ClearCache();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        await Task.Delay(200);
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении сетки зоны: {ex.Message}");
                    tcs.SetResult(false);
                }
            });
            await tcs.Task;
        }

        //private void PrewarmTilesParallel(List<GridCellData> hydrants)
        //{
        //    if (hydrants == null || hydrants.Count == 0) return;

        //    if (!MBTilesProvider.Instance.IsLoaded && File.Exists(_currentMBTilesPath))
        //        MBTilesProvider.Instance.LoadMBTilesFile(_currentMBTilesPath);

        //    int zoom = 16;
        //    var needed = new HashSet<(int x, int y)>();

        //    foreach (var hydrant in hydrants)
        //    {
        //        int tileX = (int)Math.Floor((hydrant.Longitude + 180.0) / 360.0 * (1 << zoom));
        //        int tileY = (int)Math.Floor((1.0 - Math.Log(Math.Tan(hydrant.Latitude * Math.PI / 180.0)
        //            + 1.0 / Math.Cos(hydrant.Latitude * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

        //        int max = (1 << zoom) - 1;
        //        tileX = Math.Clamp(tileX, 0, max);
        //        tileY = Math.Clamp(tileY, 0, max);

        //        for (int dx = -1; dx <= 1; dx++)
        //            for (int dy = -1; dy <= 1; dy++)
        //            {
        //                int tx = Math.Clamp(tileX + dx, 0, max);
        //                int ty = Math.Clamp(tileY + dy, 0, max);
        //                needed.Add((tx, ty));
        //            }
        //    }

        //    var tileList = needed.ToList();
        //    Parallel.For(0, tileList.Count, i =>
        //    {
        //        MBTilesProvider.Instance.GetTileImage(
        //            new GPoint(tileList[i].x, tileList[i].y), zoom);
        //    });
        //} 

        private void PrewarmTilesDirect(List<GridCellData> hydrants)
        {
            if (hydrants == null || hydrants.Count == 0) return;

            if (!MBTilesProvider.Instance.IsLoaded && File.Exists(_currentMBTilesPath))
                MBTilesProvider.Instance.LoadMBTilesFile(_currentMBTilesPath);

            int[] zooms = { 15, 16, 17 };
            var processed = new HashSet<string>();

            foreach (var hydrant in hydrants)
            {
                foreach (int zoom in zooms)
                {
                    long tileX = (long)Math.Floor((hydrant.Longitude + 180.0) / 360.0 * (1 << zoom));
                    long tileY = (long)Math.Floor((1.0 - Math.Log(Math.Tan(hydrant.Latitude * Math.PI / 180.0) + 1.0 / Math.Cos(hydrant.Latitude * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

                    long maxTile = (1L << zoom) - 1;
                    tileX = Math.Clamp(tileX, 0, maxTile);
                    tileY = Math.Clamp(tileY, 0, maxTile);

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            long tx = Math.Clamp(tileX + dx, 0, maxTile);
                            long ty = Math.Clamp(tileY + dy, 0, maxTile);

                            var tile = new GPoint(tx, ty);
                            string key = $"{zoom}_{tile.X}_{tile.Y}";
                            if (processed.Add(key))
                                MBTilesProvider.Instance.GetTileImage(tile, zoom);
                        }
                    }
                }
            }
        }
        private async Task PrewarmTileCacheAsync(List<GridCellData> hydrants)
        {
            if (hydrants == null || hydrants.Count == 0) return;

            if (!MBTilesProvider.Instance.IsLoaded && File.Exists(_currentMBTilesPath))
                MBTilesProvider.Instance.LoadMBTilesFile(_currentMBTilesPath);

            var warmWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Visibility = Visibility.Hidden
            };

            var warmMap = new GMapControl
            {
                Width = 280,
                Height = 260,
                MapProvider = MBTilesProvider.Instance,
                MinZoom = 16,
                MaxZoom = 16,
                Zoom = 16,
                Bearing = 0,
                CanDragMap = false,
                MouseWheelZoomEnabled = false,
                ShowTileGridLines = false,
                LevelsKeepInMemory = 10,
                Manager = { Mode = AccessMode.ServerAndCache }
            };

            warmWindow.Content = warmMap;
            warmWindow.Show();

            // Ждём инициализации
            await Task.Delay(200);
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

            // Перебираем все гидранты и переключаем позицию — GMap сам подгрузит тайлы
            var uniquePositions = hydrants
                .Select(h => new PointLatLng(h.Latitude, h.Longitude))
                .Distinct()
                .ToList();

            for (int i = 0; i < uniquePositions.Count; i++)
            {
                warmMap.Position = uniquePositions[i];
                warmMap.InvalidateVisual();

                // Каждые 4 позиции даём время на загрузку
                if (i % 4 == 0 || i == uniquePositions.Count - 1)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Task.Delay(300);
                }
            }

            warmMap.InvalidateVisual();
            await Task.Delay(500);
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

            warmWindow.Close();
        }

        private static void InvalidateAllMiniMaps(DependencyObject parent)
        {
            if (parent == null) return;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is GMapControl map)
                {
                    map.InvalidateVisual();
                }
                InvalidateAllMiniMaps(child);
            }
        }

        private static double CalculateOptimalZoom(double minLat, double maxLat, double minLng, double maxLng)
        {
            double latDiff = Math.Abs(maxLat - minLat);
            double lngDiff = Math.Abs(maxLng - minLng);
            double maxDiff = Math.Max(latDiff, lngDiff);
            if (maxDiff <= 0.002) return 18;
            if (maxDiff <= 0.004) return 17;
            if (maxDiff <= 0.008) return 16;
            if (maxDiff <= 0.016) return 15;
            if (maxDiff <= 0.032) return 14;
            if (maxDiff <= 0.064) return 13;
            if (maxDiff <= 0.128) return 12;
            return 11;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "Без_названия";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName.Replace(' ', '_');
        }
        #endregion

        #region РЕЗЕРВНОЕ КОПИРОВАНИЕ
        private async void FullBackup_Click(object sender, RoutedEventArgs e)
        {
            await _backupService.CreateFullBackupAsync(this);
        }

        private async void FullRestore_Click(object sender, RoutedEventArgs e)
        {
            if (await _backupService.RestoreFromBackupAsync(this))
            {
                LoadZonesToSelector();
                LoadAllZonesInEditor();
                DrawAllZonesOnHydrantMap();
                LoadHydrantsFromDatabase();
                LoadBindingsFromDatabase();
                UpdateMarkersZoneInfo();
                LoadMarkersToDataGrid();
                if (zonesDictionary.Count > 0)
                {
                    currentZoneId = zonesDictionary.First().Key;
                    ZoneSelector.SelectedValue = currentZoneId;
                    HighlightCurrentZoneInEditor();
                }
            }
        }
        #endregion

        #region СПИСОК ГИДРАНТОВ
        private void RefreshMarkers_Click(object sender, RoutedEventArgs e)
        {
            LoadMarkersToDataGrid();
        }

        // Заменить существующий метод LoadMarkersToDataGrid на этот:
        private void LoadMarkersToDataGrid(List<Ewss>? ewssList = null)
        {
            if (ewssList == null)
            {
                ewssList = _ewsService.GetAllEwssWithDisplay();
                AssignZoneIds(ewssList);
            }

            // Добавляем ZoneName для отображения
            foreach (var ewss in ewssList)
            {
                if (ewss.ZoneId.HasValue)
                    ewss.ZoneName = zonesDictionary.TryGetValue(ewss.ZoneId.Value, out string? value) ? value : $"Зона {ewss.ZoneId}";
                else
                    ewss.ZoneName = "Не в зоне";
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Включаем AutoGenerateColumns = true для отображения ВСЕХ полей
                MarkersDataGrid.AutoGenerateColumns = true;
                MarkersDataGrid.ItemsSource = ewssList;
                UpdateRecordsCount();
            });
        }

        // Добавить новый метод для получения полной информации о гидранте (опционально, для отладки)
        private void ShowFullHydrantInfo(Ewss ewss)
        {
            var properties = typeof(Ewss).GetProperties();
            var info = new StringBuilder();
            info.AppendLine($"=== Полная информация о гидранте {ewss.EwsNumber} ===\n");

            foreach (var prop in properties)
            {
                var value = prop.GetValue(ewss) ?? "null";
                info.AppendLine($"{prop.Name}: {value}");
            }

            MessageBox.Show(info.ToString(), "Полные данные гидранта", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"hydrants_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportDataGridToCSV(MarkersDataGrid, saveFileDialog.FileName);
                    MessageBox.Show($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExportDataGridToCSV(DataGrid grid, string filename)
        {
            if (grid.ItemsSource == null) return;
            var items = grid.ItemsSource.Cast<object>().ToList();
            if (items.Count == 0) return;
            using var writer = new StreamWriter(filename, false, Encoding.UTF8);
            var headers = new List<string>();
            foreach (var column in grid.Columns)
            {
                if (column is DataGridTextColumn textColumn && textColumn.Header != null)
                {
                    var header = textColumn.Header.ToString().Replace(";", ",");
                    headers.Add(header);
                }
            }
            writer.WriteLine(string.Join(";", headers));
            foreach (var item in items)
            {
                var values = new List<string>();
                foreach (var column in grid.Columns)
                {
                    if (column is DataGridTextColumn textColumn && textColumn.Binding is System.Windows.Data.Binding binding)
                    {
                        var propertyName = binding.Path.Path;
                        var property = item.GetType().GetProperty(propertyName);
                        var value = property?.GetValue(item)?.ToString() ?? "";
                        values.Add(value.Replace(";", ","));
                    }
                }
                writer.WriteLine(string.Join(";", values));
            }
        }

        private void SearchInGrid_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        #region СИНХРОНИЗАЦИЯ (История изменений и проверок)
        private void LoadHistoryData(bool reset = false)
        {
            if (_isHistoryLoading) return;

            try
            {
                _isHistoryLoading = true;

                if (reset)
                {
                    _historyOffset = 0;
                    _historyHasMore = true;
                }

                string search = HistorySearchTextBox.Text?.Trim();
                string dateFrom = HistoryDateFromPicker.SelectedDate?.ToString("yyyy-MM-dd");
                string dateTo = HistoryDateToPicker.SelectedDate?.ToString("yyyy-MM-dd");

                var items = _ewsService.GetCopyEwssPaged(_historyOffset, SyncPageSize, search, dateFrom, dateTo);

                if (items.Count < SyncPageSize)
                    _historyHasMore = false;

                if (reset)
                    HistoryDataGrid.ItemsSource = items;
                else
                {
                    var existing = HistoryDataGrid.ItemsSource as System.Collections.Generic.List<КопияEwss>;
                    if (existing != null)
                    {
                        existing.AddRange(items);
                        HistoryDataGrid.Items.Refresh();
                    }
                }

                _historyOffset += items.Count;
            }
            finally
            {
                _isHistoryLoading = false;
            }
        }

        private void LoadCheckData(bool reset = false)
        {
            if (_isCheckLoading) return;

            try
            {
                _isCheckLoading = true;

                if (reset)
                {
                    _checkOffset = 0;
                    _checkHasMore = true;
                }

                string search = CheckSearchTextBox.Text?.Trim();
                string dateFrom = CheckDateFromPicker.SelectedDate?.ToString("yyyy-MM-dd");
                string dateTo = CheckDateToPicker.SelectedDate?.ToString("yyyy-MM-dd");

                var items = _ewsService.GetEwssChecksPaged(_checkOffset, SyncPageSize, search, dateFrom, dateTo);

                if (items.Count < SyncPageSize)
                    _checkHasMore = false;

                if (reset)
                    CheckDataGrid.ItemsSource = items;
                else
                {
                    var existing = CheckDataGrid.ItemsSource as System.Collections.Generic.List<EwssCheck>;
                    if (existing != null)
                    {
                        existing.AddRange(items);
                        CheckDataGrid.Items.Refresh();
                    }
                }

                _checkOffset += items.Count;
            }
            finally
            {
                _isCheckLoading = false;
            }
        }

        private void HistoryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_historyHasMore || _isHistoryLoading) return;
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 50)
                LoadHistoryData();
        }

        private void CheckScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_checkHasMore || _isCheckLoading) return;
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 50)
                LoadCheckData();
        }

        private async void SyncCreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await _backupService.CreateFullBackupAsync(this);
        }

        private async void SyncRestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await _backupService.RestoreFromBackupAsync(this);
        }
        #endregion

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            if (MarkersDataGrid.ItemsSource == null) return;
            string searchText = SearchTextBox.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText))
            {
                MarkersDataGrid.Items.Filter = null;
            }
            else
            {
                MarkersDataGrid.Items.Filter = item =>
                {
                    if (item == null) return false;
                    var properties = item.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        if (prop.Name == "Id" || prop.Name == "ZoneId")
                        {
                            var value = prop.GetValue(item)?.ToString();
                            if (value?.Equals(searchText, StringComparison.OrdinalIgnoreCase) == true)
                                return true;
                        }
                        else
                        {
                            var value = prop.GetValue(item)?.ToString()?.ToLower();
                            if (!string.IsNullOrEmpty(value) && value.Contains(searchText))
                                return true;
                        }
                    }
                    return false;
                };
            }
            UpdateRecordsCount();
        }

        private void UpdateRecordsCount()
        {
            if (MarkersDataGrid.ItemsSource == null) return;
            if (MarkersDataGrid.ItemsSource is System.Collections.IEnumerable enumerable)
            {
                var list = enumerable.Cast<object>().ToList();
                int totalCount = list.Count;
                int filteredCount = MarkersDataGrid.Items.Filter != null ? list.Count(item => MarkersDataGrid.Items.Filter(item)) : totalCount;
                TotalRecordsText.Text = totalCount.ToString();
                FilteredRecordsText.Text = filteredCount.ToString();
            }
        }
        #endregion

        #region ЭКСПОРТ В WORD (OpenXML) С ИЗОБРАЖЕНИЯМИ
        private async void SaveAllZonesWithHydrantsToWord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Запрашиваем данные титульного листа (один раз для всех)
                var titleDialog = new TitlePageDialog { Owner = this };
                if (titleDialog.ShowDialog() != true)
                {
                    MessageBox.Show("Создание документа отменено.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. Выбор папки с PNG
                var pngFolderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Выберите папку с PNG изображениями",
                    Multiselect = false
                };
                string pngFolder = null;
                if (pngFolderDialog.ShowDialog() == true)
                    pngFolder = pngFolderDialog.FolderName;

                // 3. Выбор места сохранения
                var folderDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Выберите место для сохранения папки с документами Word",
                    FileName = $"Зоны_Word_экспорт_{DateTime.Now:dd_MM_yyyy}_Время_{DateTime.Now:HH_mm}",
                    Filter = "Папка|*.folder",
                    CheckFileExists = false,
                    CheckPathExists = false,
                    ValidateNames = false,
                    OverwritePrompt = false
                };
                if (folderDialog.ShowDialog() != true)
                {
                    MessageBox.Show("Сохранение отменено.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string exportFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folderDialog.FileName),
                                      System.IO.Path.GetFileNameWithoutExtension(folderDialog.FileName));
                Directory.CreateDirectory(exportFolder);

                // 4. Подготовка списка зон
                LoadZonesToSelector();
                var zonesList = new List<ZoneInfo>();
                var allHydrants = _ewsService.GetAllEwssWithDisplay();

                foreach (var zone in zonesDictionary)
                {
                    var bounds = _dbService.GetZoneBounds(zone.Key);
                    if (bounds?.HasValidBounds == true)
                    {
                        zonesList.Add(new ZoneInfo
                        {
                            ZoneId = zone.Key,
                            ZoneName = zone.Value,
                            MinLat = bounds.MinLat,
                            MaxLat = bounds.MaxLat,
                            MinLng = bounds.MinLng,
                            MaxLng = bounds.MaxLng,
                            HydrantCount = GetHydrantsInZone(zone.Key).Count
                        });
                    }
                }

                if (zonesList.Count == 0)
                {
                    MessageBox.Show("Не найдено ни одной зоны.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 5. Окно прогресса
                var progressWindow = new ExportProgressWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                progressWindow.SetMaxProgress(zonesList.Count * 2);
                progressWindow.Show();

                int errorCount = 0;
                int completed = 0;

                // 6. Основная работа в фоне
                await Task.Run(async () =>
                {
                    foreach (var zone in zonesList)
                    {
                        try
                        {
                            string zoneFolder = System.IO.Path.Combine(exportFolder, $"{zone.ZoneId:00}_{SanitizeFileName(zone.ZoneName)}");
                            Directory.CreateDirectory(zoneFolder);

                            // --- ОБЩИЙ ПЛАН ЗОНЫ (С ТИТУЛЬНЫМ ЛИСТОМ) ---
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressWindow.SetProgressText($"Обработка: {zone.ZoneName}");
                                progressWindow.SetStatusText($"Создание общего плана для зоны {zone.ZoneName}");
                                progressWindow.UpdateLayout();
                            });

                            string overviewFile = System.IO.Path.Combine(zoneFolder, $"00_{SanitizeFileName(zone.ZoneName)}_общий_план.docx");
                            string overviewPng = null;

                            if (!string.IsNullOrEmpty(pngFolder) && Directory.Exists(pngFolder))
                                overviewPng = FindPngForZone(pngFolder, zone.ZoneId, zone.ZoneName);

                            // Создаем общий план с титульным листом, таблицей и картинкой
                            CreateZoneOverviewWithTitlePage(zone, overviewFile, overviewPng, titleDialog.TitlePageData);

                            Interlocked.Increment(ref completed);
                            Application.Current.Dispatcher.Invoke(() => progressWindow.SetProgressValue(completed));
                            await Task.Delay(100);

                            // --- СЕТКА ЗОНЫ (БЕЗ ТИТУЛЬНОГО ЛИСТА, ТОЛЬКО КАРТИНКИ ПОДЗОН) ---
                            if (zone.HydrantCount > 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    progressWindow.SetStatusText($"Создание сетки для зоны {zone.ZoneName}");
                                    progressWindow.UpdateLayout();
                                });

                                string gridFile = System.IO.Path.Combine(zoneFolder, $"01_{SanitizeFileName(zone.ZoneName)}_сетка.docx");
                                var gridPngs = new List<string>();

                                if (!string.IsNullOrEmpty(pngFolder) && Directory.Exists(pngFolder))
                                    gridPngs = FindAllGridPngsForZone(pngFolder, zone.ZoneId, zone.ZoneName);

                                if (gridPngs.Count > 0)
                                    CreateZoneGridWithMultipleImages(zone, gridFile, gridPngs);

                                Interlocked.Increment(ref completed);
                                Application.Current.Dispatcher.Invoke(() => progressWindow.SetProgressValue(completed));
                                await Task.Delay(100);
                            }
                            else
                            {
                                Interlocked.Increment(ref completed);
                                Application.Current.Dispatcher.Invoke(() => progressWindow.SetProgressValue(completed));
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref errorCount);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressWindow.SetStatusText($"Ошибка в зоне {zone.ZoneName}: {ex.Message}");
                            });
                            await Task.Delay(500);
                        }
                    }
                });

                progressWindow.Close();

                string message = $"Сохранение завершено\nОшибок: {errorCount}\nПапка: {exportFolder}";
                if (string.IsNullOrEmpty(pngFolder))
                    message += "\n\nИзображения не добавлены (папка с PNG не выбрана)";

                MessageBox.Show(message, "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

                if (MessageBox.Show("Открыть папку?", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", exportFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateZoneGridWithMultipleImages(ZoneInfo zone, string fileName, List<string> imagePaths)
        {
            using (var document = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Настройка страницы - альбомная ориентация
                var sectionProps = new SectionProperties();
                sectionProps.AppendChild(new PageSize() { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape });
                sectionProps.AppendChild(new PageMargin() { Top = 500, Right = 500, Bottom = 500, Left = 500 });
                body.AppendChild(sectionProps);

                // Заголовок
                body.AppendChild(CreateWordParagraph($"Сетка зоны: {zone.ZoneName}", 24, true, JustificationValues.Center));
                body.AppendChild(CreateWordParagraph("", 12));

                int imageCounter = 1;
                foreach (var imagePath in imagePaths)
                {
                    try
                    {
                        ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png);
                        using (FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                        {
                            imagePart.FeedData(stream);
                        }

                        string relationshipId = mainPart.GetIdOfPart(imagePart);

                        var run = new Run();
                        var drawing = new Drawing();
                        var inline = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline();

                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent() { Cx = 6500000L, Cy = 4800000L });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties() { Id = (uint)imageCounter, Name = $"Grid{imageCounter}" });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties());

                        var graphic = new DocumentFormat.OpenXml.Drawing.Graphic();
                        var graphicData = new DocumentFormat.OpenXml.Drawing.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };

                        var picture = new DocumentFormat.OpenXml.Drawing.Pictures.Picture();

                        var nvPicPr = new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties();
                        nvPicPr.AppendChild(new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties() { Id = (uint)(imageCounter + 100), Name = $"Grid{imageCounter}" });
                        nvPicPr.AppendChild(new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties());
                        picture.AppendChild(nvPicPr);

                        var blipFill = new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill();
                        blipFill.AppendChild(new DocumentFormat.OpenXml.Drawing.Blip() { Embed = relationshipId });
                        blipFill.AppendChild(new DocumentFormat.OpenXml.Drawing.Stretch());
                        picture.AppendChild(blipFill);

                        var spPr = new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties();
                        var xfrm = new DocumentFormat.OpenXml.Drawing.Transform2D();
                        xfrm.AppendChild(new DocumentFormat.OpenXml.Drawing.Offset() { X = 0L, Y = 0L });
                        xfrm.AppendChild(new DocumentFormat.OpenXml.Drawing.Extents() { Cx = 6500000L, Cy = 4800000L });
                        spPr.AppendChild(xfrm);
                        spPr.AppendChild(new DocumentFormat.OpenXml.Drawing.PresetGeometry() { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });
                        picture.AppendChild(spPr);

                        graphicData.AppendChild(picture);
                        graphic.AppendChild(graphicData);
                        inline.AppendChild(graphic);
                        drawing.AppendChild(inline);
                        run.AppendChild(drawing);

                        var paragraph = new Paragraph();
                        paragraph.AppendChild(run);
                        paragraph.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                        body.AppendChild(paragraph);

                        body.AppendChild(CreateWordParagraph("", 12));

                        imageCounter++;
                    }
                    catch (Exception ex)
                    {
                        body.AppendChild(CreateWordParagraph($"Ошибка вставки изображения {imageCounter}: {ex.Message}", 12, false, JustificationValues.Left));
                    }
                }

                document.Save();
            }
        }

        private void CreateTitlePageContent(Body body, TitlePageData data)
        {
            body.AppendChild(CreateWordParagraph("", 12));
            body.AppendChild(CreateWordParagraph("", 12));
            body.AppendChild(CreateWordParagraph("", 12));

            var firstLinePara = new Paragraph();
            firstLinePara.AppendChild(CreateRun($"Составлен «{data.CompositionDay}» _____________ 2026 г. в количестве _____ экз.", 12, false));
            firstLinePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }));
            body.AppendChild(firstLinePara);

            body.AppendChild(CreateWordParagraph("", 24));
            body.AppendChild(CreateWordParagraph("", 24));

            var approvePara = new Paragraph();
            approvePara.AppendChild(CreateRun("УТВЕРЖДАЮ", 12, false));
            approvePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }));
            body.AppendChild(approvePara);

            body.AppendChild(CreateWordParagraph("", 12));

            var positionPara = new Paragraph();
            positionPara.AppendChild(CreateRun(data.Position, 12, false));
            positionPara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }));
            body.AppendChild(positionPara);

            body.AppendChild(CreateWordParagraph("", 12));

            var rankPara = new Paragraph();
            rankPara.AppendChild(CreateRun(data.Rank, 12, false));
            rankPara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }));
            body.AppendChild(rankPara);

            body.AppendChild(CreateWordParagraph("", 24));

            var signaturePara = new Paragraph();
            signaturePara.AppendChild(CreateRun("__________________ ", 12, false));
            signaturePara.AppendChild(CreateRun(data.ChiefName, 12, false));
            signaturePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }));
            body.AppendChild(signaturePara);

            body.AppendChild(CreateWordParagraph("", 12));

            var approveDatePara = new Paragraph();
            approveDatePara.AppendChild(CreateRun("«_____» _____________________ 2026 г.", 12, false));
            approveDatePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }));
            body.AppendChild(approveDatePara);

            body.AppendChild(CreateWordParagraph("", 48));
            body.AppendChild(CreateWordParagraph("", 24));

            var titlePara = new Paragraph();
            var titleRun = new Run();
            var titleProps = new RunProperties();
            titleProps.AppendChild(new FontSize() { Val = "28" });
            titleProps.AppendChild(new Bold());
            titleRun.AppendChild(titleProps);
            titleRun.AppendChild(new Text(data.TitleText));
            titlePara.AppendChild(titleRun);
            titlePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
            body.AppendChild(titlePara);

            body.AppendChild(CreateWordParagraph("", 48));
            body.AppendChild(CreateWordParagraph("", 48));
            body.AppendChild(CreateWordParagraph("", 24));

            var compilerPara = new Paragraph();
            compilerPara.AppendChild(CreateRun("Составил: ", 12, true));
            compilerPara.AppendChild(CreateRun(data.CompilerPosition, 12, false));
            compilerPara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }));
            body.AppendChild(compilerPara);

            body.AppendChild(CreateWordParagraph("", 12));

            var compilerSignaturePara = new Paragraph();
            compilerSignaturePara.AppendChild(CreateRun("__________________ ", 12, false));
            compilerSignaturePara.AppendChild(CreateRun(data.CompilerName, 12, false));
            compilerSignaturePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }));
            body.AppendChild(compilerSignaturePara);

            body.AppendChild(CreateWordParagraph("", 12));

            var compilerDatePara = new Paragraph();
            compilerDatePara.AppendChild(CreateRun("«___» ______________ 2026 г.", 12, false));
            compilerDatePara.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }));
            body.AppendChild(compilerDatePara);
        }

        private void CreateZoneOverviewWithTitlePage(ZoneInfo zone, string fileName, string imagePath, TitlePageData titleData)
        {
            using (var document = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // === ТИТУЛЬНЫЙ ЛИСТ ===
                var sectionProps = new SectionProperties();
                sectionProps.AppendChild(new PageSize() { Width = 11906, Height = 16838, Orient = PageOrientationValues.Portrait });
                sectionProps.AppendChild(new PageMargin() { Top = 850, Right = 850, Bottom = 850, Left = 850 });
                body.AppendChild(sectionProps);

                CreateTitlePageContent(body, titleData);

                // Разрыв страницы
                body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));

                // === ОБЩИЙ ПЛАН (АЛЬБОМНАЯ ОРИЕНТАЦИЯ) ===
                var sectionProps2 = new SectionProperties();
                sectionProps2.AppendChild(new PageSize() { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape });
                sectionProps2.AppendChild(new PageMargin() { Top = 500, Right = 500, Bottom = 500, Left = 500 });
                body.AppendChild(sectionProps2);

                // Заголовок
                body.AppendChild(CreateWordParagraph($"ОБЩИЙ ПЛАН ЗОНЫ: {zone.ZoneName}", 24, true, JustificationValues.Center));
                body.AppendChild(CreateWordParagraph($"ID: {zone.ZoneId} | Гидрантов: {zone.HydrantCount}", 14, false, JustificationValues.Center));
                body.AppendChild(CreateWordParagraph($"Границы: Широта {zone.MinLat:F6} - {zone.MaxLat:F6} | Долгота {zone.MinLng:F6} - {zone.MaxLng:F6}", 12, false, JustificationValues.Center));
                body.AppendChild(CreateWordParagraph("", 12));

                // === ТАБЛИЦА С ГИДРАНТАМИ ===
                var hydrants = GetHydrantsInZone(zone.ZoneId).OrderBy(h => h.DisplayNumber).ToList();
                if (hydrants.Count > 0)
                {
                    body.AppendChild(CreateWordParagraph("СПИСОК ГИДРАНТОВ", 18, true, JustificationValues.Center));
                    body.AppendChild(CreateWordParagraph("", 6));

                    var table = new Table();
                    var tableProps = new TableProperties(
                        new TableWidth() { Width = "100", Type = TableWidthUnitValues.Pct },
                        new TableBorders(
                            new TopBorder() { Val = BorderValues.Single, Size = 4 },
                            new BottomBorder() { Val = BorderValues.Single, Size = 4 },
                            new LeftBorder() { Val = BorderValues.Single, Size = 4 },
                            new RightBorder() { Val = BorderValues.Single, Size = 4 },
                            new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4 },
                            new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4 }
                        )
                    );
                    table.AppendChild(tableProps);

                    // Заголовки таблицы
                    var headerRow = new TableRow();
                    headerRow.AppendChild(CreateWordTableCell("№", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Номер", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Диаметр", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Адрес", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Принадлежность", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Статус", true, 11, "D3D3D3", JustificationValues.Center));
                    headerRow.AppendChild(CreateWordTableCell("Причина поломки", true, 11, "D3D3D3", JustificationValues.Center));
                    table.AppendChild(headerRow);

                    // Данные
                    for (int i = 0; i < hydrants.Count; i++)
                    {
                        var h = hydrants[i];
                        string statusColor = h.StatusName == "Исправный" ? "D4EDDA" : (h.StatusName == "Неисправный" ? "F8D7DA" : "FFF3CD");

                        var row = new TableRow();
                        row.AppendChild(CreateWordTableCell((i + 1).ToString(), false, 10, null, JustificationValues.Center));
                        row.AppendChild(CreateWordTableCell(h.DisplayNumber ?? "", false, 10, null, JustificationValues.Center));
                        row.AppendChild(CreateWordTableCell(h.PipeInfo ?? "", false, 10, null, JustificationValues.Center));
                        row.AppendChild(CreateWordTableCell(h.AddressText ?? "", false, 10));
                        row.AppendChild(CreateWordTableCell(h.OrganizationName ?? "", false, 10));
                        row.AppendChild(CreateWordTableCell(h.StatusName ?? "", false, 10, statusColor, JustificationValues.Center));
                        row.AppendChild(CreateWordTableCell("", false, 10));
                        table.AppendChild(row);
                    }
                    body.AppendChild(table);
                    body.AppendChild(CreateWordParagraph("", 12));
                }
                else
                {
                    body.AppendChild(CreateWordParagraph("В зоне нет гидрантов.", 12, true, JustificationValues.Center));
                    body.AppendChild(CreateWordParagraph("", 12));
                }

                // === КАРТИНКА ОБЩЕГО ПЛАНА (ВНИЗУ) ===
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    body.AppendChild(CreateWordParagraph("КАРТА ЗОНЫ:", 16, true, JustificationValues.Center));
                    body.AppendChild(CreateWordParagraph("", 6));

                    try
                    {
                        ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png);
                        using (FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                            imagePart.FeedData(stream);

                        string relationshipId = mainPart.GetIdOfPart(imagePart);

                        var run = new Run();
                        var drawing = new Drawing();
                        var inline = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline();

                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent() { Cx = 6000000L, Cy = 4500000L });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties() { Id = 1U, Name = "ZoneMap" });
                        inline.AppendChild(new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties());

                        var graphic = new DocumentFormat.OpenXml.Drawing.Graphic();
                        var graphicData = new DocumentFormat.OpenXml.Drawing.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
                        var picture = new DocumentFormat.OpenXml.Drawing.Pictures.Picture();

                        var nvPicPr = new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties();
                        nvPicPr.AppendChild(new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties() { Id = 2U, Name = "ZoneMap" });
                        nvPicPr.AppendChild(new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties());
                        picture.AppendChild(nvPicPr);

                        var blipFill = new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill();
                        blipFill.AppendChild(new DocumentFormat.OpenXml.Drawing.Blip() { Embed = relationshipId });
                        blipFill.AppendChild(new DocumentFormat.OpenXml.Drawing.Stretch());
                        picture.AppendChild(blipFill);

                        var spPr = new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties();
                        var xfrm = new DocumentFormat.OpenXml.Drawing.Transform2D();
                        xfrm.AppendChild(new DocumentFormat.OpenXml.Drawing.Offset() { X = 0L, Y = 0L });
                        xfrm.AppendChild(new DocumentFormat.OpenXml.Drawing.Extents() { Cx = 6000000L, Cy = 4500000L });
                        spPr.AppendChild(xfrm);
                        spPr.AppendChild(new DocumentFormat.OpenXml.Drawing.PresetGeometry() { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });
                        picture.AppendChild(spPr);

                        graphicData.AppendChild(picture);
                        graphic.AppendChild(graphicData);
                        inline.AppendChild(graphic);
                        drawing.AppendChild(inline);
                        run.AppendChild(drawing);

                        var paragraph = new Paragraph();
                        paragraph.AppendChild(run);
                        paragraph.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                        body.AppendChild(paragraph);
                    }
                    catch (Exception ex)
                    {
                        body.AppendChild(CreateWordParagraph($"Ошибка вставки изображения: {ex.Message}", 12));
                    }
                }

                document.Save();
            }
        }

        private Run CreateRun(string text, int fontSize, bool bold = false)
        {
            var run = new Run();
            var runProps = new RunProperties();
            runProps.AppendChild(new FontSize() { Val = (fontSize * 2).ToString() });
            if (bold) runProps.AppendChild(new Bold());
            run.AppendChild(runProps);
            run.AppendChild(new Text(text));
            return run;
        }

        private TableCell CreateWordTableCell(string text, bool bold = false, int fontSize = 11,
    string backgroundColor = null, JustificationValues? alignment = null)
        {
            var cell = new TableCell();
            var cellProps = new TableCellProperties();

            if (!string.IsNullOrEmpty(backgroundColor))
            {
                cellProps.AppendChild(new Shading() { Val = ShadingPatternValues.Clear, Color = "auto", Fill = backgroundColor });
            }
            cell.AppendChild(cellProps);

            var paragraph = new Paragraph();
            var run = new Run();
            var runProps = new RunProperties(new FontSize() { Val = (fontSize * 2).ToString() });
            if (bold) runProps.AppendChild(new Bold());

            run.AppendChild(runProps);
            run.AppendChild(new Text(text ?? ""));
            paragraph.AppendChild(run);

            if (alignment.HasValue)
                paragraph.AppendChild(new ParagraphProperties(new Justification() { Val = alignment.Value }));

            cell.AppendChild(paragraph);
            return cell;
        }

        private Paragraph CreateWordParagraph(string text, int fontSize, bool bold = false,
           JustificationValues? alignment = null, bool italic = false)
        {
            var paragraph = new Paragraph();
            var run = new Run();
            var runProps = new RunProperties();

            runProps.AppendChild(new FontSize() { Val = (fontSize * 2).ToString() });
            if (bold) runProps.AppendChild(new Bold());
            if (italic) runProps.AppendChild(new Italic());

            run.AppendChild(runProps);
            run.AppendChild(new Text(text));
            paragraph.AppendChild(run);

            if (alignment.HasValue)
            {
                paragraph.AppendChild(new ParagraphProperties(new Justification() { Val = alignment.Value }));
            }

            return paragraph;
        }

        private List<string> FindAllGridPngsForZone(string pngFolder, int zoneId, string zoneName)
        {
            var result = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(pngFolder) || !Directory.Exists(pngFolder)) return result;

                string zoneSubFolder = System.IO.Path.Combine(pngFolder, $"{zoneId:D2}_{SanitizeFileName(zoneName)}");
                string zoneIdPrefix = $"{zoneId:D2}_";
                string sanitizedName = SanitizeFileName(zoneName).ToLower();

                if (Directory.Exists(zoneSubFolder))
                {
                    var files = Directory.GetFiles(zoneSubFolder, "*.png", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        string fn = System.IO.Path.GetFileName(file).ToLower();
                        if (fn.Contains("сетка") || fn.Contains("grid"))
                            result.Add(file);
                    }
                }

                if (result.Count == 0)
                {
                    var files = Directory.GetFiles(pngFolder, "*.png", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        string fn = System.IO.Path.GetFileName(file).ToLower();
                        if ((fn.Contains("сетка") || fn.Contains("grid")) &&
                            (fn.StartsWith(zoneIdPrefix) || fn.Contains(sanitizedName)))
                            result.Add(file);
                    }
                }

                result.Sort();
            }
            catch { }
            return result;
        }

        private string FindPngForZone(string pngFolder, int zoneId, string zoneName)
        {
            try
            {
                var files = Directory.GetFiles(pngFolder, "*.png", SearchOption.AllDirectories);
                string sanitizedName = SanitizeFileName(zoneName);

                foreach (var file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    string fileNameLower = fileName.ToLower();

                    if (fileNameLower.Contains("сетка") || fileNameLower.Contains("grid") || fileNameLower.Contains("стр"))
                        continue;

                    if (!fileNameLower.Contains("общий_план") && !fileNameLower.Contains("overview"))
                        continue;

                    string zoneIdPrefix = $"{zoneId:D2}_";
                    if (fileName.StartsWith(zoneIdPrefix) || fileName.Contains($"_{zoneId:D2}_"))
                    {
                        return file;
                    }
                }

                foreach (var file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    string fileNameLower = fileName.ToLower();

                    if (fileNameLower.Contains("сетка") || fileNameLower.Contains("grid") || fileNameLower.Contains("стр"))
                        continue;

                    if (!fileNameLower.Contains("общий_план") && !fileNameLower.Contains("overview"))
                        continue;

                    if (fileNameLower.Contains(sanitizedName.ToLower()))
                    {
                        return file;
                    }
                }

                foreach (var file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    string fileNameLower = fileName.ToLower();

                    if (fileNameLower.Contains("сетка") || fileNameLower.Contains("grid") || fileNameLower.Contains("стр"))
                        continue;

                    string zoneIdPrefix = $"{zoneId:D2}_";
                    if (fileName.StartsWith(zoneIdPrefix))
                    {
                        return file;
                    }
                }
            }
            catch { }
            return null;
        }
        #endregion
    }
}