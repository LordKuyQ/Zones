using System.Windows;
using System.Windows.Controls;
using WpfApp5.Models;
using ZoneHydrantEditor.Helpers;
using ZoneHydrantEditor.Models;

namespace ZoneHydrantEditor
{
    public partial class AddMarkerWindow : Window
    {
        public string GidrantNumber { get; set; }
        public string GidrantTruba { get; set; }
        public string GidrantAdres { get; set; }
        public string CompanyName { get; set; }
        public string Status { get; set; }
        public string BreakReason { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        private bool _isLoaded = false;
        private bool _isAddressResolving = false;
        private readonly GeocodingHelper _geocodingService;
        private readonly DatabaseService _dbService;
        private List<CompanyInfo> _companies;

        public AddMarkerWindow(DatabaseService dbService = null)
        {
            InitializeComponent();
            _geocodingService = new GeocodingHelper();
            _dbService = dbService ?? new DatabaseService();
            LoadCompanies();
            Loaded += AddMarkerWindow_Loaded;
        }

        private void LoadCompanies()
        {
            try
            {
                _companies = _dbService.GetAllCompanies();
                CompanyComboBox.Items.Clear();

                foreach (var company in _companies)
                {
                    CompanyComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = company.Name,
                        Tag = company.Id
                    });
                }

                if (CompanyComboBox.Items.Count > 0)
                    CompanyComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка принадлежностей: {ex.Message}");
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
                    GidrantAdres = address;
                    GidrantAdresTextBox.Background = System.Windows.Media.Brushes.LightGreen;
                    await Task.Delay(500);
                    GidrantAdresTextBox.Background = System.Windows.Media.Brushes.White;
                }
                else
                {
                    string coordsAddress = $"Координаты: {Latitude.Value:F6}, {Longitude.Value:F6}";
                    GidrantAdresTextBox.Text = coordsAddress;
                    GidrantAdres = coordsAddress;
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
            GidrantNumberTextBox.Text = GidrantNumber ?? "";
            GidrantTrubaTextBox.Text = GidrantTruba ?? "";
            GidrantAdresTextBox.Text = GidrantAdres ?? "";

            if (!string.IsNullOrEmpty(CompanyName))
            {
                foreach (ComboBoxItem item in CompanyComboBox.Items)
                {
                    if (item.Content.ToString() == CompanyName)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }

            string statusToSelect = Status ?? "Непроверенный";
            foreach (ComboBoxItem item in StatusComboBox.Items)
            {
                if (item.Content.ToString() == statusToSelect)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            UpdateBreakReasonVisibility(statusToSelect);

            if (!string.IsNullOrEmpty(BreakReason))
            {
                foreach (ComboBoxItem item in BreakReasonComboBox.Items)
                {
                    if (item.Content.ToString() == BreakReason)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }

            _isLoaded = true;

            if (Latitude.HasValue && Longitude.HasValue && string.IsNullOrEmpty(GidrantAdres))
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(100);
                    await ResolveAddressAsync();
                }));
            }
        }

        private void AddCompanyButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Введите название новой принадлежности:", "Новая принадлежность");
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer))
            {
                string newCompany = dialog.Answer.Trim();

                // Проверяем, нет ли уже такой
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
                    // Сохраняем в БД
                    _dbService.AddCompany(newCompany);

                    // Добавляем в выпадающий список
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

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;

            if (StatusComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                UpdateBreakReasonVisibility(selectedItem.Content.ToString());
            }
        }

        private void UpdateBreakReasonVisibility(string status)
        {
            if (BreakReasonLabel == null || BreakReasonComboBox == null)
                return;

            if (status == "Неисправный")
            {
                BreakReasonLabel.Visibility = Visibility.Visible;
                BreakReasonComboBox.Visibility = Visibility.Visible;
            }
            else
            {
                BreakReasonLabel.Visibility = Visibility.Collapsed;
                BreakReasonComboBox.Visibility = Visibility.Collapsed;
                BreakReason = null;

                if (BreakReasonComboBox.SelectedItem != null)
                {
                    BreakReasonComboBox.SelectedItem = null;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            GidrantNumber = GidrantNumberTextBox.Text;
            GidrantTruba = GidrantTrubaTextBox.Text;
            GidrantAdres = GidrantAdresTextBox.Text;
            CompanyName = (CompanyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Бесхозный";
            Status = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Непроверенный";

            if (Status == "Неисправный")
            {
                BreakReason = (BreakReasonComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            }
            else
            {
                BreakReason = null;
            }

            if (string.IsNullOrWhiteSpace(GidrantNumber))
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