using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAppRTSP2Images
{
    public delegate Task ShowLog(string message);
    public delegate Task SetWidth(int  width);
    public delegate Task SetHeight(int height);
    public delegate Task UploadFile(string filePath);

    public class RTSP2Images
    {
        private string RTSPUrl;
        private int OutputWidth;
        private int OutputHeight;
        private string OutputFormat;
        private int CaptureFPS;
        private string OutputPath;

        private SetWidth setWidth;
        private SetHeight setHeight;
        private UploadFile uploadFile;

        private VideoCapture videoCapture;
        public RTSP2Images(string url, string format, int fps, SetWidth widthSet, SetHeight heightSet, string outputPath, UploadFile uploadFile)
        {
            RTSPUrl = url;
            OutputFormat = format;
            CaptureFPS = fps;

            setWidth = widthSet;
            setHeight = heightSet;
            this.uploadFile = uploadFile;

            videoCapture = new VideoCapture(RTSPUrl);

            OutputPath = outputPath;
            this.uploadFile = uploadFile;
        }

        private CancellationTokenSource cts;

        public async Task StartAsync(ShowLog showLog)
        {
            var tmp = new Mat(1024, 768, MatType.CV_8UC3);
            //if (!videoCapture.Open(RTSPUrl))
            if (!videoCapture.IsOpened())
            {
                await showLog($"RTSP stream has not been opened.");
                return;
            }
            if (!videoCapture.Grab())
            {
                await showLog($"RTSP stream has not been grabed.");
                return;
            }
            await showLog($"Starting at {DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss")}");

            //OutputHeight = (videoCapture.FrameHeight * OutputWidth) / videoCapture.FrameWidth;
            //videoCapture.FrameWidth = OutputWidth;
            //videoCapture.FrameHeight = OutputHeight;
            setWidth(videoCapture.FrameWidth);
            setHeight(videoCapture.FrameHeight);

            cts = new CancellationTokenSource();

            while (true)
            {
                var now = DateTime.Now;
                string format = "";
                int duration = 10;
                lock (this)
                {
                    duration = CaptureFPS;
                    format = OutputFormat;
                }
                var output = new Mat(videoCapture.FrameHeight, videoCapture.FrameWidth, MatType.CV_8UC3);
                
                if (videoCapture.Read(output)) // Read seems not to be used
                {
                    string filename = $"img-{DateTime.Now.ToString("yyyyMMddHHmmss")}.{format}";
                    string filepath = Path.Join(OutputPath, filename);
                    output.SaveImage(filepath);
                    await showLog($"Saved - {filename}");
                    await uploadFile(filepath);
                }
                if (cts.Token.IsCancellationRequested)
                {
                    cts.Token.ThrowIfCancellationRequested();
                }
                var nextTime = now.Add(TimeSpan.FromSeconds(duration));
                while (DateTime.Now < nextTime)
                {
                    videoCapture.Grab();
                    await Task.Delay(TimeSpan.FromMilliseconds(1000/videoCapture.Fps), cts.Token);
                }
            }
        }

        public async Task StopAsync(ShowLog showLog)
        {
            await showLog($"Stopped at {DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss")}");
            cts.Cancel();
            cts.Dispose();
        }

        public async Task UpdateProperties(IDictionary<string, object> properties)
        {
            if (properties.ContainsKey("duration") && properties.ContainsKey("format"))
            {
                lock (this)
                {
                    CaptureFPS = int.Parse((string)properties["duration"]);
                    OutputFormat = (string)properties["format"];
                }
            }
        }
    }
}
