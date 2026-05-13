using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ZoneHydrantEditor
{
    public partial class TitlePageDialog : Window
    {
        public TitlePageData TitlePageData { get; private set; }

        public TitlePageDialog()
        {
            InitializeComponent();

            // Установка значений по умолчанию
            DayTextBox.Text = DateTime.Now.Day.ToString();
            MonthTextBox.Text = System.Globalization.CultureInfo.CurrentCulture
                .DateTimeFormat.GetMonthName(DateTime.Now.Month);
            CopiesCountTextBox.Text = "1";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Базовая валидация
            if (string.IsNullOrWhiteSpace(TitleTextTextBox.Text))
            {
                MessageBox.Show("Введите название документа", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TitlePageData = new TitlePageData
            {
                CompositionDay = DayTextBox.Text,
                CompositionMonth = MonthTextBox.Text,
                CopiesCount = CopiesCountTextBox.Text,
                ChiefName = ChiefNameTextBox.Text,
                Rank = RankTextBox.Text,
                Position = PositionTextBox.Text,
                CompilerPosition = CompilerPositionTextBox.Text,
                CompilerName = CompilerNameTextBox.Text,
                TitleText = TitleTextTextBox.Text
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Класс данных титульного листа
    public class TitlePageData
    {
        public string CompositionDay { get; set; } = "";
        public string CompositionMonth { get; set; } = "";
        public string CopiesCount { get; set; } = "";
        public string ChiefName { get; set; } = "";
        public string Rank { get; set; } = "";
        public string Position { get; set; } = "";
        public string CompilerPosition { get; set; } = "";
        public string CompilerName { get; set; } = "";
        public string TitleText { get; set; } = "";
    }

}