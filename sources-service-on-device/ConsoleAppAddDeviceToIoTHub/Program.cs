// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Devices;

Console.WriteLine("Hello, World!");

string connectionString = "H<- Connection String of IoT Hub Registry Writable Access Policy ->";
var builder = IotHubConnectionStringBuilder.Create(connectionString);
string deviceId = Guid.NewGuid().ToString();

var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
var device = await registryManager.AddDeviceAsync(new Device(deviceId));
var authentication = device.Authentication;
Console.WriteLine($"authentication - {authentication.Type.ToString()}");
if (authentication.Type== AuthenticationType.Sas)
{
    string devicePrimaryKey = authentication.SymmetricKey.PrimaryKey;
    //string connectionStringForDevice = $"HostName={builder.HostName};DeviceId={device.Id};SharedAccessKey={devicePrimaryKey}";
}
var builderForService = IotHubConnectionStringBuilder.Create(connectionString);
string connectionStringForDevice = $"HostName={builder.HostName};DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
Console.WriteLine($"connection string - {connectionStringForDevice}");