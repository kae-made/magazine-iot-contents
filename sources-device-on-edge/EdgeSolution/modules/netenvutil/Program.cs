namespace SampleModule
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;

    class Program
    {
        static int counter;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdated, ioTHubModuleClient);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            var envStatus = new Dictionary<string, object>();

            var netValues = new Dictionary<string, string>();
            envStatus.Add("network", netValues);
            foreach(var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            netValues.Add(ni.Name, ip.Address.ToString());
                        }
                    }
                }
            }

            foreach (var ek in Environment.GetEnvironmentVariables().Keys)
            {
                // Console.WriteLine($"{ek}={Environment.GetEnvironmentVariables()[ek]}");
                Console.WriteLine($"{ek}={Environment.GetEnvironmentVariable((string)ek)}");
            }

            string envStatusJson = Newtonsoft.Json.JsonConvert.SerializeObject(envStatus);
            Console.WriteLine(envStatusJson);
            var rp = new TwinCollection(envStatusJson);
            await ioTHubModuleClient.UpdateReportedPropertiesAsync(rp);
            Console.WriteLine("Reported Properties Updated.");
        }

        static async Task DesiredPropertyUpdated(TwinCollection desiredProperties, object userContext)
        {
            string dpJson = desiredProperties.ToJson();
            dynamic dp = Newtonsoft.Json.JsonConvert.DeserializeObject(dpJson);
            var rpValues = new Dictionary<string, object>();
            var envValues = new Dictionary<string, string>();
            rpValues.Add("environments", envValues);
            foreach (dynamic env in dp.environments)
            {
                Console.WriteLine($"Try get {(string)env}");
                var envVal = Environment.GetEnvironmentVariable((string)env);
                if (!string.IsNullOrEmpty(envVal))
                {
                    Console.WriteLine($"{(string)env} is {envVal}");
                    envValues.Add((string)env, envVal);
                }
            }

            string rpJson = Newtonsoft.Json.JsonConvert.SerializeObject(rpValues);
            Console.WriteLine($"Updating {rpJson} for Reported Properties...");
            var rp = new TwinCollection(rpJson);
            await ((ModuleClient)userContext).UpdateReportedPropertiesAsync(rp);
        }
    }
}
