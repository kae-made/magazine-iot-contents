using Azure.DigitalTwins.Core;
using Azure.Identity;
using Kae.Utility.Logging;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        }

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
            }
        }

        List<string> modelsJson = new List<string>();

        private async void buttonParseDTDLFiles_Click(object sender, RoutedEventArgs e)
        {
            var parser = new ModelParser();
            try
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
                        foreach(var declKey in parseResultForJson.Keys)
                        {
                            var declFrag = parseResultForJson[declKey];
                            if (declFrag != null)
                            {
                                switch (declFrag.EntityKind)
                                {
                                    case DTEntityKind.Interface:
                                        var declInterface = (DTInterfaceInfo)declFrag;
                                        var declIf = new DTDLDecl();
                                        declIf.Resolve(declInterface);
                                        parsedDTDLs.Add(declIf.GetTreeViewData());
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
                        }
                    }
                }
                buttonUploadDTDL.IsEnabled = true;
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

        private async void buttonUploadDTDL_Click(object sender, RoutedEventArgs e)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string adtInstanceUrl = configuration.GetConnectionString("ADT"); 

            var credential = new DefaultAzureCredential();
            try
            {
                var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
                await logger.LogInfo($"Service client created – ready to go");
                await client.CreateModelsAsync(modelsJson);
            }
            catch (Exception ex)
            {
                await logger.LogError($"{ex.Message}");
            }
        }

    }
    public class TreeViewData
    {
        public string Name { get; set; }
        public ObservableCollection<TreeViewData> Children { get; set; }
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
