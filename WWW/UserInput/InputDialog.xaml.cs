using System.Windows;

namespace ZoneHydrantEditor
{
    public partial class InputDialog : Window
    {
        public string Answer { get; private set; }
        public InputDialog(string question, string title = "Ввод", string defaultAnswer = "")
        {
            InitializeComponent();

            Title = title;
            QuestionText.Text = question;
            InputTextBox.Text = defaultAnswer;

            Loaded += (s, e) => InputTextBox.Focus();
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Answer = InputTextBox.Text;
            DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}