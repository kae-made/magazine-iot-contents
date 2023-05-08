using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfAppRTSP2Images
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

        RTSP2Images rtsp2images = null;
        IoTDeviceFileToBlob iotDeviceFileToBlob = null;
        FileCompresser fileCompresser = null;
        long uploadKBSizeThreshold = 256;

        private async void buttonControl_Click(object sender, RoutedEventArgs e)
        {
            if (buttonControl.Content.ToString() == "Start")
            {
                if (rtsp2images != null)
                {
                    await rtsp2images.StopAsync(ShowLog);
                }
                try
                {
                    rtsp2images = new RTSP2Images(tbURL.Text, ((ComboBoxItem)cbFormat.SelectedItem).Content.ToString(), int.Parse(tbFPS.Text),
                       async (w) => { tbWidth.Text = $"{w}"; }, async (h) => { tbHeight.Text = $"{h}"; }, tbFolder.Text,
                       FileUpload);
                    if (cbUpload.IsChecked == true)
                    {
                        iotDeviceFileToBlob = new IoTDeviceFileToBlob(tbCS.Text, UpdatedIoTDeviceProperties);
                        await iotDeviceFileToBlob.StartAsync();
                    }
                    await rtsp2images.StartAsync(ShowLog);
                    buttonControl.Content = "Stop";
                }
                catch (Exception ex) { ShowLog(ex.Message); }
            }
            else
            {
                if (rtsp2images != null)
                {
                    await rtsp2images.StopAsync(ShowLog);
                    rtsp2images = null;
                }
            }
        }

        private async Task UpdatedIoTDeviceProperties(IDictionary<string, object> properties)
        {
            if (rtsp2images != null)
            {
                rtsp2images.UpdateProperties(properties);
            }
            if (properties.ContainsKey(IoTDeviceFileToBlob.DPKeyUploadSizeThreshold))
            {
                long threshold = long.Parse((string)properties[IoTDeviceFileToBlob.DPKeyUploadSizeThreshold]);
                this.Dispatcher.Invoke(() =>
                {
                    tbZipSize.Text = $"{threshold}";
                });
                lock (this)
                {
                    uploadKBSizeThreshold = threshold;
                }
            }
        }

        private async Task FileUpload(string filePath)
        {
            if (cbUpload.IsChecked == true && iotDeviceFileToBlob != null)
            {
                string blobName = "";
                bool shouldUpload = true;
                if (cbCompress.IsChecked == true)
                {
                    string zipFilePath = System.IO.Path.Join(tbFolder.Text, "tmp.zip");
                    if (fileCompresser == null)
                    {
                        fileCompresser = new FileCompresser() { ZipFilePath = zipFilePath };
                        fileCompresser.CreateZipFile();
                    }
                    fileCompresser.AddFile(filePath);
                    if (cbDelete.IsChecked == true)
                    {
                        File.Delete(filePath);
                        await ShowLog($"Deleted {filePath}");
                    }
                    long currentUploadSize = 0;
                    lock (this)
                    {
                        currentUploadSize = 1024 * uploadKBSizeThreshold;
                    }
                    if (fileCompresser.FileSize > currentUploadSize)
                    {
                        filePath = zipFilePath;
                        blobName = $"cmp-{DateTime.Now.ToString("yyyyMMddHHmmss")}.zip";
                    }
                    else
                    {
                        shouldUpload = false;
                    }

                }
                if (shouldUpload)
                {
                    await iotDeviceFileToBlob.UploadFile(filePath, blobName);
                    await ShowLog($"Updated {filePath}");
                    if (cbDelete.IsChecked == true)
                    {
                        File.Delete(filePath);
                        await ShowLog($"Deleted {filePath}");
                    }
                }
            }
        }

        private async Task ShowLog(string message)
        {
            var sb = new StringBuilder(tbLog.Text);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine(message);
            }
            tbLog.Text = sb.ToString();
        }

        private void cbCompress_Checked(object sender, RoutedEventArgs e)
        {
            if (cbCompress.IsChecked == true)
            {
                tbZipSize.IsEnabled = true;
                uploadKBSizeThreshold = long.Parse(tbZipSize.Text);
            }
            else
            {
                tbZipSize.IsEnabled = false;
            }
        }
    }
}