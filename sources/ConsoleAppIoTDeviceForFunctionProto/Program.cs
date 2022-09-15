// See https://aka.ms/new-console-template for more information
using Kae.Utility.Logging;

Console.WriteLine("Hello, World!");
var iotdevice = new ConsoleAppIoTDeviceForFunctionProto.IoTDevice(args[1], ConsoleLogger.CreateLogger());
await iotdevice.StartAsync();
var key =Console.ReadKey();
await iotdevice.StopAsync();
