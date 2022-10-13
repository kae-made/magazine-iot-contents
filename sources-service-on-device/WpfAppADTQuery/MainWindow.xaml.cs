using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace WpfAppADTQuery
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<DTInterfaceInfo> twinModels = new ObservableCollection<DTInterfaceInfo>();
        Dictionary<string, DTInterfaceInfo> twinInterfaceDefs = new Dictionary<string, DTInterfaceInfo>();
        Dictionary<string, DigitalTwinsModelData> twinModelDefs = new Dictionary<string, DigitalTwinsModelData>();
        ObservableCollection<string> outGoingRelationships = new ObservableCollection<string>();
        ObservableCollection<string> inComingRelationships = new ObservableCollection<string>();
        Dictionary<string, DTRelationshipInfo> relationshipDefs = new Dictionary<string, DTRelationshipInfo>();
        ObservableCollection<string> twinsForResultOfQueryForTwin = new ObservableCollection<string>();
        ObservableCollection<string> twinsForResultOfQueryForRelationship = new ObservableCollection<string>();

        DigitalTwinsClient adtClient = null;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string adtInstanceUrl = configuration.GetConnectionString("ADT");
            tbADTUrl.Text = adtInstanceUrl;

            this.lbTwinModels.ItemsSource = twinModels;
            this.lbOutgoingRelationships.ItemsSource = outGoingRelationships;
            this.lbInComingRelationships.ItemsSource = inComingRelationships;
            this.lbTwins.ItemsSource = twinsForResultOfQueryForTwin;
            this.lbLinkedTwins.ItemsSource = twinsForResultOfQueryForRelationship;

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

        private async void buttonGetModels_Click(object sender, RoutedEventArgs e)
        {
            if (adtClient == null)
            {
                return;
            }
#if false
            pbProcessModeling.Visibility = Visibility.Visible;
            tbProcessing.Visibility = Visibility.Visible;
            workingProgress.Processing = "Getting Models...";
            workingProgress.Progress = 0;
            var cancelationSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (true)
                {
                    int cp = workingProgress.Progress;
                    if (++cp >= pbProcessModeling.Maximum)
                    {
                        cp = 0;
                    }
                    workingProgress.Progress = cp;
                    await Task.Delay(100, cancelationSource.Token);
                    if (cancelationSource.Token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            });
#endif
            buttonGetModels.IsEnabled = false;
            AsyncPageable<DigitalTwinsModelData> models = adtClient.GetModelsAsync(new GetModelsOptions() { IncludeModelDefinition = true });

#if false
            cancelationSource.Cancel();
            tbProcessing.Visibility = Visibility.Hidden;
            pbProcessModeling.Visibility = Visibility.Hidden;
#endif

            var modelsJson = new List<string>();
            twinModels.Clear();
            twinModelDefs.Clear();
            await foreach (var model in models)
            {
                var id = model.Id;
                string displayName = model.LanguageDisplayNames.Values.First();
                if (!string.IsNullOrEmpty(model.DtdlModel))
                {
                    modelsJson.Add(model.DtdlModel);
                }
                twinModelDefs.Add(id, model);
            }
            if (modelsJson.Count > 0)
            {
                buttonGetModels.IsEnabled = false;
            }
            MessageBox.Show($"Got {modelsJson.Count} models.");

            var parser = new ModelParser();
            try
            {
                twinInterfaceDefs.Clear();
                twinModels.Clear();
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
                                twinModels.Add(declInterface);
                                twinInterfaceDefs.Add(declInterface.Id.AbsolutePath, declInterface);
                                break;
                        }
                    }
                }

                PickupRelationships();

                buttonGetModels.IsEnabled = true;
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

        private void PickupRelationships()
        {
            relationshipDefs.Clear();
            foreach(var ifk in twinInterfaceDefs.Keys)
            {
                var interfaceDef = twinInterfaceDefs[ifk];
                foreach(var ck in interfaceDef.Contents.Keys)
                {
                    var c = interfaceDef.Contents[ck];
                    if (c.EntityKind== DTEntityKind.Relationship)
                    {
                        var relDef = (DTRelationshipInfo)c;
                        relationshipDefs.Add(relDef.Name, relDef);
                    }
                }
            }
        }

        string selectedTwinModelId = "";
        DTInterfaceInfo selectedInterfaceDef = null;

        private void lbTwinModels_Selected(object sender, RoutedEventArgs e)
        {
            if (lbTwinModels.SelectedItem == null)
            {
                return;
            }
            selectedInterfaceDef = (DTInterfaceInfo)lbTwinModels.SelectedItem;
            selectedTwinModelId = selectedInterfaceDef.Id.AbsolutePath;

            inComingRelationships.Clear();
            outGoingRelationships.Clear();
            foreach(var ck in selectedInterfaceDef.Contents.Keys)
            {
                var c = selectedInterfaceDef.Contents[ck];
                if (c.EntityKind == DTEntityKind.Relationship)
                {
                    var relDef = (DTRelationshipInfo)c;
                    outGoingRelationships.Add(relDef.Id.AbsolutePath);
                }
                else if (c.EntityKind == DTEntityKind.Property)
                {
                    var propDef = (DTPropertyInfo)c;
                    if (!string.IsNullOrEmpty(propDef.Comment) && propDef.Comment.StartsWith("@"))
                    {
                        string[] colorings = propDef.Comment.Substring(1).Split(new char[] { ',' });
                        foreach (var coloring in colorings)
                        {
                            if (coloring.StartsWith("PR"))
                            {
                                string participatingRel = coloring.Substring(1);
                                if (relationshipDefs.ContainsKey(participatingRel))
                                {
                                    var relDef = (DTRelationshipInfo)relationshipDefs[participatingRel];
                                    inComingRelationships.Add(relDef.Id.AbsolutePath);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
