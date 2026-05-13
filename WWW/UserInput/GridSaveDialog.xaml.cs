using System.IO;
using System.Windows;

namespace ZoneHydrantEditor
{
    public partial class GridSaveDialog : Window
    {
        private string _fileName;
        private readonly SaveMode _mode;
        private readonly ZoneInfo _zoneInfo;
        private readonly int _hydrantCount;
        private readonly string _zoneName;
        private readonly int _currentPage;
        private readonly int _totalPages;
        private readonly List<GridCellData> _cells;
        private readonly string _baseFileName;

        public string FileName => _fileName;
        public bool NeedsRetry { get; private set; }
        public bool IsCancelled { get; private set; }

        public enum SaveMode
        {
            ZoneGrid,
            ZoneOverview
        }

        public GridSaveDialog(string zoneName, int currentPage, int totalPages, List<GridCellData> cells, string baseFileName)
        {
            InitializeComponent();

            _mode = SaveMode.ZoneGrid;
            _zoneName = zoneName;
            _currentPage = currentPage;
            _totalPages = totalPages;
            _cells = cells;
            _zoneInfo = null;
            _baseFileName = baseFileName;

            NeedsRetry = false;
            IsCancelled = false;

            string directory = Path.GetDirectoryName(baseFileName);
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
            string extension = Path.GetExtension(baseFileName);

            if (totalPages > 1)
            {
                _fileName = Path.Combine(directory, $"{filenameWithoutExt}_стр{currentPage}{extension}");
            }
            else
            {
                _fileName = baseFileName;
            }

            if (totalPages == 1)
            {
                NextButton.Content = "Сохранить";
            }
        }

        public GridSaveDialog(ZoneInfo zoneInfo, int hydrantCount, string baseFileName)
        {
            InitializeComponent();

            _mode = SaveMode.ZoneOverview;
            _zoneInfo = zoneInfo;
            _zoneName = zoneInfo.ZoneName;
            _hydrantCount = hydrantCount;
            _fileName = baseFileName;
            _baseFileName = baseFileName;
            _cells = null;
            _currentPage = 1;
            _totalPages = 1;
            NeedsRetry = false;
            IsCancelled = false;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_fileName))
            {
                var result = MessageBox.Show($"Файл '{Path.GetFileName(_fileName)}' уже существует.\n\nЗаменить существующий файл?", "Файл существует", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
                else if (result == MessageBoxResult.No)
                {
                    NeedsRetry = true;
                    IsCancelled = false;
                    DialogResult = true;
                    Close();
                    return;
                }
            }

            NeedsRetry = false;
            IsCancelled = false;
            DialogResult = true;
            Close();
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            NeedsRetry = true;
            IsCancelled = false;
            DialogResult = true;
            Close();
        }
    }
}
public class GridCellData
{
    public int HydrantId { get; set; }
    public string HydrantNumber { get; set; }
    public string HydrantTruba { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; }
    public string BreakReason { get; set; }
    public string EwsId { get; set; } = "";
    public string EwsPriviazka { get; set; } = "";
    public string EwsPriviazkaGeoX { get; set; } = "";
    public string EwsPriviazkaGeoY { get; set; } = "";
}