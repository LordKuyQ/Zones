using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TestDbApp.Models;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor
{
    public partial class UserSelectDialog : Window
    {
        private readonly EwsMapDataService _ewsService;
        public Fio SelectedUser { get; private set; }
        public bool IsNewUserCreated { get; private set; }

        public UserSelectDialog()
        {
            InitializeComponent();
            _ewsService = new EwsMapDataService();
            LoadUsers();
        }

        private void LoadUsers()
        {
            var users = _ewsService.GetAllFios();
            var userList = new List<FioDisplay>();

            foreach (var user in users)
            {
                userList.Add(new FioDisplay
                {
                    FioId = user.FioId,
                    DisplayName = GetUserDisplayName(user)
                });
            }

            UserComboBox.ItemsSource = userList;

            if (userList.Any())
                UserComboBox.SelectedIndex = 0;
        }

        private string GetUserDisplayName(Fio user)
        {
            string family = user.FioFamily ?? "";
            string name = user.FioName ?? "";
            string surname = user.FioSurname ?? "";
            string duty = user.FioDuty ?? "";

            string fullName = $"{family} {name} {surname}".Trim();
            if (!string.IsNullOrEmpty(duty))
                fullName += $" ({duty})";

            return string.IsNullOrWhiteSpace(fullName) ? "Без имени" : fullName;
        }

        private void CreateUserButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UserCreateDialog();
            if (dialog.ShowDialog() == true && dialog.NewUser != null)
            {
                _ewsService.InsertFio(dialog.NewUser);
                LoadUsers();
                IsNewUserCreated = true;

                var userList = UserComboBox.ItemsSource as List<FioDisplay>;
                if (userList != null)
                {
                    var created = userList.FirstOrDefault(u => u.FioId == dialog.NewUser.FioId);
                    if (created != null)
                        UserComboBox.SelectedItem = created;
                }

                MessageBox.Show("Пользователь успешно создан!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SelectUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = UserComboBox.SelectedItem as FioDisplay;
            if (selected != null)
            {
                SelectedUser = _ewsService.GetFioById(selected.FioId);
                DialogResult = true;
                Close();
            }
        }

        public class FioDisplay
        {
            public string FioId { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
