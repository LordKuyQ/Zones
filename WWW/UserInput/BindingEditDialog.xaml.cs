using System.Windows;

namespace ZoneHydrantEditor.UserInput
{
    public partial class BindingEditDialog : Window
    {
        public string BindingComment { get; private set; }
        public string BindingLat { get; private set; }
        public string BindingLng { get; private set; }
        public string BindingLeft { get; private set; }
        public string BindingRight { get; private set; }
        public string BindingStraight { get; private set; }

        public BindingEditDialog(double lat, double lng,
            string comment = "", string left = "", string right = "", string straight = "")
        {
            InitializeComponent();

            LatitudeText.Text = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            LongitudeText.Text = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);

            CommentTextBox.Text = comment;
            LeftText.Text = left;
            RightText.Text = right;
            StraightText.Text = straight;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            BindingComment = CommentTextBox.Text?.Trim() ?? "";
            BindingLat = LatitudeText.Text?.Trim() ?? "";
            BindingLng = LongitudeText.Text?.Trim() ?? "";
            BindingLeft = LeftText.Text?.Trim() ?? "";
            BindingRight = RightText.Text?.Trim() ?? "";
            BindingStraight = StraightText.Text?.Trim() ?? "";

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
