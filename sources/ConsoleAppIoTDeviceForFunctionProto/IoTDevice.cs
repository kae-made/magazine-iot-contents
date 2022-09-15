using Kae.Utility.Logging;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppIoTDeviceForFunctionProto
{
    internal class IoTDevice
    {
        DeviceClient deviceClient;
        string connectionString;
        Logger logger;
        CancellationTokenSource cancellationTokenSource;
        Random random;
        int sendInterval;
        Task workTask;
        bool workng = false;

        static readonly string dpKeyInterval = "interval";

        public IoTDevice(string connectionString,  Logger logger)
        {
            this.connectionString = connectionString;
            this.logger = logger;
        }

        public async Task StartAsync()
        {
            deviceClient = DeviceClient.CreateFromConnectionString(connectionString);
            deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChanged);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdated, this);
            await deviceClient.SetMethodDefaultHandlerAsync(MethodInvoked, this);
            await deviceClient.SetReceiveMessageHandlerAsync(ReceivedMessage, this);

            await deviceClient.OpenAsync();

            var dp = await deviceClient.GetTwinAsync();
            if (dp.Properties.Desired.Contains(dpKeyInterval))
            {
                sendInterval = (int)dp.Properties.Desired[dpKeyInterval];
            }
            else
            {
                sendInterval = 1000;
            }
            await NotifyStatus("initialized");
        }

        public async Task StopAsync()
        {
            cancellationTokenSource.Cancel();
            workTask.Wait();
            await NotifyStatus("terminated");
            await deviceClient.CloseAsync();
        }

        public async Task WorkAsync()
        {
            lock (this)
            {
                workng = true;
            }
            await NotifyStatus("working");
            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            random = new Random(DateTime.Now.Millisecond);
            workTask = new Task(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    while (true)
                    {
                        var data = new IoTDataPacket()
                        {
                            x = random.NextDouble(),
                            y = random.NextDouble(),
                            z = random.Next()
                        };
                        var msg = new Message(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
                        await deviceClient.SendEventAsync(msg);
                        await Task.Delay(sendInterval);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    lock (this)
                    {
                        workng = false;
                    }
                    await NotifyStatus("Stopping");
                }
            );
            workTask.Start();
        }

        public async Task StartWork()
        {
            bool currentWorking = false;
            lock (this)
            {
                currentWorking = this.workng;
            }
            if (currentWorking==false)
            {
                await WorkAsync();
            }
        }
        public async Task StopWork()
        {
            bool currentWorking = false;
            lock (this)
            {
                currentWorking = this.workng;
            }
            if (workng)
            {
                cancellationTokenSource.Cancel();
                workTask.Wait();
            }
        }

        public async Task NotifyStatus(string status)
        {
            var rp = new ReportedProperties()
            {
                status = status,
                interval = this.sendInterval
            };
            var reportedProperties = new TwinCollection(Newtonsoft.Json.JsonConvert.SerializeObject(rp));
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }


        private async Task ReceivedMessage(Message message, object userContext)
        {
            await logger.LogInfo($"Received Message - '{System.Text.Encoding.UTF8.GetString(message.GetBytes())}'");
        }

        private async Task<MethodResponse> MethodInvoked(MethodRequest methodRequest, object userContext)
        {
            await logger.LogInfo($"Invoked Method - {methodRequest.Name}(payload:{methodRequest.DataAsJson})");
            var responsePayload = new
            {
                message = "Done"
            };
            switch (methodRequest.Name)
            {
                case "start":
                    await StartWork();
                    break;
                case "stop":
                    await StopWork();
                    break;
            }
            return new MethodResponse(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(responsePayload)), (int)HttpStatusCode.OK);             
        }

        private async Task DesiredPropertyUpdated(TwinCollection desiredProperties, object userContext)
        {
            await logger.LogInfo($"Updated Desired Properties - '{desiredProperties.ToJson()}'");
            if(desiredProperties.Contains(dpKeyInterval))
            {
                sendInterval = (int)desiredProperties[dpKeyInterval];
                await NotifyStatus("Updated");
            }
            
        }

        private void ConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            logger.LogInfo($"Connection Status={status.ToString()},Reason={reason.ToString()}");
        }
    }

    public class IoTDataPacket
    {
        public double x { get; set; }
        public double y { get; set; }
        public int z { get; set; }
    }
    public class ReportedProperties
    {
        // 0->initialized,1->working,2->completed
        public string status { get; set; }
        public int interval { get; set; }
    }
}
