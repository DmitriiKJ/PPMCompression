using Microsoft.Win32;
using PPM_Compression.Collections;
using System.IO;
using System.Text;
using System.Windows;

namespace PPM_Compression
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void EncodeTextButton_Click(object sender, RoutedEventArgs e)
        {
            var text = Encoding.UTF8.GetBytes(JustTextBox.Text).ToList();
            var compressed = Compression.PPM.PPMCompression(text);

            var dialog = new SaveFileDialog();
            dialog.Title = "Save file";
            dialog.Filter = "DPPM file (*.dppm)|*.dppm";
            dialog.DefaultExt = "dppm";
            dialog.AddExtension = true;

            if (dialog.ShowDialog() == true)
            {
                byte[] data = CompressedPPM.ToBinary(compressed);
                File.WriteAllBytes(dialog.FileName, data);
            }
        }

        private void DecodeTextButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load file";
            dialog.Filter = "DPPM file (*.dppm)|*.dppm";

            if (dialog.ShowDialog() == true)
            {
                byte[] raw = File.ReadAllBytes(dialog.FileName);
                var compressed = CompressedPPM.FromBinary(raw);
                var decompressed = Compression.PPM.PPMRestore(compressed);
                DecodedTextBox.Text = Encoding.UTF8.GetString(decompressed.ToArray());
            }
        }
    }
}