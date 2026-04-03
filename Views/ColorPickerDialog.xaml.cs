using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VideoCreatorWPF.Views
{
    public partial class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; set; } = Colors.White;

        public ColorPickerDialog()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize with current color
            Loaded += (s, e) =>
            {
                ColorPreview.Background = new SolidColorBrush(SelectedColor);
                RBox.Text = SelectedColor.R.ToString();
                GBox.Text = SelectedColor.G.ToString();
                BBox.Text = SelectedColor.B.ToString();
                HexBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

                RSlider.Value = SelectedColor.R;
                GSlider.Value = SelectedColor.G;
                BSlider.Value = SelectedColor.B;
            };
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                byte r = (byte)RSlider.Value;
                byte g = (byte)GSlider.Value;
                byte b = (byte)BSlider.Value;

                switch (slider.Name)
                {
                    case nameof(RSlider):
                        RBox.Text = r.ToString();
                        break;
                    case nameof(GSlider):
                        GBox.Text = g.ToString();
                        break;
                    case nameof(BSlider):
                        BBox.Text = b.ToString();
                        break;
                }

                SelectedColor = Color.FromArgb(255, r, g, b);
                ColorPreview.Background = new SolidColorBrush(SelectedColor);
                HexBox.Text = $"#{r:X2}{g:X2}{b:X2}";
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box)
            {
                byte r = SelectedColor.R;
                byte g = SelectedColor.G;
                byte b = SelectedColor.B;

                switch (box.Name)
                {
                    case nameof(RBox):
                        if (byte.TryParse(box.Text, out var rv))
                        {
                            RSlider.Value = rv;
                            r = rv;
                        }
                        break;
                    case nameof(GBox):
                        if (byte.TryParse(box.Text, out var gv))
                        {
                            GSlider.Value = gv;
                            g = gv;
                        }
                        break;
                    case nameof(BBox):
                        if (byte.TryParse(box.Text, out var bv))
                        {
                            BSlider.Value = bv;
                            b = bv;
                        }
                        break;
                    case nameof(HexBox):
                        var hex = box.Text.TrimStart('#');
                        if (hex.Length == 6)
                        {
                            try
                            {
                                r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                                g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                                b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                                RSlider.Value = r;
                                GSlider.Value = g;
                                BSlider.Value = b;
                            }
                            catch { }
                        }
                        break;
                }

                SelectedColor = Color.FromArgb(255, r, g, b);
                ColorPreview.Background = new SolidColorBrush(SelectedColor);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
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
