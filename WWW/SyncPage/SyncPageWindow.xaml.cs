using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TestDbApp.Models;
using ZoneHydrantEditor.Helpers;

namespace ZoneHydrantEditor.SyncPage
{
    public partial class SyncPageWindow : Window
    {
        private readonly EwsMapDataService _ewsService;
        private readonly BackupService _backupService;
        private const int PageSize = 50;
        private int _historyOffset;
        private int _checkOffset;
        private bool _isHistoryLoading;
        private bool _isCheckLoading;
        private bool _historyHasMore = true;
        private bool _checkHasMore = true;
        private readonly DispatcherTimer _historyDebounce;
        private readonly DispatcherTimer _checkDebounce;

        public SyncPageWindow(EwsMapDataService ewsService = null, BackupService backupService = null)
        {
            InitializeComponent();

            _ewsService = ewsService ?? new EwsMapDataService();
            _backupService = backupService ?? new BackupService(
                new DatabaseService(), _ewsService, "zones0815.db");

            _historyDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _historyDebounce.Tick += (s, e) => { _historyDebounce.Stop(); LoadHistoryData(reset: true); };

            _checkDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _checkDebounce.Tick += (s, e) => { _checkDebounce.Stop(); LoadCheckData(reset: true); };

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

                var items = _ewsService.GetCopyEwssPaged(_historyOffset, PageSize, search, dateFrom, dateTo);

                if (items.Count < PageSize)
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

                var items = _ewsService.GetEwssChecksPaged(_checkOffset, PageSize, search, dateFrom, dateTo);

                if (items.Count < PageSize)
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

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await _backupService.CreateFullBackupAsync(this);
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await _backupService.RestoreFromBackupAsync(this);
        }
    }
}
