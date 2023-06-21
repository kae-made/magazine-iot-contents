#include <ArduinoJson.h>
#include "BMI088.h"

float ax = 0, ay = 0, az = 0;
float gx = 0, gy = 0, gz = 0;
int16_t temp = 0;
BMI088 bmi088( BMI088_ACC_ADDRESS, BMI088_GYRO_ADDRESS );

String readBuf = "";
long loopUnit = 50;
long sendInterval = 5000;
long lastMillis = 0;
StaticJsonDocument<256> doc;

void setup(void) {
    Wire.begin();
    Serial.begin(115200);

    while (!Serial);
    Serial.println("BMI088 Raw Data");
    

    while (1) {
        if (bmi088.isConnection()) {
            bmi088.initialize();
            Serial.println("BMI088 is connected");
            break;
        } else {
            Serial.println("BMI088 is not connected");
        }

        delay(2000);
    }
}

String getSensorData()
{
    bmi088.getAcceleration(&ax, &ay, &az);
    bmi088.getGyroscope(&gx, &gy, &gz);
    temp = bmi088.getTemperature();

    String data = "{";
    data += "\"accelerometer\":{";
    data += "\"x\":" + String(ax);
    data += ",";
    data += "\"y\":" + String(ay);
    data += ",";
    data += "\"z\":" + String(az);
    data += "},";
    data += "\"gyro\":{";
    data += "\"x\":" + String(gx);
    data += ",";
    data += "\"y\":" + String(gy);
    data += ",";
    data += "\"z\":" + String(gz);
    data += "},";
    data += "\"temperature\":" + String(temp);
    data += "}";

    return data;
}

void loop(void) {
    String data = getSensorData();
    long nowMillis = millis();
    if (lastMillis + sendInterval <= nowMillis) {
      Serial.print(data);
      Serial.println();

      lastMillis = nowMillis;
    }

    if (Serial.available() > 0) {
      readBuf = Serial.readString();
      Serial.print(readBuf);
      DeserializationError error = deserializeJson(doc, readBuf);
      if (error){
        Serial.print(F("deserializeJson() failed: "));
        Serial.println(error.f_str());
      } else {
        Serial.print("interval update : ");
        Serial.print(sendInterval);
        Serial.print(" -> ");
        sendInterval = doc["interval"];
        Serial.print(sendInterval);
        Serial.println();
      }
    }
    delay(loopUnit);

}
