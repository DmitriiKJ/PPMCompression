using Microsoft.Win32;
using PPM_Compression.Collections;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace PPM_Compression
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private byte[] FileBytes = new byte[0];

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void EncodeTextButton_Click(object sender, RoutedEventArgs e)
        {
            var text = Encoding.UTF8.GetBytes(JustTextBox.Text).ToList();
            await CompressAndSave(text);
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

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load file";

            if (dialog.ShowDialog() == true)
            {
                FileBytes = File.ReadAllBytes(dialog.FileName);
                FileNameTextBlock.Text = dialog.FileName;
            }
        }

        private async void EncodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            await CompressAndSave(FileBytes.ToList());
        }

        private async Task CompressAndSave(List<byte> data)
        {
            FileEncodingProgressBar.Minimum = 0;
            FileEncodingProgressBar.Maximum = FileBytes.Length;
            FileEncodingProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<int>(value =>
            {
                FileEncodingProgressBar.Value = value;
            });

            var compressed = await Task.Run(() =>
                Compression.PPM.PPMCompression(data, progress)
            );

            FileEncodingProgressBar.Visibility = Visibility.Hidden;

            var dialog = new SaveFileDialog();
            dialog.Title = "Save file";
            dialog.Filter = "DPPM file (*.dppm)|*.dppm";
            dialog.DefaultExt = "dppm";
            dialog.AddExtension = true;

            if (dialog.ShowDialog() == true)
            {
                byte[] compressedData = CompressedPPM.ToBinary(compressed);
                File.WriteAllBytes(dialog.FileName, compressedData);
            }
        }

        private async void DecodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            var compressed = CompressedPPM.FromBinary(FileBytes);

            FileDecodingProgressBar.Minimum = 0;
            FileDecodingProgressBar.Maximum = compressed.BytesLen;
            FileDecodingProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<int>(value =>
            {
                FileDecodingProgressBar.Value = value;
            });

            var decompressed = await Task.Run(() =>
                Compression.PPM.PPMRestore(compressed, progress)
            );

            FileDecodingProgressBar.Visibility = Visibility.Hidden;

            var dialog = new SaveFileDialog();
            dialog.Title = "Save file";
            dialog.Filter = "mp4 file (*.mp4)|*.mp4";
            dialog.DefaultExt = "mp4";
            dialog.AddExtension = true;

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(dialog.FileName, decompressed.ToArray());
            }
        }

        private void SelectEncodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load file";
            dialog.Filter = "DPPM file (*.dppm)|*.dppm";

            if (dialog.ShowDialog() == true)
            {
                FileBytes = File.ReadAllBytes(dialog.FileName);
                EncodedFileNameTextBlock.Text = dialog.FileName;
            }
        }
    }
}