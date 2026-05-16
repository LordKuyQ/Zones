using System;
using System.Windows;
using TestDbApp.Models;

namespace ZoneHydrantEditor
{
    public partial class UserCreateDialog : Window
    {
        public Fio NewUser { get; private set; }

        public UserCreateDialog()
        {
            InitializeComponent();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(FamilyTextBox.Text))
            {
                MessageBox.Show("Введите фамилию", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Введите имя", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewUser = new Fio
            {
                FioId = Guid.NewGuid().ToString("N"),
                FioFamily = FamilyTextBox.Text.Trim(),
                FioName = NameTextBox.Text.Trim(),
                FioSurname = string.IsNullOrWhiteSpace(SurnameTextBox.Text) ? null : SurnameTextBox.Text.Trim(),
                FioCityCod = string.IsNullOrWhiteSpace(CityCodeTextBox.Text) ? null : CityCodeTextBox.Text.Trim(),
                FioUnitCod = string.IsNullOrWhiteSpace(UnitCodeTextBox.Text) ? null : UnitCodeTextBox.Text.Trim(),
                FioDuty = string.IsNullOrWhiteSpace(DutyTextBox.Text) ? null : DutyTextBox.Text.Trim()
            };

            DialogResult = true;
            Close();
        }
    }
}