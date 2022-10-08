using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAppADTOperation
{
    public class TwinProperty : INotifyPropertyChanged
    {
        private string name;
        private string valueOf;
        private bool writable = true;
        private string propertyKind = "";
        private DTPropertyInfo propInfo = null;
        private DTFieldInfo fieldInfo = null;

        public string Name
        {
            get { return name; }
            set { name = value;
                OnPropertyChanged("Name");
            }
        }
        public string Value
        {
            get { return valueOf; }
            set
            {
                valueOf = value;
                OnPropertyChanged("Value");
            }
        }

        public bool Writable
        {
            get { return writable; }
            set
            {
                writable = value;
                OnPropertyChanged("Writable");
            }
        }

        public string PropertyKind
        {
            get { return propertyKind; }
            set
            {
                propertyKind = value;
                OnPropertyChanged("PropertyKind");
            }
        }

        public bool IsIdentity { get; set; } = false;

        public DTEntityKind DataTypeOfValue { get; set; }

        public DTPropertyInfo PropertyInfo { get { return propInfo; } set { propInfo = value; } }
        public DTFieldInfo FieldInfo { get { return fieldInfo; } set { fieldInfo = value; } }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public object GetDataTypedValue()
        {
            object dataValue = null;
            DTSchemaInfo schemaInfo = null;
            if (propInfo != null)
            {
                schemaInfo = propInfo.Schema;
            }
            else if (fieldInfo != null)
            {
                schemaInfo = fieldInfo.Schema;
            }
            if (schemaInfo == null)
            {
                throw new ArgumentOutOfRangeException("propInfo or fieldInfo shoudn't be null!");
            }
            if (DataTypeOfValue == DTEntityKind.Array)
            {
                dataValue = ConvertToValueAsSpecifiedValue(Value, (DTArrayInfo)schemaInfo);
            }
            else if (DataTypeOfValue == DTEntityKind.Enum)
            {
                dataValue = ConvertToValueAsSpecifiedValue(Value, (DTEnumInfo)schemaInfo);
            }
            else
            {
                dataValue = ConvertToValueAsSpecifiedValue(Value, DataTypeOfValue);
            }

            return dataValue;
        }

        public static object ConvertToValueAsSpecifiedValue(string currentValue, DTEnumInfo enumInfo)
        {
            object dataValue = null;
            if (enumInfo.ValueSchema.EntityKind== DTEntityKind.String)
            {
                if (string.IsNullOrEmpty(currentValue))
                {
                    dataValue = "";
                }
                else
                {
                    dataValue = currentValue;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(currentValue))
                {
                    dataValue = 0;
                }
                else
                {
                    dataValue=int.Parse(currentValue);
                }
            }
            return dataValue;
        }

        public static List<object> ConvertToValueAsSpecifiedValue(string currentValue, DTArrayInfo arrayInfo)
        {
            var dataValue = new List<object>();

            if (!string.IsNullOrEmpty(currentValue))
            {
                var elemsInDataValue = currentValue.Split(new char[] { ',' });
                foreach (var elem in elemsInDataValue)
                {
                    var elemValue = elem.Trim();
                    var convertedValue = ConvertToValueAsSpecifiedValue(elemValue, arrayInfo.ElementSchema.EntityKind);
                    dataValue.Add(convertedValue);
                }
            }

            return dataValue;
        }
        public static object ConvertToValueAsSpecifiedValue(string currentValue, DTEntityKind entityKind)
        {
            object dataValue = null;
            switch (entityKind)
            {
                case DTEntityKind.Array:
                    break;
                case DTEntityKind.Boolean:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = false;
                    }
                    else
                    {
                        dataValue = bool.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Date:
                case DTEntityKind.DateTime:
                case DTEntityKind.Time:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = "";
                    }
                    else
                    {
                        dataValue = DateTime.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Double:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = (double)0.0;
                    }
                    else
                    {
                        dataValue = double.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Duration:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = TimeSpan.FromTicks(0);
                    }
                    else
                    {
                        dataValue = TimeSpan.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Enum:
                    throw new ArgumentOutOfRangeException("Please use 'ConvertToValueAsSpecifiedValue(string currentValue, DTEnumInfo enumInfo)' for enumeration");
                case DTEntityKind.Float:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = (float)0.0;
                    }
                    else
                    {
                        dataValue = float.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Integer:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = (int)0;
                    }
                    else
                    {
                        dataValue = int.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.Long:
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = (long)0;
                    }
                    else
                    {
                        dataValue = long.Parse(currentValue);
                    }
                    break;
                case DTEntityKind.String:
                    dataValue = currentValue;
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        dataValue = "";
                    }
                    break;
            }

            return dataValue;
        }
    }
}
