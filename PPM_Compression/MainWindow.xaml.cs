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
        private byte[] FileBytes = new byte[0];
        private string FileName = string.Empty;

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
            dialog.Filter = "PPM file (*.ppm)|*.ppm";

            if (dialog.ShowDialog() == true)
            {
                byte[] raw = File.ReadAllBytes(dialog.FileName);
                var compressed = CompressedPPM.FromBinary(raw);
                var decompressed = Compression.PPM.PPMRestore(compressed, null);
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
                FileName = Path.GetFileName(dialog.FileName);
                EncodeFileButton.IsEnabled = true;
            }
        }

        private async void EncodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            await CompressAndSave(FileBytes.ToList());
        }

        private async Task CompressAndSave(List<byte> data)
        {
            SelectFileButton.IsEnabled = false;
            EncodeFileButton.IsEnabled = false;

            FileEncodingProgressBar.Minimum = 0;
            FileEncodingProgressBar.Maximum = data.Count;
            FileEncodingProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<int>(value =>
            {
                FileEncodingProgressBar.Value = value;
            });

            var compressed = await Task.Run(() =>
                Compression.PPM.PPMCompression(data, progress)
            );

            compressed.FileName = FileName;

            FileEncodingProgressBar.Visibility = Visibility.Hidden;

            var dialog = new SaveFileDialog();
            dialog.Title = "Save file";
            dialog.Filter = "PPM file (*.ppm)|*.ppm";
            dialog.DefaultExt = "ppm";
            dialog.AddExtension = true;

            SelectFileButton.IsEnabled = true;
            EncodeFileButton.IsEnabled = true;

            if (dialog.ShowDialog() == true)
            {
                byte[] compressedData = CompressedPPM.ToBinary(compressed);
                File.WriteAllBytes(dialog.FileName, compressedData);
            }
        }

        private async void DecodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            SelectEncodeFileButton.IsEnabled = false;
            DecodeFileButton.IsEnabled = false;

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
            var safeName = string.Concat(
                FileName
                .Trim()
                .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))
            );
            dialog.FileName = safeName;
            dialog.Filter = "Все файлы (*.*)|*.*";

            SelectEncodeFileButton.IsEnabled = true;
            DecodeFileButton.IsEnabled = true;

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(dialog.FileName, decompressed.ToArray());
            }
        }

        private void SelectEncodeFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load file";
            dialog.Filter = "PPM file (*.ppm)|*.ppm";

            if (dialog.ShowDialog() == true)
            {
                FileBytes = File.ReadAllBytes(dialog.FileName);
                EncodedFileNameTextBlock.Text = dialog.FileName;
                DecodeFileButton.IsEnabled = true;
            }
        }
    }
}