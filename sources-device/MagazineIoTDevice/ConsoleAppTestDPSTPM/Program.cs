// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Devices.Provisioning.Security;

Console.WriteLine("Hello, World!");

string globalDeviceEndpoint = "global.azure-devices-provisioning.net";
string idScope = "<- your DPS ID Scope ->";
string registrationId = "< your Registration Id >";

var security = new SecurityProviderTpmHsm(registrationId);

var transportHandler = new ProvisioningTransportHandlerHttp();
var provClient = ProvisioningDeviceClient.Create(
    globalDeviceEndpoint,
    idScope,
    security,
    transportHandler);

Console.WriteLine($"Initialized for registration Id {security.GetRegistrationID()}.");

Console.WriteLine("Registering with the device provisioning service...");
DeviceRegistrationResult result = await provClient.RegisterAsync();
Console.WriteLine($"Registration status: {result.Status}.");

if (result.Status != ProvisioningRegistrationStatusType.Assigned)
{
    Console.WriteLine($"Registration status did not assign a hub, so exiting this sample.");
    return;
}

Console.WriteLine($"Device {result.DeviceId} registered to {result.AssignedHub}.");

Console.WriteLine("Creating HW TPM authentication for IoT Hub...");
IAuthenticationMethod auth = new DeviceAuthenticationWithTpm(result.DeviceId, security);

Console.WriteLine($"Testing the provisioned device with IoT Hub...");
using DeviceClient deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Amqp);

await deviceClient.OpenAsync();

var deviceTwin = await deviceClient.GetTwinAsync();
int intervalMSec = deviceTwin.Properties.Desired["intervalmsec"];
Console.WriteLine($"interval msec = {intervalMSec}");

Console.WriteLine("Sending a telemetry message...");
var msg = new
{
    message = "hello",
    timestamp = DateTime.Now
};
var iotMsg = new Message(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(msg)));
await deviceClient.SendEventAsync(iotMsg);

await deviceClient.CloseAsync();
Console.WriteLine("Done.");