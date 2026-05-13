using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ZoneHydrantEditor.Models;

namespace ZoneHydrantEditor.Helpers
{
    /// Сервис для создания и восстановления полных резервных копий всех данных
    public class BackupService(DatabaseService dbService, string zonesDbFile, string hydrantsDbFile)
    {
        private readonly DatabaseService _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        private readonly string _zonesDbFile = zonesDbFile;
        private readonly string _hydrantsDbFile = hydrantsDbFile;
        public async Task<bool> CreateFullBackupAsync(Window owner = null)
        {
            try
            {
                SaveFileDialog dialog = null;
                string description = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    dialog = new SaveFileDialog
                    {
                        Title = "Сохранить полную резервную копию",
                        Filter = "Резервная копия HydrantEditor (*.hbackup)|*.hbackup|Все файлы (*.*)|*.*",
                        DefaultExt = "hbackup",
                        FileName = $"HydrantBackup_{DateTime.Now:yyyy-MM-dd_HH-mm}.hbackup"
                    };
                    if (dialog.ShowDialog(owner) == true)
                    {
                        var descriptionDialog = new InputDialog("Введите описание резервной копии (необязательно):", "Описание бэкапа", $"Автоматический бэкап от {DateTime.Now:dd.MM.yyyy HH:mm}");
                        if (descriptionDialog.ShowDialog() == true)
                        {
                            description = descriptionDialog.Answer;
                        }
                    }
                });

