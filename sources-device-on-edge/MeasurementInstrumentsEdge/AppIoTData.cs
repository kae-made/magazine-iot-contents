// ------------------------------------------------------------------------------
// <auto-generated>
//     このコードはツールによって生成されました。
//     ランタイム バージョン: 0.1.1
//  
//     このファイルへの変更は、正しくない動作の原因になる可能性があり、
//     コードが再生成されると失われます。
// </auto-generated>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace My.Gen.MeasurementInstrumentsEdge
{
    class D2CData : Kae.IoT.Framework.IoTDataWithProperties
    {
        public class EnvironmentDataType
        {
            public double Temperature { get; set; }

            public double Humidity { get; set; }

            public double AtmosphericPressure { get; set; }

            public double CO2Concentration { get; set; }

            public double Brightness { get; set; }

            public DateTime MeasuredTime { get; set; }

        }

        public EnvironmentDataType Environment { get; set; }


        public override Kae.IoT.Framework.IoTData Deserialize(string json)
        {
            return (D2CData) Newtonsoft.Json.JsonConvert.DeserializeObject(json, typeof(D2CData));
        }

        public override string Serialize()
        {
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            return content;
        }

        public D2CData()
        {
            this.Environment = new EnvironmentDataType();
        }
    }

    class AppDTDesiredProperties : Kae.IoT.Framework.IoTData
    {
        public int RequestInterval { get; set; }


        public override Kae.IoT.Framework.IoTData Deserialize(string json)
        {
            Console.WriteLine($"Deserializing - '{json}'");
            var dtProps = (AppDTDesiredProperties) Newtonsoft.Json.JsonConvert.DeserializeObject(json, typeof(AppDTDesiredProperties));
            Console.WriteLine($"RequestInterval = {dtProps.RequestInterval}");
            return dtProps;
        }

        public override string Serialize()
        {
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            return content;
        }
    }

    class AppDTReporetedProperties : Kae.IoT.Framework.IoTData
    {
        public int CurrentInterval { get; set; }

        public string DeviceStatus { get; set; }


        public override Kae.IoT.Framework.IoTData Deserialize(string json)
        {
            return (AppDTReporetedProperties) Newtonsoft.Json.JsonConvert.DeserializeObject(json, typeof(AppDTReporetedProperties));
        }

        public override string Serialize()
        {
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            return content;
        }
    }

}
