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

        public TwinProperty(DTPropertyInfo propInfo)
        {
            this.propInfo = propInfo;
        }

        public TwinProperty()
        {
            ;
        }

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
            object dataType = null;

            switch (DataTypeOfValue)
            {
                case DTEntityKind.Array:
                    break;
                case DTEntityKind.Boolean:
                    dataType = bool.Parse(Value);
                    break;
                case DTEntityKind.Date:
                case DTEntityKind.DateTime:
                case DTEntityKind.Time:
                    dataType = DateTime.Parse(Value);
                    break;
                case DTEntityKind.Double:
                    dataType = double.Parse(Value);
                    break;
                case DTEntityKind.Duration:
                    dataType =TimeSpan.Parse(Value);
                    break;
                case DTEntityKind.Enum:
                    var enumSchema = (DTEnumInfo)propInfo.Schema;
                    if (enumSchema.ValueSchema.EntityKind== DTEntityKind.String)
                    {
                        dataType = Value;
                    }
                    else
                    {
                        dataType = int.Parse(Value);
                    }
                    break;
                case DTEntityKind.Float:
                    dataType = float.Parse(Value);
                    break;
                case DTEntityKind.Integer:
                    dataType = int.Parse(Value);
                    break;
                case DTEntityKind.Long:
                    dataType = long.Parse(Value);
                    break;
                case DTEntityKind.String:
                    dataType = Value;
                    break;
            }


            return dataType;
        }
    }
}
