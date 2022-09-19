// See https://aka.ms/new-console-template for more information
using Kae.Utility.Logging;

var iotdevice = new ConsoleAppIoTDeviceAppPrototype.IoTDevice(args[0], ConsoleLogger.CreateLogger());
await iotdevice.InitializeAsync();
var key = Console.ReadKey();
await iotdevice.TerminateAsync();
