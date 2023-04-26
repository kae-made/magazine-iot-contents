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

        private VideoCapture videoCapture;
        public RTSP2Images(string url, string format, int fps, SetWidth widthSet, SetHeight heightSet, string outputPath)
        {
            RTSPUrl = url;
            OutputFormat = format;
            CaptureFPS = fps;

            setWidth = widthSet;
            setHeight = heightSet;

            videoCapture = new VideoCapture(RTSPUrl);
            
            OutputPath = outputPath;
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
                var output = new Mat(videoCapture.FrameHeight, videoCapture.FrameWidth, MatType.CV_8UC3);
                if (videoCapture.Read(output))
                {
                    string filename = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}.{OutputFormat}";
                    string filepath = Path.Join(OutputPath, filename);
                    output.SaveImage(filepath);
                    await showLog($"Saved - {filename}");
                }
                if (cts.Token.IsCancellationRequested)
                {
                    cts.Token.ThrowIfCancellationRequested();
                }
                await Task.Delay(TimeSpan.FromSeconds(CaptureFPS),cts.Token);
            }
        }

        public async Task StopAsync(ShowLog showLog)
        {
            await showLog($"Stopped at {DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss")}");
            cts.Cancel();
            cts.Dispose();
        }
    }
}
