import json
import datetime
import asyncio

from sense_hat import SenseHat
from iotclientadaptor import IoTClientAdaptor

# Sense Hat API : https://pythonhosted.org/sense-hat/api/

class IoTApp:
    def __init__(self, adaptor):
        self.sensorSensing ={
            'humidity': False,
            'temperature': False,
            'temperature_from_humidity': False,
            'temperature_from_pressure': False,
            'pressure': False,
            'orientation_radians': False,
            'orientation_degrees': False,
            'orientation': False,
            'compass': False,
            'compass_raw': False,
            'gyroscope': False,
            'gyroscope_raw': False,
            'accelerometer': False,
            'accelerometer_raw': False
        }
        self.settingLock = asyncio.Lock()
        self.senseHatLock = asyncio.Lock()
        self.senseHat =  SenseHat()
        self.iotClientAdaptor = adaptor
        self.telemetrySendingIntervalMSec = 5000

    async def updateSensorSettings(self, desiredProps):
        if 'sensor_setting' in desiredProps:
            async with self.settingLock:
                desiredSensorSettings = desiredProps['sensor_setting']
                for k in desiredSensorSettings.keys():
                    self.sensorSensing[k] = desiredSensorSettings[k]
                self.setupSensors()
    
    def setupSensors(self):
        compassEnabled = False
        gyroEnabled = False
        accelEnabled = False
        for k in self.sensorSensing.keys():
            if self.sensorSensing[k]:
                if k.startswith('compass'):
                    compassEnabled = True
                    continue
                elif k.startswith('gyro') or k.startswith('orientation'):
                    gyroEnabled = True
                    continue
                elif k.startswith('accel'):
                    accelEnabled = True
                    continue

        self.senseHat.set_imu_config(compassEnabled, gyroEnabled, accelEnabled)

    async def sensingSensors(self):
        sensedValues = {}
        async with self.settingLock:
            for k in self.sensorSensing.keys():
                if self.sensorSensing[k]:
                    value = 0.0
                    if k == 'humidity':
                        sensedValues[k] = self.senseHat.get_humidity()
                    elif k == 'temperature':
                        sensedValues[k] = self.senseHat.get_temperature()
                    elif k == 'temperature_from_humidity':
                        sensedValues[k] = self.senseHat.get_temperature_from_humidity()
                    elif k == 'temperature_from_pressure':
                        sensedValues[k] = self.senseHat.get_temperature_from_pressure()
                    elif k == 'pressure':
                        sensedValues[k] = self.senseHat.get_pressure()
                    elif k == 'orientation_radians':
                        sensedValues[k] = self.senseHat.get_orientation_radians()
                    elif k == 'orientation_degrees':
                        sensedValues[k] = self.senseHat.get_orientation_degrees()
                    elif k == 'orientation':
                        sensedValues[k] = self.senseHat.get_orientation()
                    elif k == 'compass':
                        sensedValues[k] = self.senseHat.get_compass()
                    elif k == 'compass_raw':
                        sensedValues[k] = self.senseHat.get_compass_raw()
                    elif k == 'gyroscope':
                        sensedValues[k] = self.senseHat.get_gyroscope()
                    elif k == 'gyroscope_raw':
                        sensedValues[k] = self.senseHat.get_gyroscope_raw()
                    elif k == 'accelerometer':
                        sensedValues[k] = self.senseHat.get_accelerometer()
                    elif k == 'accelerometer_raw':
                        sensedValues[k] = self.senseHat.get_accelerometer_raw()
            sensedValues['timestamp'] = datetime.datetime.now().strftime("%Y/%m/%dT%H:%M:%S.%f")
        return sensedValues

    def showPixels(self, rotation, pixelMatrix):
        print("Executing set_pixels...")
        self.senseHat.set_rotation(rotation, False)
        self.senseHat.set_pixels(pixelMatrix)

    def crearLed(self, color):
        print("Executing clear...")
        self.senseHat.clear(color)

    def showMessage(self, rotation, text, speed, forground, background):
        print("Executing show_message...")
        self.senseHat.set_rotation(rotation, False)
        self.senseHat.show_message(text, speed, forground, background)

    def showLetter(self, rotation, text, forground, background):
        print("Executing show_letter...")
        self.senseHat.set_rotation(rotation, False)
        self.senseHat.show_letter(text, forground, background)

    async def initlialize(self):
        dp = await self.iotClientAdaptor.getDesiredProperties()
        await self.updateSensorSettings(dp)
        if 'telemetry_send_interval_msec' in dp:
            self.telemetrySendingIntervalMSec = dp['telemetry_send_interval_msec']

        await self.iotClientAdaptor.updateReportedProperties(
            {
                'current_sensors': self.sensorSensing,
                'current_telemetry_send_interval_msec': self.telemetrySendingIntervalMSec
            })
        
    async def devicePropertiesListener(self):
        while True:
            patch = await self.iotClientAdaptor.receiveTwinDesiredPropertiesPatch()  # blocking call
            await self.updateSensorSettings(patch)
            if 'telemetry_send_interval_msec' in patch:
                self.telemetrySendingIntervalMSec = patch['telemetry_send_interval_msec']

            await self.iotClientAdaptor.updateReportedProperties(
                {
                    'current_sensors': self.sensorSensing,
                    'current_telemetry_send_interval_msec': self.telemetrySendingIntervalMSec
                })
            
    async def updatedDesiredProperties(self, patch):
        await self.updateSensorSettings(patch)
        if 'telemetry_send_interval_msec' in patch:
            self.telemetrySendingIntervalMSec = patch['telemetry_send_interval_msec']

        await self.iotClientAdaptor.updateReportedProperties(
            {
                'current_sensors': self.sensorSensing,
                'current_telemetry_send_interval_msec': self.telemetrySendingIntervalMSec
            })
            
    async def directMethodsListener(self):
        while True:
            responseStatus = 200
            responsePayload = {}
            command_request = await self.iotClientAdaptor.receiveMethodRequest(None)
            if command_request.name == 'showPixels':
                self.showPixels(command_request.payload['rotation'], command_request.payload['pixel_matrix'])
            elif command_request.name == 'showMessage':
                self.showMessage(command_request.payload['rotation'], command_request.payload['text'], command_request.payload['speed'], command_request.payload['forgraound'], command_request.payload['background'])
            elif command_request.name == 'showLetter':
                self.showLetter(command_request.payload['rotation'], command_request.payload['text'], command_request.payload['forgraound'], command_request.payload['background'])
            elif command_request.name == 'clearLed':
                self.crearLed(command_request.payload['color'])
            else:
                responseStatus = 400
                responsePayload['method_name'] = command_request.name
                responsePayload['reason'] = "not existed."

            await self.iotClientAdaptor.returnMethodResponse(command_request, responseStatus, responsePayload)
            
    async def invokedDirectMethods(self, command_request):
        print("processing invokedDirectMethods...")
        responseStatus = 200
        responsePayload = {}
        async with self.senseHatLock:
            if command_request.name == 'showPixels':
                self.showPixels(command_request.payload['rotation'], command_request.payload['pixel_matrix'])
            elif command_request.name == 'showMessage':
                self.showMessage(command_request.payload['rotation'], command_request.payload['text'], command_request.payload['speed'], command_request.payload['forground'], command_request.payload['background'])
            elif command_request.name == 'showLetter':
                self.showLetter(command_request.payload['rotation'], command_request.payload['text'], command_request.payload['forground'], command_request.payload['background'])
            elif command_request.name == 'clearLed':
                self.crearLed(command_request.payload['color'])
            else:
                responseStatus = 400
                responsePayload['method_name'] = command_request.name
                responsePayload['reason'] = "not existed."
        print("processed invokedDirectMethod")
        await self.iotClientAdaptor.returnMethodResponse(command_request, responseStatus, responsePayload)

    async def doWork(self):
        print("Sending telemetry for sensors")

        while True:
            sensingValues = await self.sensingSensors()
            if len(sensingValues) > 0:
                await self.iotClientAdaptor.sendTelemetry(sensingValues)
            intervalf = 1.0
            async with self.settingLock:
                intervalf = float(self.telemetrySendingIntervalMSec)/1000.0
            await asyncio.sleep(intervalf)
