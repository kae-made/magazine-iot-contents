using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Kae.Utility.Logging;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

namespace WpfAppDTDLParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<DTDLDecl> dtdlDecls = new List<DTDLDecl>();
        private Dictionary<string, string> dtdlFiles = new Dictionary<string, string>();
        ObservableCollection<TreeViewData> parsedDTDLs = new ObservableCollection<TreeViewData>();
        private Dictionary<string, DTInterfaceInfo> parsedIfInfos = new Dictionary<string, DTInterfaceInfo>();
        private Dictionary<string, DigitalTwinsModelData> gotModels = new Dictionary<string, DigitalTwinsModelData>();

        private Logger logger;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            tvParsedDTDL.ItemsSource = parsedDTDLs;
            logger = new TextBlockLogger(this.tbLog);

            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string adtInstanceUrl = configuration.GetConnectionString("ADT");
            tbADTConnectionString.Text = adtInstanceUrl;

            var credential = new DefaultAzureCredential();
            try
            {
                adtClient = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
                logger.LogInfo($"Service client created – ready to go");
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }

        bool parseForFiles = false;

        private void buttonSelectDTDLFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "DTDL Files(.json)|*.json";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == true)
            {
                dtdlFiles.Clear();
                tbDTDLFiles.Text = "";
                buttonParseDTDLFiles.IsEnabled = false;
                foreach (var file in dialog.FileNames)
                {
                    var fileInfo = new FileInfo(file);
                    if (!string.IsNullOrEmpty(tbDTDLFiles.Text))
                    {
                        tbDTDLFiles.Text += ", ";
                    }
                    tbDTDLFiles.Text += fileInfo.Name;
                    dtdlFiles.Add(fileInfo.Name ,file);
                }
            }
            if (dtdlFiles.Count > 0)
            {
                buttonParseDTDLFiles.IsEnabled = true;
                parseForFiles = true;
            }
        }

        List<string> modelsJson = new List<string>();

        private async void buttonParseDTDLFiles_Click(object sender, RoutedEventArgs e)
        {
            var parser = new ModelParser();
            try
            {
                parsedIfInfos.Clear();
                parsedDTDLs.Clear();
                if (parseForFiles)
                {
                    modelsJson.Clear();
                    foreach (var dtdlFile in dtdlFiles.Keys)
                    {
                        using (var reader = new StreamReader(dtdlFiles[dtdlFile]))
                        {
                            var json = reader.ReadToEnd();
                            string dtdlFileName = dtdlFile.Substring(0, dtdlFile.LastIndexOf(".json"));
                            if (dtdlFileName.LastIndexOf("_iotpnp") < 0)
                            {
                                modelsJson.Add(json);
                            }
                            await logger.LogInfo($"Loaded  - {dtdlFile}");
                            var parseResultForJson = await parser.ParseAsync(new List<string>() { json });
                            await logger.LogInfo($"Checked - {dtdlFile}");
                            foreach (var declKey in parseResultForJson.Keys)
                            {
                                var declFrag = parseResultForJson[declKey];
                                if (declFrag != null)
                                {
                                    AddInterfaceInfo(declFrag);
                                }
                            }
                        }
                    }
                    buttonUploadDTDL.IsEnabled = true;
                }
                else
                {
                    buttonUploadDTDL.IsEnabled = false;
                    var parseResultForJson = await parser.ParseAsync(modelsJson);
                    foreach(var declKey in parseResultForJson.Keys)
                    {
                        AddInterfaceInfo(parseResultForJson[declKey]);
                        await logger.LogInfo($"Parsed - {declKey.AbsolutePath}");
                    }
                    buttonGetModels.IsEnabled = true;
                }
                buttonParseDTDLFiles.IsEnabled = false;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    await logger.LogError(GetInnerExeptionMessage(ex.InnerException));
                }
                await logger.LogError(ex.Message);
            }
        }

        private void AddInterfaceInfo(DTEntityInfo ifInfo)
        {
            switch (ifInfo.EntityKind)
            {
                case DTEntityKind.Interface:
                    bool avaliable = true;
                    string id = ifInfo.Id.AbsolutePath;
                    if (!id.StartsWith("dtmi"))
                    {
                        id = $"dtmi:{id}";
                    }
                    if (gotModels.ContainsKey(id))
                    {
                        var gotModel = gotModels[$"dtmi:{ifInfo.Id.AbsolutePath}"];
                        avaliable = gotModel.Decommissioned == false;
                    }
                    var declInterface = (DTInterfaceInfo)ifInfo;
                    var declIf = new DTDLDecl() { Available = avaliable };
                    declIf.Resolve(declInterface);
                    parsedDTDLs.Add(declIf.GetTreeViewData());
                    parsedIfInfos.Add(declInterface.Id.AbsolutePath, declInterface);
                    break;
                case DTEntityKind.Component:
                    break;
                case DTEntityKind.Command:
                    break;
                case DTEntityKind.CommandPayload:
                    break;
                case DTEntityKind.CommandType:
                    break;
                case DTEntityKind.String:
                    break;
                case DTEntityKind.Integer:
                    break;
                case DTEntityKind.Array:
                    break;
                case DTEntityKind.Boolean:
                    break;
                case DTEntityKind.Date:
                    break;
                case DTEntityKind.DateTime:
                    break;
                case DTEntityKind.Double:
                    break;
                case DTEntityKind.Duration:
                    break;
                case DTEntityKind.Long:
                    break;
                case DTEntityKind.Enum:
                    break;
                case DTEntityKind.EnumValue:
                    break;
                case DTEntityKind.Object:
                    break;
                case DTEntityKind.Field:
                    break;
                case DTEntityKind.Float:
                    break;
                case DTEntityKind.Map:
                    break;
                case DTEntityKind.MapKey:
                    break;
                case DTEntityKind.MapValue:
                    break;
                case DTEntityKind.Property:
                    break;
                case DTEntityKind.Reference:
                    break;
                case DTEntityKind.Relationship:
                    break;
                case DTEntityKind.Telemetry:
                    break;
                case DTEntityKind.Time:
                    break;
                case DTEntityKind.Unit:
                    break;
                case DTEntityKind.UnitAttribute:
                    break;
            }
        }

        private string GetInnerExeptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(ex.Message);
            if (ex.InnerException != null)
            {
                sb.Append(GetInnerExeptionMessage(ex.InnerException));
            }
            return sb.ToString();
        }

        DigitalTwinsClient adtClient = null;

        private async void buttonUploadDTDL_Click(object sender, RoutedEventArgs e)
        {
            if (adtClient == null)
            {
                return;
            }
            try
            {
                await logger.LogInfo($"Service client created – ready to go");
                await adtClient.CreateModelsAsync(modelsJson);
            }
            catch (Exception ex)
            {
                await logger.LogError($"{ex.Message}");
            }
        }

        private async void buttonGetModels_Click(object sender, RoutedEventArgs e)
        {
            if (adtClient == null)
            {
                return;
            }
            AsyncPageable<DigitalTwinsModelData> models = adtClient.GetModelsAsync(new GetModelsOptions() { IncludeModelDefinition = true });
            await logger.LogInfo("Getting models from Azure Digital Twins");
            modelsJson.Clear();
            gotModels.Clear();
            tbDTDLFiles.Text = "";
            await foreach (var model in models)
            {
                var id = model.Id;
                string displayName = model.LanguageDisplayNames.Values.First();
                if (!string.IsNullOrEmpty(model.DtdlModel))
                {
                    modelsJson.Add(model.DtdlModel);
                    await logger.LogInfo($"Got - '{displayName}':{id}");
                    if (!string.IsNullOrEmpty(tbDTDLFiles.Text))
                    {
                        tbDTDLFiles.Text += ",";
                    }
                    tbDTDLFiles.Text += displayName;
                }
                gotModels.Add(id, model);
            }
            await logger.LogInfo("Getting models done.");
            if (modelsJson.Count > 0)
            {
                buttonParseDTDLFiles.IsEnabled = true;
                buttonGetModels.IsEnabled = false;
            }
        }

        private DTInterfaceInfo currentParsedInterface = null;
        private void tvParsedDTDL_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            buttonDecommissionModel.IsEnabled = false;
            buttonDeleteModel.IsEnabled = false;
            currentParsedInterface = null;
            var selectedIfInfo = (TreeViewData)tvParsedDTDL.SelectedItem;
            if (selectedIfInfo != null && parsedIfInfos.ContainsKey(selectedIfInfo.Name))
            {
                currentParsedInterface = parsedIfInfos[selectedIfInfo.Name];
                buttonDecommissionModel.IsEnabled = true;
                buttonDeleteModel.IsEnabled = true;
            }
        }

        private async void buttonDecommissionModel_Click(object sender, RoutedEventArgs e)
        {
            if (currentParsedInterface != null)
            {
                try
                {
                    string idOnADT = $"dtmi:{currentParsedInterface.Id.AbsolutePath}";
                    await adtClient.DecommissionModelAsync(idOnADT);
                }
                catch (Exception ex)
                {
                    await logger.LogError(ex.Message);
                }
            }
        }

        private async void buttonDeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (currentParsedInterface != null)
            {
                try
                {
                    string idOnADT = $"dtmi:{currentParsedInterface.Id.AbsolutePath}";
                    await adtClient.DeleteModelAsync(idOnADT);
                }
                catch (Exception ex)
                {
                    await logger.LogError(ex.Message);
                }
            }
        }
    }
    public class TreeViewData : INotifyPropertyChanged
    {
        private string name;
        private ObservableCollection<TreeViewData> children;
        private bool available = true;
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnChanged("Name");
            }
        }
        public ObservableCollection<TreeViewData> Children
        {
            get { return children; }
            set
            {
                children = value;
                OnChanged("Children");
            }
        }
        public bool Available
        {
            get { return available; }
            set
            {
                available = value;
                OnChanged("Available");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return Colors.Black;
            }
            return Colors.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class TextBlockLogger : Logger
    {
        private TextBlock textBlock;
        public TextBlockLogger(TextBlock tb)
        {
            textBlock = tb;
        }

        protected override async Task LogInternal(Level level, string log, string timestamp)
        {
            textBlock.Dispatcher.Invoke(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{timestamp}:[{level.ToString()}] {log}");
                sb.Append(textBlock.Text);
                textBlock.Text = sb.ToString();
            });
        }
    }
}
