using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TestDbApp.Models;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor.SyncPage
{
    public partial class SyncPageWindow : Window
    {
        private readonly EwsMapDataService _ewsService;
        private Ewss _selectedEwss;

        public SyncPageWindow()
        {
            InitializeComponent();
            _ewsService = new EwsMapDataService();
            LoadHydrants();
        }

        private void LoadHydrants()
        {
            var ewssList = _ewsService.GetAllEwssWithDisplay();
            HydrantsGrid.ItemsSource = ewssList;
        }

        private void HydrantsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEwss = HydrantsGrid.SelectedItem as Ewss;

            if (_selectedEwss != null)
            {
                HydrantNumberText.Text = _selectedEwss.DisplayNumber;
                PriviazkaTextBox.Text = _selectedEwss.EwsPriviazka ?? "";
                GeoXText.Text = _selectedEwss.EwsPriviazkaGeoX ?? "";
                GeoYText.Text = _selectedEwss.EwsPriviazkaGeoY ?? "";
                PrLeftText.Text = _selectedEwss.EwsPrLeft ?? "";
                PrRightText.Text = _selectedEwss.EwsPrRight ?? "";
                PrStrightText.Text = _selectedEwss.EwsPrStright ?? "";
            }
            else
            {
                HydrantNumberText.Text = "";
                PriviazkaTextBox.Text = "";
                GeoXText.Text = "";
                GeoYText.Text = "";
                PrLeftText.Text = "";
                PrRightText.Text = "";
                PrStrightText.Text = "";
            }
        }

        private void PriviazkaTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedEwss == null) return;

            string text = PriviazkaTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                GeoXText.Text = "";
                GeoYText.Text = "";
                PrLeftText.Text = "";
                PrRightText.Text = "";
                PrStrightText.Text = "";
                return;
            }

            var coord = Utility.ParseBindingCoord(text);
            if (coord == null)
            {
                GeoXText.Text = "?";
                GeoYText.Text = "?";
                PrLeftText.Text = "?";
                PrRightText.Text = "?";
                PrStrightText.Text = "?";
                return;
            }

            double hydrantLat = _selectedEwss.LatitudeD;
            double hydrantLng = _selectedEwss.LongitudeD;

            double dx = (coord.Value.lat - hydrantLat) * 111320.0;
            double dy = (coord.Value.lng - hydrantLng) *
                (111320.0 * Math.Cos(hydrantLat * Math.PI / 180.0));

            double distance = Math.Sqrt(dx * dx + dy * dy);

            GeoXText.Text = dx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            GeoYText.Text = dy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            PrLeftText.Text = dx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            PrRightText.Text = dy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            PrStrightText.Text = distance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void SetBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEwss == null)
            {
                MessageBox.Show("Выберите гидрант из списка.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string priviazka = PriviazkaTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(priviazka))
            {
                MessageBox.Show("Введите координаты привязки в формате: широта,долгота", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var coord = Utility.ParseBindingCoord(priviazka);
            if (coord == null)
            {
                MessageBox.Show("Не удалось распознать координаты. Используйте формат: широта,долгота (например: 55.7558,37.6173)",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _ewsService.UpdateEwssBinding(
                    _selectedEwss.EwsId,
                    priviazka,
                    GeoXText.Text,
                    GeoYText.Text,
                    PrLeftText.Text,
                    PrRightText.Text,
                    PrStrightText.Text
                );

                _selectedEwss.EwsPriviazka = priviazka;
                _selectedEwss.EwsPriviazkaGeoX = GeoXText.Text;
                _selectedEwss.EwsPriviazkaGeoY = GeoYText.Text;
                _selectedEwss.EwsPrLeft = PrLeftText.Text;
                _selectedEwss.EwsPrRight = PrRightText.Text;
                _selectedEwss.EwsPrStright = PrStrightText.Text;

                HydrantsGrid.Items.Refresh();

                InfoText.Text = $"Привязка установлена для гидранта {_selectedEwss.DisplayNumber}";
                InfoText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении привязки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEwss == null)
            {
                MessageBox.Show("Выберите гидрант из списка.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedEwss.EwsPriviazka))
            {
                MessageBox.Show("У этого гидранта нет привязки.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Очистить привязку гидранта {_selectedEwss.DisplayNumber}?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                _ewsService.ClearEwssBinding(_selectedEwss.EwsId);

                _selectedEwss.EwsPriviazka = null;
                _selectedEwss.EwsPriviazkaGeoX = null;
                _selectedEwss.EwsPriviazkaGeoY = null;
                _selectedEwss.EwsPrLeft = null;
                _selectedEwss.EwsPrRight = null;
                _selectedEwss.EwsPrStright = null;

                PriviazkaTextBox.Text = "";
                GeoXText.Text = "";
                GeoYText.Text = "";
                PrLeftText.Text = "";
                PrRightText.Text = "";
                PrStrightText.Text = "";

                HydrantsGrid.Items.Refresh();

                InfoText.Text = $"Привязка гидранта {_selectedEwss.DisplayNumber} очищена";
                InfoText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при очистке привязки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
