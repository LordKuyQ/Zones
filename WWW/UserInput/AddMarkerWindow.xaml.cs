using System.Windows;
using System.Windows.Controls;
using TestDbApp.Models;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor
{
    public partial class AddMarkerWindow : Window
    {
        public Ewss EditEwss { get; set; } = new Ewss();
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string ChangeReason => ChangeReasonTextBox.Text;
        private bool _isLoaded = false;
        private bool _isAddressResolving = false;
        private readonly GeocodingHelper _geocodingService;
        private readonly EwsMapDataService _ewsService;
        private List<_05Organization> _organizations;
        private List<EwsType> _types;
        private List<EwsPipeType> _pipeTypes;
        private List<EwsDiameter> _diameters;
        private List<EwsPkdiameter> _pkdiameters;
        private List<_04AdressObject> _addressObjects;
        private List<CEwsStatus> _statuses;

        public AddMarkerWindow(EwsMapDataService ewsService = null)
        {
            InitializeComponent();
            _geocodingService = new GeocodingHelper();
            _ewsService = ewsService ?? new EwsMapDataService();
            LoadReferenceData();
            Loaded += AddMarkerWindow_Loaded;
        }

        private void LoadReferenceData()
        {
            try
            {
                // Загрузка типов гидрантов
                _types = _ewsService.GetAllEwsTypes();
                TypeComboBox.Items.Clear();
                TypeComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var t in _types)
                    TypeComboBox.Items.Add(new ComboBoxItem { Content = t.EwsTypeNameShort ?? "", Tag = t.EwsTypeId ?? "" });

                // Загрузка типов труб
                _pipeTypes = _ewsService.GetAllEwsPipeTypes();
                PipeTypeComboBox.Items.Clear();
                PipeTypeComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var p in _pipeTypes)
                    PipeTypeComboBox.Items.Add(new ComboBoxItem { Content = p.EwsPipeTypeName ?? "", Tag = p.EwsPipeTypeId ?? "" });

                // ЗАГРУЗКА ДИАМЕТРОВ (исправленная версия)
                try
                {
                    _diameters = _ewsService.GetAllEwsDiameters();

                    // Диагностика в Output окно вместо MessageBox
                    System.Diagnostics.Debug.WriteLine($"=== Диагностика диаметров ===");
                    System.Diagnostics.Debug.WriteLine($"Количество диаметров: {_diameters?.Count ?? 0}");

                    DiameterComboBox.Items.Clear();

                    if (_diameters != null && _diameters.Any())
                    {
                        foreach (var d in _diameters)
                        {
                            // Формируем отображаемое значение
                            string displayValue = "";

                            // Проверяем все возможные поля
                            if (!string.IsNullOrWhiteSpace(d.EwsDiameter1))
                            {
                                displayValue = d.EwsDiameter1;
                            }
                            else if (!string.IsNullOrWhiteSpace(d.Note))
                            {
                                displayValue = d.Note;
                            }
                            else
                            {
                                displayValue = "(без значения)";
                            }

                            // Добавляем единицу измерения, если есть
                            if (!string.IsNullOrWhiteSpace(d.EwsIzm))
                            {
                                displayValue = $"{displayValue} {d.EwsIzm}";
                            }

                            // Создаем ComboBoxItem
                            var item = new ComboBoxItem
                            {
                                Content = displayValue.Trim(),
                                Tag = d.EwsDiameterId ?? ""
                            };

                            DiameterComboBox.Items.Add(item);

                            // Выводим в Debug
                            System.Diagnostics.Debug.WriteLine($"Добавлен диаметр: ID={d.EwsDiameterId}, Значение={displayValue}, EwsDiameter1={d.EwsDiameter1}");
                        }
                    }
                    else
                    {
                        // Если данных нет, добавляем тестовые значения
                        DiameterComboBox.Items.Add(new ComboBoxItem { Content = "(нет данных)", Tag = "" });
                        System.Diagnostics.Debug.WriteLine("Диаметры не найдены в БД");
                    }

                    // Устанавливаем выбранный элемент по умолчанию
                    if (DiameterComboBox.Items.Count > 0)
                        DiameterComboBox.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки диаметров: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);

                    MessageBox.Show($"Ошибка загрузки диаметров: {ex.Message}\n\nПроверьте подключение к БД и наличие данных в таблице EwsDiameter.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

                    DiameterComboBox.Items.Clear();
                    DiameterComboBox.Items.Add(new ComboBoxItem { Content = "Ошибка загрузки", Tag = "" });
                }

                // ЗАГРУЗКА ДИАМЕТРОВ ПК (исправленная версия)
                try
                {
                    _pkdiameters = _ewsService.GetAllEwsPkdiameters();

                    System.Diagnostics.Debug.WriteLine($"=== Диагностика диаметров ПК ===");
                    System.Diagnostics.Debug.WriteLine($"Количество диаметров ПК: {_pkdiameters?.Count ?? 0}");

                    PKDiameterComboBox.Items.Clear();

                    if (_pkdiameters != null && _pkdiameters.Any())
                    {
                        foreach (var pkd in _pkdiameters)
                        {
                            string displayValue = "";

                            if (!string.IsNullOrWhiteSpace(pkd.EwsPkdiameter1))
                            {
                                displayValue = pkd.EwsPkdiameter1;
                            }
                            else if (!string.IsNullOrWhiteSpace(pkd.Note))
                            {
                                displayValue = pkd.Note;
                            }
                            else
                            {
                                displayValue = "(без значения)";
                            }

                            if (!string.IsNullOrWhiteSpace(pkd.EwsIzm))
                            {
                                displayValue = $"{displayValue} {pkd.EwsIzm}";
                            }

                            var item = new ComboBoxItem
                            {
                                Content = displayValue.Trim(),
                                Tag = pkd.EwsPkdiameterId ?? ""
                            };

                            PKDiameterComboBox.Items.Add(item);

                            System.Diagnostics.Debug.WriteLine($"Добавлен диаметр ПК: ID={pkd.EwsPkdiameterId}, Значение={displayValue}");
                        }
                    }
                    else
                    {
                        PKDiameterComboBox.Items.Add(new ComboBoxItem { Content = "(нет данных)", Tag = "" });
                        System.Diagnostics.Debug.WriteLine("Диаметры ПК не найдены в БД");
                    }

                    if (PKDiameterComboBox.Items.Count > 0)
                        PKDiameterComboBox.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки диаметров ПК: {ex.Message}");

                    MessageBox.Show($"Ошибка загрузки диаметров ПК: {ex.Message}\n\nПроверьте подключение к БД и наличие данных в таблице EwsPkdiameter.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

                    PKDiameterComboBox.Items.Clear();
                    PKDiameterComboBox.Items.Add(new ComboBoxItem { Content = "Ошибка загрузки", Tag = "" });
                }

                // Остальной код загрузки остается без изменений...
                _addressObjects = _ewsService.GetAllAdressObjects();
                AddressObjectComboBox.Items.Clear();
                AddressObjectComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var a in _addressObjects)
                    AddressObjectComboBox.Items.Add(new ComboBoxItem { Content = a.AdressObjectName ?? "", Tag = a.AdressObjectId ?? "" });

                _organizations = _ewsService.GetAllOrganizations();
                CompanyComboBox.Items.Clear();
                CompanyComboBox.Items.Add(new ComboBoxItem { Content = "(не выбрана)", Tag = "" });
                foreach (var org in _organizations)
                    CompanyComboBox.Items.Add(new ComboBoxItem { Content = org.OrganizationNameShort ?? "", Tag = org.OrganizationId?.ToString() ?? "0" });

                _statuses = _ewsService.GetAllCEwsStatuses();
                StatusComboBox.Items.Clear();
                StatusComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var s in _statuses)
                    StatusComboBox.Items.Add(new ComboBoxItem { Content = s.EwsStatusName ?? "", Tag = s.EwsStatusId ?? "" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочных данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AutoAddressButton_Click(object sender, RoutedEventArgs e)
        {
            await ResolveAddressAsync();
        }

        private async Task ResolveAddressAsync()
        {
            if (_isAddressResolving) return;
            try
            {
                _isAddressResolving = true;
                AutoAddressButton.IsEnabled = false;
                AutoAddressButton.Content = "Поиск";
                GidrantAdresTextBox.Text = "Определение адреса";
                GidrantAdresTextBox.Background = System.Windows.Media.Brushes.LightYellow;
                string address = await _geocodingService.GetAddressFromCoordinatesAsync(Latitude.Value, Longitude.Value);

                if (!string.IsNullOrEmpty(address) && address != "Адрес не найден")
                {
                    GidrantAdresTextBox.Text = address;
                    GidrantAdresTextBox.Background = System.Windows.Media.Brushes.LightGreen;
                    await Task.Delay(500);
                    GidrantAdresTextBox.Background = System.Windows.Media.Brushes.White;
                }
                else
                {
                    string coordsAddress = $"Координаты: {Latitude.Value:F6}, {Longitude.Value:F6}";
                    GidrantAdresTextBox.Text = coordsAddress;
                    GidrantAdresTextBox.Background = System.Windows.Media.Brushes.LightYellow;
                }
            }
            catch (Exception ex)
            {
                GidrantAdresTextBox.Text = $"Ошибка: {ex.Message}";
                GidrantAdresTextBox.Background = System.Windows.Media.Brushes.LightCoral;
            }
            finally
            {
                _isAddressResolving = false;
                AutoAddressButton.IsEnabled = true;
                AutoAddressButton.Content = "Повторить";
            }
        }

        private void AddMarkerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GidrantNumberTextBox.Text = EditEwss.EwsNumber ?? "";

            SelectComboBoxItemByTag(TypeComboBox, EditEwss.EwsTypeCod);
            SelectComboBoxItemByTag(PipeTypeComboBox, EditEwss.EwsPipeTypeCod);
            SelectComboBoxItemByTag(DiameterComboBox, EditEwss.EwsDiameterCod);
            SelectComboBoxItemByTag(PKDiameterComboBox, EditEwss.EwsPkdiameterCod);
            SelectComboBoxItemByTag(AddressObjectComboBox, EditEwss.EwsAdressObjectCod);
            HouseNumberTextBox.Text = EditEwss.EwsHouseNumber ?? "";
            AddressNoteTextBox.Text = EditEwss.EwsAdressNote ?? "";
            GidrantAdresTextBox.Text = EditEwss.AddressText ?? "";
            SelectComboBoxItemByTag(CompanyComboBox, EditEwss.EwsOrganizationCod);
            SelectComboBoxItemByTag(StatusComboBox, EditEwss.EwsStatusCod);
            NotesTextBox.Text = EditEwss.EwsNotes ?? "";

            if (StatusComboBox.SelectedIndex < 0 && StatusComboBox.Items.Count > 0)
                StatusComboBox.SelectedIndex = 0;

            _isLoaded = true;

            if (Latitude.HasValue && Longitude.HasValue && string.IsNullOrEmpty(EditEwss.AddressText))
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(100);
                    await ResolveAddressAsync();
                }));
            }
        }

        private static void SelectComboBoxItemByTag(ComboBox combo, string? tagValue)
        {
            if (string.IsNullOrEmpty(tagValue)) return;

            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == tagValue)
                {
                    item.IsSelected = true;
                    return;
                }
            }
        }

        private void AddCompanyButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Введите название новой принадлежности:", "Новая принадлежность");
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer))
            {
                string newCompany = dialog.Answer.Trim();

                bool exists = false;
                foreach (ComboBoxItem item in CompanyComboBox.Items)
                {
                    if (item.Content.ToString().Equals(newCompany, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    MessageBox.Show("Такая принадлежность уже существует", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    _ewsService.AddOrganization(newCompany);
                    var newItem = new ComboBoxItem { Content = newCompany };
                    CompanyComboBox.Items.Add(newItem);
                    CompanyComboBox.SelectedItem = newItem;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            EditEwss.EwsNumber = GidrantNumberTextBox.Text;
            EditEwss.EwsTypeCod = (TypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsPipeTypeCod = (PipeTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsDiameterCod = (DiameterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsPkdiameterCod = (PKDiameterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsAdressObjectCod = (AddressObjectComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsHouseNumber = HouseNumberTextBox.Text;
            EditEwss.EwsAdressNote = AddressNoteTextBox.Text;
            EditEwss.EwsOrganizationCod = (CompanyComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsStatusCod = (StatusComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            EditEwss.EwsNotes = NotesTextBox.Text;

            if (string.IsNullOrWhiteSpace(EditEwss.EwsNumber))
            {
                MessageBox.Show("Пожалуйста, введите номер гидранта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}