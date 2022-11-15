using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        WorkingProgress workingProgress = new WorkingProgress();

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

            this.pbProcessModeling.DataContext = workingProgress;
            this.tbProcessing.DataContext = workingProgress;

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

            try
            {
                pbProcessModeling.Visibility = Visibility.Visible;
                tbProcessing.Visibility = Visibility.Visible;
                workingProgress.Processing = "Getting Models...";
                workingProgress.Progress = 0;
                var cancelationSource = new CancellationTokenSource();
                await Task.Factory.StartNew(async () =>
                {
                    await Dispatcher.Invoke(async () =>
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
                });

                buttonGetModels.IsEnabled = false;
                AsyncPageable<DigitalTwinsModelData> models = adtClient.GetModelsAsync(new GetModelsOptions() { IncludeModelDefinition = true });

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
                var parser = new ModelParser();
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

                cancelationSource.Cancel();
                tbProcessing.Visibility = Visibility.Hidden;
                pbProcessModeling.Visibility = Visibility.Hidden;

                MessageBox.Show($"Got {modelsJson.Count} models.");

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

        DTInterfaceInfo selectedTwinModel = null;
        private void lbTwinModels_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (lbTwinModels.SelectedItem == null)
            {
                return;
            }
            selectedTwinModel = (DTInterfaceInfo)lbTwinModels.SelectedItem;
            string twinModelId = selectedTwinModel.Id.AbsolutePath;
            if (!twinModelId.StartsWith("dtmi:"))
            {
                twinModelId = $"dtmi:{twinModelId}";
            }
            tbTwinQuery.Text = $"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('{twinModelId}') ";

            buttonExecTwinQuery.IsEnabled = true;

            // Show outgoing relationship list
            outGoingRelationships.Clear();
            foreach(var ck in selectedTwinModel.Contents.Keys)
            {
                var c = selectedTwinModel.Contents[ck];
                if (c.EntityKind== DTEntityKind.Relationship)
                {
                    outGoingRelationships.Add(c.Id.AbsolutePath);
                }
            }

            // Show incoming relationship list
            inComingRelationships.Clear();
            foreach (var ck in selectedTwinModel.Contents.Keys)
            {
                var c = selectedTwinModel.Contents[ck];
                if (c.EntityKind == DTEntityKind.Property)
                {
                    var propInfo = (DTPropertyInfo)c;
                    if (!string.IsNullOrEmpty(propInfo.Comment) && propInfo.Comment.StartsWith("@"))
                    {
                        string[] colorings = propInfo.Comment.Substring(1).Split(new char[] { ',' });
                        foreach(var coloring in colorings)
                        {
                            if (coloring.StartsWith("P"))
                            {
                                string partRel = coloring.Substring(1);
                                if (relationshipDefs.ContainsKey(partRel))
                                {
                                    inComingRelationships.Add(relationshipDefs[partRel].Id.AbsolutePath);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void buttonExecTwinQuery_Click(object sender, RoutedEventArgs e)
        {
            if (lbTwinModels.SelectedItem == null)
            {
                MessageBox.Show("Please select Twin Model and write condition");
                return;
            }
            twinsForResultOfQueryForTwin.Clear();
            string query = tbTwinQuery.Text;
            try
            {
                AsyncPageable<BasicDigitalTwin> queryResult = adtClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var twin in queryResult)
                {
                    twinsForResultOfQueryForTwin.Add(twin.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CheckRelationshipTraversePreConditon()
        {
            if ((lbInComingRelationships.SelectedItem !=null || lbOutgoingRelationships.SelectedItem!=null) && lbTwins.SelectedItem != null)
            {
                buttonTraverse.IsEnabled = true;
                cbUseQueryForTrvs.IsEnabled = true;
            }
            else
            {
                buttonTraverse.IsEnabled = false;
                cbUseQueryForTrvs.IsEnabled = false;
            }
        }

        string selectedTwinId = "";
        string selectedInCommingRelName = "";
        string selectedOutGoingRelName = "";
        private void lbOutgoingRelationships_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbOutgoingRelationships.SelectedItem == null)
            {
                return;
            }
            CheckRelationshipTraversePreConditon();
            selectedOutGoingRelName = GetRelNameByRelId( (string)lbOutgoingRelationships.SelectedItem);
            selectedInCommingRelName = "";
            lbInComingRelationships.SelectedItem = null;
            SetRelationshipTraverseQuery();
        }

        private void lbInComingRelationships_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbInComingRelationships.SelectedItem == null)
            {
                return;
            }
            CheckRelationshipTraversePreConditon();
            selectedInCommingRelName = GetRelNameByRelId((string)lbInComingRelationships.SelectedItem);
            selectedOutGoingRelName = "";
            lbOutgoingRelationships.SelectedItem = null;
            SetRelationshipTraverseQuery();
        }

        private string GetRelNameByRelId(string relId)
        {
            var relDef = relationshipDefs.Values.Where((rel) => { return rel.Id.AbsolutePath == relId; }).FirstOrDefault();
            string relName = "";
            if (relDef != null)
            {
                relName = relDef.Name;
            }
            return relName;
        }

        private void lbTwins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckRelationshipTraversePreConditon();
            selectedTwinId= (string)lbTwins.SelectedItem;
        }

        private async void buttonTraverse_Click(object sender, RoutedEventArgs e)
        {
            twinsForResultOfQueryForRelationship.Clear();
            try
            {
                if (cbUseQueryForTrvs.IsChecked == true)
                {
                    if (!string.IsNullOrEmpty(tbTraverseQuery.Text))
                    {
                        AsyncPageable<BasicDigitalTwin> queryResult = adtClient.QueryAsync<BasicDigitalTwin>(tbTraverseQuery.Text);
                        await foreach(var twin in queryResult)
                        {
                            var content = (JsonElement)twin.Contents[currentTwinKeyword];
                            var twinId = content.GetProperty("$dtId");
                            if (twinId.ValueKind== JsonValueKind.String)
                            {
                                twinsForResultOfQueryForRelationship.Add(twinId.GetString());
                            }
                            var json = Newtonsoft.Json.JsonConvert.DeserializeObject(content.GetRawText());
                            var jsonObject = (JObject)json;
                            foreach(var p in jsonObject)
                            {
                                string k = p.Key;
                                object v = p.Value;
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(selectedOutGoingRelName))
                    {
                        AsyncPageable<BasicRelationship> linkedRelationships = adtClient.GetRelationshipsAsync<BasicRelationship>(selectedTwinId, selectedOutGoingRelName);
                        await foreach (var linkedRelationship in linkedRelationships)
                        {
                            twinsForResultOfQueryForRelationship.Add(linkedRelationship.TargetId);
                        }
                    }
                    else
                    {
                        // Incoming
                        AsyncPageable<IncomingRelationship> incommingLinks = adtClient.GetIncomingRelationshipsAsync(selectedTwinId);
                        await foreach (var incomingLink in incommingLinks)
                        {
                            if (!string.IsNullOrEmpty(selectedInCommingRelName) && selectedInCommingRelName == incomingLink.RelationshipName)
                            {
                                twinsForResultOfQueryForRelationship.Add(incomingLink.SourceId);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string GetDTDLId(string id)
        {
            string result = id;
            if (!id.StartsWith("dtmi:"))
            {
                result = $"dtmi:{id}";
            }
            return result; 
        }

        private void cbUseQueryForTrvs_Checked(object sender, RoutedEventArgs e)
        {
            SetRelationshipTraverseQuery();
        }

        string currentTwinKeyword = "";
        private void SetRelationshipTraverseQuery()
        {
            tbTraverseQuery.Text = "";
            if (cbUseQueryForTrvs.IsChecked == true)
            {
                string srcId = selectedTwinId;
                if (!string.IsNullOrEmpty(selectedOutGoingRelName))
                {
                    currentTwinKeyword = "target";
                    // Outgoing Relationship Traverse
                    tbTraverseQuery.Text = $"SELECT {currentTwinKeyword} FROM DIGITALTWINS source JOIN {currentTwinKeyword} RELATED source.{selectedOutGoingRelName} WHERE source.$dtId = '{selectedTwinId}'";
                }
                if (!string.IsNullOrEmpty(selectedInCommingRelName))
                {
                    currentTwinKeyword = "source";
                    // Incoming Relationship Traverse
                    tbTraverseQuery.Text = $"SELECT {currentTwinKeyword} FROM DIGITALTWINS {currentTwinKeyword} JOIN target RELATED {currentTwinKeyword}.{selectedInCommingRelName} WHERE target.$dtId = '{selectedTwinId}'";
                }
            }
        }

    }

    public class WorkingProgress : INotifyPropertyChanged
    {
        private string processing;
        private int progress;

        public string Processing
        {
            get { return processing; }
            set
            {
                processing = value;
                OnChanged("Processing");
            }
        }
        public int Progress
        {
            get { return progress; }
            set
            {
                progress = value;
                OnChanged("Progress");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }

}
