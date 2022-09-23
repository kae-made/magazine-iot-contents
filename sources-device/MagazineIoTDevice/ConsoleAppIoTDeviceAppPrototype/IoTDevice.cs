﻿using Kae.Utility.Logging;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppIoTDeviceAppPrototype
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
        bool autoStart = false;

        const string dpKeyInterval = "RequestInterval";
        const string dmKeyStart = "Start";
        const string dmKeyStop = "Stop";

        public IoTDevice(string connectionString, string pnpModelId, Logger logger, bool autoStart=false)
        {
            this.connectionString = connectionString;
            this.logger = logger;
            var clientOptions = new ClientOptions();
            if (!string.IsNullOrEmpty(pnpModelId))
            {
                clientOptions.ModelId = pnpModelId;
            }
            deviceClient = DeviceClient.CreateFromConnectionString(this.connectionString, clientOptions);
            random = new Random(DateTime.Now.Millisecond);
            cancellationTokenSource = new CancellationTokenSource();
            this.autoStart = autoStart;
        }

        public async Task InitializeAsync()
        {
            deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChanged);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdated, this);
            await deviceClient.SetMethodDefaultHandlerAsync(MethodInvoked, this);
            await deviceClient.SetReceiveMessageHandlerAsync(ReceivedMessage, this);

            await deviceClient.OpenAsync();
            await logger.LogInfo("IoT Hub has been connected");

            var dp = await deviceClient.GetTwinAsync();
            if (dp.Properties.Desired.Contains(dpKeyInterval))
            {
                sendInterval = (int)dp.Properties.Desired[dpKeyInterval];
            }
            else
            {
                sendInterval = 1000;
            }

            if (autoStart)
            {
                StartWork();
            }
            await NotifyStatus("initialized");
        }

        public async Task TerminateAsync()
        {
            var currentWorking = false;
            lock (this)
            {
                currentWorking = workng;
            }
            if (currentWorking)
            {
                await StopWork();
            }
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
            var cancellationToken = cancellationTokenSource.Token;
            workTask = new Task(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (true)
                {
                    await SendMessage();
                    int currentSendInterval = 1000;
                    lock (this)
                    {
                        currentSendInterval = sendInterval;
                    }
                    await Task.Delay(currentSendInterval);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                lock (this)
                {
                    workng = false;
                }
            }
            );
            workTask.Start();
        }

        public async Task SendMessage()
        {
            var data = new 
            {
                Environment = new IoTDataPacket()
                {
                    Temperature = 25 + random.NextDouble(),
                    Humidity = 50 + random.NextDouble(),
                    Pressure = 1000 + random.NextDouble(),
                    Timestamp = DateTime.Now
                }
            };
            var dataJson = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            var msg = new Message(System.Text.Encoding.UTF8.GetBytes(dataJson));
#if PHASE_DEVELOPMENT
            msg.Properties.Add("phase", "development");
#endif
            await deviceClient.SendEventAsync(msg);
            await logger.LogInfo($"Send - '{dataJson}'");
        }

        public async Task StartWork()
        {
            var currentWorking = false;
            lock (this)
            {
                currentWorking = this.workng;
            }
            if (currentWorking == false)
            {
                await WorkAsync();
                Task.WaitAll(workTask);
            }
        }
        public async Task StopWork()
        {
            var currentWorking = false;
            lock (this)
            {
                currentWorking = this.workng;
            }
            if (workng)
            {
                cancellationTokenSource.Cancel();
                Task.WaitAll(workTask);
                await NotifyStatus("stopped");
            }
        }

        public async Task NotifyStatus(string status)
        {
            var rp = new ReportedProperties()
            {
                DeviceStatus = status
            };
            lock (this)
            {
                rp.CurrentInterval = sendInterval;
            }
            var reportedProperties = new TwinCollection(Newtonsoft.Json.JsonConvert.SerializeObject(rp));
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }


        private async Task ReceivedMessage(Message message, object userContext)
        {
            await logger.LogInfo($"Received Message - '{System.Text.Encoding.UTF8.GetString(message.GetBytes())}'");
            await deviceClient.CompleteAsync(message);
        }

        private async Task<MethodResponse> MethodInvoked(MethodRequest methodRequest, object userContext)
        {
            await logger.LogInfo($"Invoked Method - {methodRequest.Name}(payload:{methodRequest.DataAsJson})");
            var responsePayload = new
            {
                message = "done"
            };
            switch (methodRequest.Name)
            {
                case dmKeyStart:
                    await StartWork();
                    break;
                case dmKeyStop:
                    await StopWork();
                    break;
            }
            return new MethodResponse(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(responsePayload)), (int)HttpStatusCode.OK);
        }

        private async Task DesiredPropertyUpdated(TwinCollection desiredProperties, object userContext)
        {
            await logger.LogInfo($"Updated Desired Properties - '{desiredProperties.ToJson()}'");
            if (desiredProperties.Contains(dpKeyInterval))
            {
                lock (this)
                {
                    sendInterval = (int)desiredProperties[dpKeyInterval];
                }
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
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public DateTime Timestamp { get; set; }
    }
    public class ReportedProperties
    {
        // 0->initialized,1->working,2->completed
        public string DeviceStatus { get; set; }
        public int CurrentInterval { get; set; }
    }

}
