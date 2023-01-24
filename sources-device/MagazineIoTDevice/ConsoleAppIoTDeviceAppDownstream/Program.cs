// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.Security.Cryptography.X509Certificates;

Console.WriteLine("Hello, World!");

string cs = "HostName=<your IoT Hub>.azure-devices.net;DeviceId=<downstream Device Id>;SharedAccessKey=<SAS key for the downstream device>=;GatewayHostName=<FQDN or IP Address of gateway>";

#if false
string trustedCACertPath = "/var/aziot/certs/azure-iot-test-only.root.ca.cert.pem";

Console.WriteLine($"Attempting to install CA certificate: {trustedCACertPath}");
try
{
    var x509Store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
    x509Store.Open(OpenFlags.ReadWrite);
    x509Store.Add(new X509Certificate2(X509Certificate.CreateFromCertFile(trustedCACertPath)));
    Console.WriteLine($"Successfully added certificate : {trustedCACertPath}");
    x509Store.Close();
}
catch(Exception ex)
{
    Console.WriteLine($"x509 - {ex.Message}");
}
#endif

var deviceClient = DeviceClient.CreateFromConnectionString(cs, TransportType.Mqtt);

var random = new Random(DateTime.Now.Millisecond);
try
{
    deviceClient.SetConnectionStatusChangesHandler((status, reason) => { Console.WriteLine($"Connection status={status.ToString()},reason={reason.ToString()}"); });
    Console.WriteLine("Opening Connection...");
    await deviceClient.OpenAsync();
    Console.WriteLine("Open Done.");


    var deviceTwins = await deviceClient.GetTwinAsync();
    Console.WriteLine($"Desired Properties - {deviceTwins.Properties.Desired.ToJson()}");
    
    await deviceClient.SetDesiredPropertyUpdateCallbackAsync(
        async (dp, context) => {
            Console.WriteLine($"Desired Properties Updated - {dp.ToJson()}");
        },
        deviceClient);

    Console.WriteLine("Set Desired Property Update Callback.");

    await deviceClient.SetReceiveMessageHandlerAsync(
        async (msg, context) => {
            Console.WriteLine($"Received Message - {System.Text.Encoding.UTF8.GetString(msg.GetBytes())}");
            await deviceClient.CompleteAsync(msg);
        },
        deviceClient);
    
    Console.WriteLine("Set Received Message Handler.");
    
    await deviceClient.SetMethodDefaultHandlerAsync(
        async (methodReq, context) => {
            Console.WriteLine($"Invoked {methodReq.Name}, Payload - {System.Text.Encoding.UTF8.GetString( methodReq.Data)}");
            return new MethodResponse(200);
        },
        deviceClient);
    Console.WriteLine("Set Method Handler.");

    var rpJson = new
    {
        status = "ready"
    };

    var reportedProperties = new TwinCollection(Newtonsoft.Json.JsonConvert.SerializeObject(rpJson));
    Console.WriteLine("Updating Reported Properties");
    await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    Console.WriteLine("Updated Reported Properties");

    Console.WriteLine("Starting send work...");
    var tokenSource = new CancellationTokenSource();
    var ct = tokenSource.Token;
    var task = Task.Run(async () =>
    {
        ct.ThrowIfCancellationRequested();
        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                break;
            }
            var msg = new
            {
                value = random.Next(10),
                timestamp = DateTime.Now
            };
            string msgJson = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            var iotMsg = new Message(System.Text.Encoding.UTF8.GetBytes(msgJson));
            iotMsg.Properties.Add("source", "downstream");
            Console.WriteLine($"Sending message... - '{msgJson}'");
            await deviceClient.SendEventAsync(iotMsg);
            int nextSec = 1 + random.Next(100);
            Console.WriteLine($"Sent completed and and wait {nextSec} sec.");
            await Task.Delay(TimeSpan.FromSeconds(nextSec), ct);
        }
    }, tokenSource.Token);

    Console.WriteLine("Work started and waiting key...");
    var key = Console.ReadKey();

    Console.WriteLine("Finishing...");
    tokenSource.Cancel();
    try
    {
        await task;
    }
    catch (OperationCanceledException ex)
    {
        Console.WriteLine(ex.Message);
    }
    finally
    {
        tokenSource.Dispose();
    }

    await deviceClient.CloseAsync();
    Console.WriteLine("Closed and end.");
}
catch(Exception ex)
{
    Console.WriteLine($"Exception - {ex.Message}");
}