                if (dialog == null || dialog.FileName == null || description == null) return false;
                BackupPackage backup = null;
                await Task.Run(() =>
                {
                    backup = new BackupPackage
                    {
                        BackupDate = DateTime.Now,
                        BackupDescription = description
                    };
                    var zones = _dbService.GetAllZones();
                    foreach (var zone in zones)
                    {
                        var zoneData = new ZoneBackupData
                        {
                            Id = zone.Id,
                            Name = zone.Name
                        };
                        var points = _dbService.GetZonePoints(zone.Id);
                        foreach (var point in points)
                        {
                            zoneData.Points.Add(new PointLatLngBackup(point.Lat, point.Lng));
                        }
                        backup.Zones.Add(zoneData);
                    }

                    backup.Hydrants = _dbService.GetAllMarkers();
                    backup.Bindings = _dbService.GetAllBindings();
                    JsonSerializerOptions jsonSerializerOptions = new()
                    {
                        WriteIndented = true
                    };
                    var options = jsonSerializerOptions;
                    string json = JsonSerializer.Serialize(backup, options);
                    File.WriteAllText(dialog.FileName, json);
                });
                // Показываем результат в UI потоке
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowBackupResult(backup, dialog.FileName);
                });
                return true;
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при создании резервной копии:\n\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return false;
            }
        }
        public async Task<bool> RestoreFromBackupAsync(Window owner = null)
        {
            try
            {
                OpenFileDialog dialog = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    dialog = new OpenFileDialog
                    {
                        Title = "Выберите файл резервной копии",
                        Filter = "Резервная копия HydrantEditor (*.hbackup)|*.hbackup|Все файлы (*.*)|*.*",
                        DefaultExt = "hbackup"
                    };
                    dialog.ShowDialog(owner);
                });

                if (dialog == null || dialog.FileName == null) return false;
                // Предупреждение о замене данных - выполняется в UI потоке
                bool confirmed = false;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    confirmed = ConfirmRestore();
                });

                if (!confirmed) return false;

                BackupPackage backup = null;
                // Выполняем восстановление в фоновом потоке
                await Task.Run(() =>
                {
                    // Чтение файла
                    string json = File.ReadAllText(dialog.FileName);
                    backup = JsonSerializer.Deserialize<BackupPackage>(json) ?? throw new Exception("Не удалось прочитать файл резервной копии");
                    ClearAllData();
                    RestoreZones(backup.Zones);
                    RestoreHydrants(backup.Hydrants);
                    RestoreBindings(backup.Bindings);
                });
                _dbService.Cache.ClearAllCache();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowRestoreResult(backup, dialog.FileName);
                });
                return true;
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"❌ Ошибка при восстановлении из резервной копии:\n\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return false;
            }
        }
        #region Приватные вспомогательные методы

        private static bool ConfirmRestore()
        {
            var result = MessageBox.Show(
                "ВНИМАНИЕ! Восстановление из резервной копии ПОЛНОСТЬЮ ЗАМЕНИТ:\n" + "• Все зоны и их границы\n" + "• Все гидранты и их информацию\n" +
                "• Все привязки гидрантов\n" + "ТЕКУЩИЕ ДАННЫЕ БУДУТ УТЕРЯНЫ!\n\n" + "Вы уверены, что хотите продолжить?", "Подтверждение восстановления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        private void ClearAllData()
        {
            using (var conn = new SQLiteConnection($"Data Source={_zonesDbFile}"))
            {
                conn.Open();
                new SQLiteCommand("DELETE FROM ZonePoints", conn).ExecuteNonQuery();
                new SQLiteCommand("DELETE FROM Zones", conn).ExecuteNonQuery();
                new SQLiteCommand("DELETE FROM ZoneBackups", conn).ExecuteNonQuery();
                new SQLiteCommand("DELETE FROM ZoneBackupPoints", conn).ExecuteNonQuery();

                conn.Close();
            }
            using (var conn = new SQLiteConnection($"Data Source={_hydrantsDbFile}"))
            {
                conn.Open();
                new SQLiteCommand("DELETE FROM HydrantBindings", conn).ExecuteNonQuery();
                new SQLiteCommand("DELETE FROM Markers", conn).ExecuteNonQuery();
                conn.Close();
            }
        }
        private void RestoreZones(List<ZoneBackupData> zones)
        {
            using var conn = new SQLiteConnection($"Data Source={_zonesDbFile}");
            conn.Open();

            foreach (var zoneData in zones)
            {
                // Вставляем зону с сохранением оригинального ID
                var insertZoneCmd = new SQLiteCommand(
                    "INSERT INTO Zones (Id, Name) VALUES (@id, @name)", conn);
                insertZoneCmd.Parameters.AddWithValue("@id", zoneData.Id);
                insertZoneCmd.Parameters.AddWithValue("@name", zoneData.Name);
                insertZoneCmd.ExecuteNonQuery();

                // Вставляем точки зоны
                for (int i = 0; i < zoneData.Points.Count; i++)
                {
                    var point = zoneData.Points[i];
                    var insertPointCmd = new SQLiteCommand(
                        "INSERT INTO ZonePoints (ZoneId, OrderIndex, Latitude, Longitude) VALUES (@zoneId, @order, @lat, @lng)",
                        conn);
                    insertPointCmd.Parameters.AddWithValue("@zoneId", zoneData.Id);
                    insertPointCmd.Parameters.AddWithValue("@order", i);
                    insertPointCmd.Parameters.AddWithValue("@lat", point.Lat);
                    insertPointCmd.Parameters.AddWithValue("@lng", point.Lng);
                    insertPointCmd.ExecuteNonQuery();
                }
            }

            conn.Close();
        }
        private void RestoreHydrants(List<MarkerInfo> hydrants)
        {
            using var conn = new SQLiteConnection($"Data Source={_hydrantsDbFile}");
            conn.Open();

            foreach (var hydrant in hydrants)
            {
                var insertHydrantCmd = new SQLiteCommand(
                    @"INSERT INTO Markers (Id, Latitude, Longitude, GidrantNumber, GidrantTruba, 
                          GidrantAdres, CompanyName, Status, BreakReason, ZoneId)
                          VALUES (@id, @lat, @lng, @num, @truba, @adres, @comp, @status, @break, @zoneId)",
                    conn);
                insertHydrantCmd.Parameters.AddWithValue("@id", hydrant.Id);
                insertHydrantCmd.Parameters.AddWithValue("@lat", hydrant.Latitude);
                insertHydrantCmd.Parameters.AddWithValue("@lng", hydrant.Longitude);
                insertHydrantCmd.Parameters.AddWithValue("@num", hydrant.GidrantNumber ?? "");
                insertHydrantCmd.Parameters.AddWithValue("@truba", hydrant.GidrantTruba ?? "");
                insertHydrantCmd.Parameters.AddWithValue("@adres", hydrant.GidrantAdres ?? "");
                insertHydrantCmd.Parameters.AddWithValue("@comp", hydrant.CompanyName ?? "");
                insertHydrantCmd.Parameters.AddWithValue("@status", hydrant.Status ?? "Непроверенный");
                insertHydrantCmd.Parameters.AddWithValue("@break", hydrant.BreakReason ?? "");
                insertHydrantCmd.Parameters.AddWithValue("@zoneId", hydrant.ZoneId.HasValue ? hydrant.ZoneId.Value : DBNull.Value);
                insertHydrantCmd.ExecuteNonQuery();
            }

            conn.Close();
        }
        private void RestoreBindings(List<BindingInfo> bindings)
        {
            using var conn = new SQLiteConnection($"Data Source={_hydrantsDbFile}");
            conn.Open();

            foreach (var binding in bindings)
            {
                var insertBindingCmd = new SQLiteCommand(
                    @"INSERT INTO HydrantBindings (Id, Latitude, Longitude, DistanceToHydrantX, DistanceToHydrantY, HydrantId)
                          VALUES (@id, @lat, @lng, @distX, @distY, @hydrantId)",
                    conn);
                insertBindingCmd.Parameters.AddWithValue("@id", binding.Id);
                insertBindingCmd.Parameters.AddWithValue("@lat", binding.Latitude);
                insertBindingCmd.Parameters.AddWithValue("@lng", binding.Longitude);
                insertBindingCmd.Parameters.AddWithValue("@distX", binding.DistanceX);
                insertBindingCmd.Parameters.AddWithValue("@distY", binding.DistanceY);
                insertBindingCmd.Parameters.AddWithValue("@hydrantId", binding.HydrantId);
                insertBindingCmd.ExecuteNonQuery();
            }

            conn.Close();
        }

        private static void ShowBackupResult(BackupPackage backup, string fileName) => MessageBox.Show($" Полная резервная копия успешно создана!\n\n" + $"Файл: {Path.GetFileName(fileName)}\n" + $"📊 Статистика:\n" + $" Зон: {backup.Zones.Count}\n" +
                $"Гидрантов: {backup.Hydrants.Count}\n" + $" Привязок: {backup.Bindings.Count}\n" + $"Описание: {backup.BackupDescription}",
                "Резервное копирование", MessageBoxButton.OK, MessageBoxImage.Information);

        private static void ShowRestoreResult(BackupPackage backup, string fileName)
        {
            MessageBox.Show($"Восстановление из резервной копии успешно завершено!\n\n" + $"Файл: {Path.GetFileName(fileName)}\n" + $"Описание: {backup.BackupDescription}\n\n" +
                $"Восстановлено:\n" + $" Зон: {backup.Zones.Count}\n" + $"Гидрантов: {backup.Hydrants.Count}\n" +
                $" Привязок: {backup.Bindings.Count}", "Восстановление", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion
    }
}