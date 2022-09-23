// See https://aka.ms/new-console-template for more information
using Kae.Utility.Logging;

bool autoStart = false;
if (args.Length > 1)
{
    autoStart = bool.Parse(args[1]);
}

string modelId = "dtmi:com:kae:sample:TestDevice;1";

var iotdevice = new ConsoleAppIoTDeviceAppPrototype.IoTDevice(args[0], modelId, ConsoleLogger.CreateLogger(), autoStart);
await iotdevice.InitializeAsync();
await iotdevice.SendMessage();
var key = Console.ReadKey();


await iotdevice.TerminateAsync();
