using System.Windows;
using System.Windows.Controls;

namespace ZoneHydrantEditor
{
    public partial class ExportProgressWindow : Window
    {
        public ExportProgressWindow()
        {
            InitializeComponent();
        }

        public void SetProgressText(string text)
        {
            Dispatcher.Invoke(() => ProgressText.Text = text);
        }

        public void SetStatusText(string text)
        {
            Dispatcher.Invoke(() => StatusText.Text = text);
        }

        public void SetProgressValue(double value)
        {
            Dispatcher.Invoke(() => ProgressBar.Value = value);
        }

        public void SetMaxProgress(double max)
        {
            Dispatcher.Invoke(() => ProgressBar.Maximum = max);
        }
    }
}