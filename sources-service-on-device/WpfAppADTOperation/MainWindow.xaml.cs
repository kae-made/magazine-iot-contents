using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfAppADTOperation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<DTInterfaceInfo> dtInterfaceDefs = new ObservableCollection<DTInterfaceInfo>();
        private ObservableCollection<TwinProperty> twinProperties = new ObservableCollection<TwinProperty>();
        private ObservableCollection<string> twinsForSelectedInterface = new ObservableCollection<string>();
        private ObservableCollection<string> relationshipsForSelectedInterface = new ObservableCollection<string>();
        private ObservableCollection<string> linkedTwins = new ObservableCollection<string>();

        private string currentModelId = "";

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lbInterfaces.ItemsSource = dtInterfaceDefs;
            lbTwins.ItemsSource = twinsForSelectedInterface;
            lbTwinProps.ItemsSource = twinProperties;
            lbRelationships.ItemsSource = relationshipsForSelectedInterface;
            lbLinkedTwins.ItemsSource = linkedTwins;
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string adtInstanceUrl = configuration.GetConnectionString("ADT");
            tbADTUri.Text = adtInstanceUrl;

            var credential = new DefaultAzureCredential();
            try
            {
                adtClient = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        List<string> modelsJson = new List<string>();
        private async void buttonParseDTDLFiles_Click(object sender, RoutedEventArgs e)
        {
            if (modelsJson.Count == 0)
            {
                return;
            }
            var parser = new ModelParser();
            try
            {
                dtInterfaceDefs.Clear();
                var parseResultForJson = await parser.ParseAsync(modelsJson);
                foreach (var declKey in parseResultForJson.Keys)
                {
                    var declFrag = parseResultForJson[declKey];
                    if (declFrag != null)
                    {
                        switch (declFrag.EntityKind)
                        {
                            case DTEntityKind.Interface:
                                var declInterface = (DTInterfaceInfo)declFrag;
                                dtInterfaceDefs.Add(declInterface);
                                break;
                        }
                    }
                }
                buttonGetModels.IsEnabled = true;
                buttonParseDTDLFiles.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (ex.InnerException != null)
                {
                    MessageBox.Show(ex.InnerException.Message);
                }
            }
        }

        DigitalTwinsClient adtClient = null;

        private async void buttonConnect_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DTInterface_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var ifInfo = (DTInterfaceInfo)lbInterfaces.SelectedItem;
            SetTwinProperties(ifInfo);
            
            buttonTwinCreate.IsEnabled = true;
        }

        private void lbInterfaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ifInfo = (DTInterfaceInfo)lbInterfaces.SelectedItem;
            currentModelId = ifInfo.Id.AbsolutePath;
        }

        private void SetTwinProperties(DTInterfaceInfo ifInfo, Dictionary<string, string> propVals=null)
        {
            twinProperties.Clear();
            foreach(var ck in ifInfo.Contents.Keys)
            {
                var c = ifInfo.Contents[ck];
                if (c.EntityKind== DTEntityKind.Property)
                {
                    var propInfo = (DTPropertyInfo)c;
                    var schema = propInfo.Schema;
                    TwinProperty tp = null;
                    switch (schema.EntityKind)
                    {
                        case DTEntityKind.Array:
                        case DTEntityKind.Boolean:
                        case DTEntityKind.Date:
                        case DTEntityKind.DateTime:
                        case DTEntityKind.Double:
                        case DTEntityKind.Duration:
                        case DTEntityKind.Float:
                        case DTEntityKind.Integer:
                        case DTEntityKind.Long:
                        case DTEntityKind.String:
                        case DTEntityKind.Time:
                        case DTEntityKind.Enum:
                            tp = new TwinProperty() { Name = propInfo.Name, DataTypeOfValue = schema.EntityKind };
                            tp.PropertyInfo = propInfo;
                            tp.Name = propInfo.Name;
                            if (propVals != null)
                            {
                                tp.Value = propVals[tp.Name];
                            }
                            break;
                        case DTEntityKind.Map:
                            MessageBox.Show("Map is not supported!");
                            break;
                        case DTEntityKind.Object:
                            SetObjectProperties((DTObjectInfo)propInfo.Schema, propInfo.Name, propVals);
                            break;
                    }
                    if (tp!= null)
                    {
                        if (IsReferenceProp(propInfo))
                        {
                            tp.Writable = false;
                            tp.PropertyKind = "R";
                        }
                        else
                        {
                            int idLevel = IsIdentityProp(propInfo);
                            if (idLevel == 0)
                            {
                                if (propVals == null)
                                {
                                    tp.Value = Guid.NewGuid().ToString();
                                }
                                tp.Writable = false;
                                tp.IsIdentity = true;
                                tp.PropertyKind = "I";
                            }
                        }
                        twinProperties.Add(tp);
                    }
                }
            }
        }

        private void SetObjectProperties(DTObjectInfo objInfo, string objName, Dictionary<string,string> propVals)
        {
            foreach(var fieldInfo in objInfo.Fields)
            {
                string fieldName = $"{objName}.{fieldInfo.Name}";
                TwinProperty tp = null;
                switch (fieldInfo.Schema.EntityKind)
                {
                    case DTEntityKind.Array:
                    case DTEntityKind.Boolean:
                    case DTEntityKind.Date:
                    case DTEntityKind.DateTime:
                    case DTEntityKind.Double:
                    case DTEntityKind.Duration:
                    case DTEntityKind.Float:
                    case DTEntityKind.Integer:
                    case DTEntityKind.Long:
                    case DTEntityKind.String:
                    case DTEntityKind.Time:
                        tp = new TwinProperty() { Name = fieldName, DataTypeOfValue = fieldInfo.Schema.EntityKind };
                        tp.FieldInfo = fieldInfo;
                        if (propVals != null)
                        {
                            tp.Value = propVals[fieldName];
                        }
                        break;
                    case DTEntityKind.Enum:
                        break;
                    case DTEntityKind.Map:
                        MessageBox.Show("Map is not supported!");
                        break;
                    case DTEntityKind.Object:
                        SetObjectProperties((DTObjectInfo)fieldInfo.Schema, fieldName, propVals);
                        break;
                }
                if (tp != null)
                {
                    twinProperties.Add(tp);
                }
            }
        }

        private bool IsReferenceProp(DTPropertyInfo propInfo)
        {
            string comment = propInfo.Comment;
            if (!string.IsNullOrEmpty(comment) && comment.StartsWith("@"))
            {
                var ms = comment.Substring(1).Split(new char[] {','});
                foreach(var m in ms)
                {
                    if (m.StartsWith("R"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int IsIdentityProp(DTPropertyInfo propInfo)
        {
            int idLevel = -1;
            string comment = propInfo.Comment;
            if (!string.IsNullOrEmpty(comment) && comment.StartsWith("@"))
            {
                var ms = comment.Substring(1).Split(new char[] { ',' });
                foreach (var m in ms)
                {
                    if (m.StartsWith("I"))
                    {
                        var id = m.Substring(1);
                        idLevel = int.Parse(m.Substring(1));
                    }
                }
            }
            return idLevel;

        }

        private async void buttonTwinCreate_Click(object sender, RoutedEventArgs e)
        {
            if (adtClient == null)
            {
                MessageBox.Show("Please connect to Azure Digital Twins");
                return;
            }
            if (string.IsNullOrEmpty(currentModelId))
            {
                return;
            }
            try
            {
                var contents = new Dictionary<string, object>();
                string twinId = "";
                var objectValues = new Dictionary<string, Dictionary<string, object>>();
                foreach (var tp in twinProperties)
                {
                    if (tp.IsIdentity)
                    {
                        twinId = (string)tp.Value;
                    }
                    var names = tp.Name.Split(new char[] { '.' });
                    if (names.Length == 1)
                    {
                        object typedValue = tp.GetDataTypedValue();
                        if (typedValue != null)
                        {
                            contents.Add(tp.Name, typedValue);
                        }
                    }
                    else
                    {
                        if (names.Length > 2)
                        {
                            MessageBox.Show("Not support deeper than 2 levels");
                        }
                        else
                        {
                            if (!objectValues.ContainsKey(names[0]))
                            {
                                objectValues.Add(names[0], new Dictionary<string, object>());
                            }
                            objectValues[names[0]].Add(names[1], tp.GetDataTypedValue());
                        }
                    }
                }
                foreach(var ovk in objectValues.Keys)
                {
                    contents.Add(ovk, objectValues[ovk]);
                }
                if (!currentModelId.StartsWith("dtmi"))
                {
                    currentModelId = $"dtmi:{currentModelId}";
                }

                var twinData = new BasicDigitalTwin()
                {
                    Id = twinId,
                    Metadata = { ModelId = currentModelId },
                    Contents = contents
                };

                await adtClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(twinId, twinData);


                buttonTwinCreate.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        Dictionary<string,DigitalTwinsModelData> gotModels = new Dictionary<string,DigitalTwinsModelData>();
        private async void buttonGetModels_Click(object sender, RoutedEventArgs e)
        {
            if (adtClient == null)
            {
                return;
            }
            buttonGetModels.IsEnabled = false;
            AsyncPageable<DigitalTwinsModelData> models = adtClient.GetModelsAsync(new GetModelsOptions() { IncludeModelDefinition = true });
            modelsJson.Clear();
            gotModels.Clear();
            await foreach (var model in models)
            {
                var id = model.Id;
                string displayName = model.LanguageDisplayNames.Values.First();
                if (!string.IsNullOrEmpty(model.DtdlModel))
                {
                    modelsJson.Add(model.DtdlModel);
                }
                gotModels.Add(id, model);
            }
            if (modelsJson.Count > 0)
            {
                buttonParseDTDLFiles.IsEnabled = true;
                buttonGetModels.IsEnabled = false;
            }
            MessageBox.Show($"Got {modelsJson.Count} models.");
        }
    }
}
