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
                _types = _ewsService.GetAllEwsTypes();
                TypeComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var t in _types)
                    TypeComboBox.Items.Add(new ComboBoxItem { Content = t.EwsTypeNameShort ?? "", Tag = t.EwsTypeId ?? "" });

                _pipeTypes = _ewsService.GetAllEwsPipeTypes();
                PipeTypeComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var p in _pipeTypes)
                    PipeTypeComboBox.Items.Add(new ComboBoxItem { Content = p.EwsPipeTypeName ?? "", Tag = p.EwsPipeTypeId ?? "" });

                _diameters = _ewsService.GetAllEwsDiameters();
                DiameterComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var d in _diameters)
                    DiameterComboBox.Items.Add(new ComboBoxItem { Content = d.EwsDiameter1 ?? "", Tag = d.EwsDiameterId ?? "" });

                _pkdiameters = _ewsService.GetAllEwsPkdiameters();
                PKDiameterComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var pkd in _pkdiameters)
                    PKDiameterComboBox.Items.Add(new ComboBoxItem { Content = pkd.EwsPkdiameter1 ?? "", Tag = pkd.EwsPkdiameterId ?? "" });

                _addressObjects = _ewsService.GetAllAdressObjects();
                AddressObjectComboBox.Items.Add(new ComboBoxItem { Content = "(не выбран)", Tag = "" });
                foreach (var a in _addressObjects)
                    AddressObjectComboBox.Items.Add(new ComboBoxItem { Content = a.AdressObjectName ?? "", Tag = a.AdressObjectId ?? "" });

                _organizations = _ewsService.GetAllOrganizations();
                CompanyComboBox.Items.Add(new ComboBoxItem { Content = "(не выбрана)", Tag = "" });
                foreach (var org in _organizations)
                    CompanyComboBox.Items.Add(new ComboBoxItem { Content = org.OrganizationNameShort ?? "", Tag = org.OrganizationId ?? "0" });

                _statuses = _ewsService.GetAllCEwsStatuses();
                foreach (var s in _statuses)
                    StatusComboBox.Items.Add(new ComboBoxItem { Content = s.EwsStatusName ?? "", Tag = s.EwsStatusId ?? "" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочных данных: {ex.Message}");
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