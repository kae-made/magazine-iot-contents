// Copyright (c) Knowledge & Experience. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// See https://aka.ms/new-console-template for more information
using Kae.IoT.Device.RaspberryPi;
using Kae.IoT.Device.RaspberryPi.Sensors;
using Kae.IoT.Device.Sensors;
using System.Diagnostics;

#if REMOTE_DEBUG
for(; ; )
{
    Console.WriteLine("Wait for remote debugging...");
    if (Debugger.IsAttached)
    {
        break;
    }
    Thread.Sleep(1000);
}
#endif

var bme280 = new BME280Sensor(1);
if (bme280.Initalize())
{
    var measurements = bme280.Read();
    foreach(var m in measurements)
    {
        Console.WriteLine($"{m.SensorType.ToString()}:{m.Value}");
    }
}

var mhz198 = new MHZ19BSensor() { Port = "/dev/serial0" };
if (mhz198.Initalize())
{
    var measurements = mhz198.Read();
    foreach(var m in measurements)
    {
        Console.WriteLine($"{m.SensorType.ToString()}:{m.Value}");
    }
}

var grobePiPlus = new GrovePiPlus();
if (grobePiPlus.Initialize(1))
{
    var lightSensor = new GrovePiLightSensor(grobePiPlus, 0);
    if (lightSensor.Initalize())
    {
        for (int i = 0; i < 10; i++)
        {
            var measurements = lightSensor.Read();
            foreach (var m in measurements)
            {
                Console.WriteLine($"{m.SensorType.ToString()}:{m.Value}");
            }
            Thread.Sleep(1000);
        }
    }

    var ledButton = new GroveLEDButton(grobePiPlus, 4, 5);
    if (ledButton.Initialize())
    {
        ledButton.TurnOn();
        Thread.Sleep(2000);
        ledButton.TurnOff();

        for (int i = 0; i < 10; i++)
        {
            var buttonState = ledButton.ReadButtonStatus();
            Console.WriteLine($"button:{buttonState.ToString()}");
            Thread.Sleep(2000);
        }
    }
}