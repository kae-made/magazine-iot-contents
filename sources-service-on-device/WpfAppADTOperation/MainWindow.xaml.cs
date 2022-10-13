using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        private ObservableCollection<string> candidateTwinsForRelationship = new ObservableCollection<string>();

        private string currentModelId = "";

        WorkingProgress workingProgress = new WorkingProgress();

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
            lbCandidatesOfLinkTarget.ItemsSource = candidateTwinsForRelationship;

            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string adtInstanceUrl = configuration.GetConnectionString("ADT");
            tbADTUri.Text = adtInstanceUrl;

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
            buttonTwinUpdate.IsEnabled = false;
        }

        string selectedTwinModelId = null;
        private async void lbInterfaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ifInfo = (DTInterfaceInfo)lbInterfaces.SelectedItem;
            currentModelId = ifInfo.Id.AbsolutePath;

            selectedTwinModelId = currentModelId;
            if (!currentModelId.StartsWith("dtmi:"))
            {
                selectedTwinModelId = $"dtmi:{currentModelId}";
            }
            string query = $"SELECT $dtId FROM DIGITALTWINS WHERE IS_OF_MODEL('{selectedTwinModelId}')";
            AsyncPageable<BasicDigitalTwin> queryResult = adtClient.QueryAsync<BasicDigitalTwin>(query);
            twinsForSelectedInterface.Clear();
            await foreach(var twin in queryResult)
            {
                twinsForSelectedInterface.Add(twin.Id);
            }
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
                    if (string.IsNullOrEmpty(tp.Value))
                    {
                        continue;
                    }
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

        private string selectedTwinId = null;
        private Dictionary<string, DTRelationshipInfo> currentRelationships = new Dictionary<string, DTRelationshipInfo>();
        private async void lbTwins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedTwinModelId))
            {
                return;
            }
            selectedTwinId = (string)lbTwins.SelectedItem;
            if (string.IsNullOrEmpty(selectedTwinId))
            {
                return;
            }
            string targetTwinModelId = selectedTwinModelId;
            if (selectedTwinModelId.StartsWith("dtmi:"))
            {
                targetTwinModelId = selectedTwinModelId.Substring("dtmi:".Length);
            }
            var selectedInterfaceInfo = dtInterfaceDefs.Where(item => { return item.Id.AbsolutePath == targetTwinModelId; }).FirstOrDefault();
            if (selectedInterfaceInfo != null)
            {
                SetTwinProperties(selectedInterfaceInfo);
                foreach(var twinInList in twinProperties)
                {
                    twinInList.Registed = false;
                    twinInList.OldValue = "";
                }
            }
            else
            {
                MessageBox.Show("Please select Twin Model Id!");
                return;
            }
            try
            {
                string query = $"SELECT * FROM DIGITALTWINS T WHERE IS_OF_MODEL('{selectedTwinModelId}') AND T.$dtId = '{selectedTwinId}'";
                AsyncPageable<BasicDigitalTwin> queryResult = adtClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var twin in queryResult)
                {
                    foreach (var twinPropKey in twin.Contents.Keys)
                    {
                        var twinPropVal = twin.Contents[twinPropKey];
                        if (twinPropVal is JsonElement)
                        {
                            bool unregisted = true;
                            var twinPropValJE = (JsonElement)twinPropVal;
                            string currentValue = "";
                            if (twinPropValJE.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var field in twinPropValJE.EnumerateObject())
                                {
                                    currentValue = field.Value.ToString();
                                    string fieldKey = $"{twinPropKey}.{field.Name}";
                                    foreach (var propFolder in twinProperties)
                                    {
                                        if (propFolder.Name == fieldKey)
                                        {
                                            propFolder.Value = currentValue;
                                            propFolder.OldValue= currentValue;
                                            propFolder.Registed = true;
                                            break;
                                        }
                                    }

                                }
                            }
                            else if (twinPropValJE.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in twinPropValJE.EnumerateArray())
                                {
                                    if (!string.IsNullOrEmpty(currentValue))
                                    {
                                        currentValue += ",";
                                    }
                                    currentValue += item.GetString();
                                }
                            }
                            else if (twinPropValJE.ValueKind== JsonValueKind.Number)
                            {
                                currentValue = $"{twinPropValJE.GetInt64()}";
                            }
                            else
                            {
                                currentValue = twinPropValJE.GetString();
                            }
                            if (unregisted)
                            {
                                foreach (var propFolder in twinProperties)
                                {
                                    if (propFolder.Name == twinPropKey)
                                    {
                                        propFolder.Value = currentValue;
                                        propFolder.OldValue = currentValue;
                                        propFolder.Registed = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                buttonTwinUpdate.IsEnabled = true;

                relationshipsForSelectedInterface.Clear();
                currentRelationships.Clear();
                foreach(var ck in selectedInterfaceInfo.Contents.Keys)
                {
                    var c = selectedInterfaceInfo.Contents[ck];
                    if (c.EntityKind== DTEntityKind.Relationship)
                    {
                        var relInfo = (DTRelationshipInfo)c;
                        relationshipsForSelectedInterface.Add(relInfo.Id.AbsolutePath);
                        currentRelationships.Add(relInfo.Id.AbsolutePath, relInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void buttonTwinUpdate_Click(object sender, RoutedEventArgs e)
        {
            string targetTwinModelId = selectedTwinModelId;
            if (selectedTwinModelId.StartsWith("dtmi:"))
            {
                targetTwinModelId = selectedTwinModelId.Substring("dtmi:".Length);
            }
            var selectedInterfaceInfo = dtInterfaceDefs.Where(item => { return item.Id.AbsolutePath == targetTwinModelId; }).FirstOrDefault();
            if (selectedInterfaceInfo != null)
            {
                try
                {
                    var updateTwinData = new JsonPatchDocument();
                    foreach (var ck in selectedInterfaceInfo.Contents.Keys)
                    {
                        var cv = selectedInterfaceInfo.Contents[ck];
                        object editedValue = null;
                        bool registed = false;
                        bool updated = false;
                        if (cv.EntityKind == DTEntityKind.Property)
                        {
                            var propInfo = (DTPropertyInfo)cv;
                            if (propInfo.Schema.EntityKind == DTEntityKind.Object)
                            {
                                var fieledFolders = twinProperties.Where(item => { return item.Name.StartsWith(ck); }).ToList();
                                if (fieledFolders.Count > 0)
                                {
                                    var fieldValues = new Dictionary<string, object>();
                                    foreach (var fieldFolder in fieledFolders)
                                    {
                                        if (!string.IsNullOrEmpty(fieldFolder.Value))
                                        {
                                            object fieldValue = fieldFolder.GetDataTypedValue();
                                            string fieldName = fieldFolder.Name.Substring(fieldFolder.Name.LastIndexOf(".") + 1);
                                            registed = fieldFolder.Registed;
                                            if (fieldValue != null && $"{fieldValue}" != fieldFolder.OldValue)
                                            {
                                                updated = true;
                                                fieldValues.Add(fieldName, fieldValue);
                                            }
                                            // Dictionary 形式ではなくて、"PreferedEnvironment/Temperature" か？
                                        }
                                    }
                                    if (fieldValues.Count > 0)
                                    {
                                        editedValue = fieldValues;
                                    }
                                }
                            }
                            else
                            {
                                var fieldFolder = twinProperties.Where(item => { return item.Name == ck; }).FirstOrDefault();
                                if (fieldFolder != null)
                                {
                                    editedValue = fieldFolder.GetDataTypedValue();
                                    registed = fieldFolder.Registed;
                                    if (fieldFolder.OldValue != $"{editedValue}")
                                    {
                                        updated = true;
                                    }
                                }
                            }
                        }

                        if (updated)
                        {
                            if (registed)
                            {
                                updateTwinData.AppendReplace($"/{ck}", editedValue);

                            }
                            else
                            {
                                updateTwinData.AppendAdd($"/{ck}", editedValue);
                            }
                        }
                    }
                    await adtClient.UpdateDigitalTwinAsync(selectedTwinId, updateTwinData);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private DTRelationshipInfo selectedRelationshipInfo = null;
        private Dictionary<string, BasicRelationship> relationshipsForTargetTwin = new Dictionary<string, BasicRelationship>();
        private async void lbRelationships_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbRelationships.SelectedItem == null)
            {
                candidateTwinsForRelationship.Clear();
                return;
            }
            string relId = lbRelationships.SelectedItem.ToString();
            selectedRelationshipInfo = currentRelationships[relId];
            string targetTwinModelId = selectedRelationshipInfo.Target.AbsolutePath;
            if (!targetTwinModelId.StartsWith("dtmi:"))
            {
                targetTwinModelId = $"dtmi:{targetTwinModelId}";
            }
            try
            {
                linkedTwins.Clear();
                relationshipsForTargetTwin.Clear();
                AsyncPageable<BasicRelationship> linkedRelationships = adtClient.GetRelationshipsAsync<BasicRelationship>(selectedTwinId, selectedRelationshipInfo.Name);
                await foreach (var linkedRelationship in linkedRelationships)
                {
                    linkedTwins.Add(linkedRelationship.TargetId);
                    relationshipsForTargetTwin.Add(linkedRelationship.TargetId, linkedRelationship);
                }

                bool notEnough = true;
                if (selectedRelationshipInfo.MaxMultiplicity != null && selectedRelationshipInfo.MaxMultiplicity.Value <= linkedTwins.Count)
                {
                    notEnough = false;
                }
                if (notEnough)
                {
                    string query = $"SELECT $dtId FROM DIGITALTWINS WHERE IS_OF_MODEL('{targetTwinModelId}')";
                    AsyncPageable<BasicDigitalTwin> queryResult = adtClient.QueryAsync<BasicDigitalTwin>(query);
                    candidateTwinsForRelationship.Clear();
                    await foreach (var twin in queryResult)
                    {
                        var registedTwins = linkedTwins.Where(id => { return id == twin.Id; });
                        if (registedTwins.Count() == 0)
                        {
                            candidateTwinsForRelationship.Add(twin.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string selectedTwinIdForLinking = "";
        private void lbCandidatesOfLinkTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbCandidatesOfLinkTarget.SelectedItem == null)
            {
                return;
            }
            selectedTwinIdForLinking = (string)lbCandidatesOfLinkTarget.SelectedItem;
            buttonLink.IsEnabled = true;
        }

        private string selectedTwinIdForUnlinking = "";
        private void lbLinkedTwins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbLinkedTwins.SelectedItem == null)
            {
                return;
            }
            selectedTwinIdForUnlinking = (string)lbLinkedTwins.SelectedItem;
            buttonUnlink.IsEnabled = true;
        }

        private async void buttonLink_Click(object sender, RoutedEventArgs e)
        {
            var relationship = new BasicRelationship()
            {
                TargetId = selectedTwinIdForLinking,
                Name = selectedRelationshipInfo.Name
            };
            try
            {
                string relId = $"{selectedTwinId}-{selectedRelationshipInfo.Id.AbsolutePath}-{selectedTwinIdForLinking}";
              
                var selectedIfDef = (DTInterfaceInfo)lbInterfaces.SelectedItem;
                var sourceTwinContentsUpdate = new JsonPatchDocument();
                var formalizedPropsForRelationship = GetRelationshipFormalizedProperties(selectedIfDef, selectedRelationshipInfo);
                foreach (var prop in formalizedPropsForRelationship)
                {
                    var editingTwinProp = twinProperties.Where(p => { return p.PropertyInfo.Id.AbsolutePath == prop.Id.AbsolutePath; }).FirstOrDefault();
                    if (editingTwinProp != null)
                    {
                        editingTwinProp.Value = selectedTwinIdForLinking;
                        if (editingTwinProp.Registed)
                        {
                            sourceTwinContentsUpdate.AppendReplace($"/{editingTwinProp.Name}", selectedTwinIdForLinking);
                        }
                        else
                        {
                            sourceTwinContentsUpdate.AppendAdd($"/{editingTwinProp.Name}", selectedTwinIdForLinking);
                            editingTwinProp.Registed = true;
                        }
                        break;
                    }
                }
                await adtClient.CreateOrReplaceRelationshipAsync<BasicRelationship>(selectedTwinId, relId, relationship);
                await adtClient.UpdateDigitalTwinAsync(selectedTwinId, sourceTwinContentsUpdate);

                MessageBox.Show($"Linked {relId}");

                buttonLink.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private List<DTPropertyInfo> GetRelationshipFormalizedProperties(DTInterfaceInfo ifDef, DTRelationshipInfo relDef)
        {
            List<DTPropertyInfo> result = new List<DTPropertyInfo>();

            string relName = relDef.Name.Substring(0, relDef.Name.LastIndexOf("_"));
            foreach (var ck in ifDef.Contents.Keys)
            {
                var c = ifDef.Contents[ck];
                if (c.EntityKind == DTEntityKind.Property)
                {
                    var propDef = (DTPropertyInfo)c;
                    if ((!string.IsNullOrEmpty(propDef.Comment)) && propDef.Comment.StartsWith("@") && propDef.Comment.IndexOf("R") > 0)
                    {
                        string[] colorings = propDef.Comment.Substring(1).Split(new char[] { ',' });
                        foreach (var coloring in colorings)
                        {
                            if (coloring == relName)
                            {
                                result.Add(propDef);
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private async void buttonUnlink_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTwinIdForUnlinking == null)
            {
                return;
            }
            var relationship = relationshipsForTargetTwin[selectedTwinIdForUnlinking];
            try
            {
                var selectedIfDef = (DTInterfaceInfo)lbInterfaces.SelectedItem;
                var sourceTwinContentsUpdate = new JsonPatchDocument();
                var formalizedPropsForRelationship = GetRelationshipFormalizedProperties(selectedIfDef, selectedRelationshipInfo);
                foreach(var propDef in formalizedPropsForRelationship)
                {
                    sourceTwinContentsUpdate.AppendRemove($"/{propDef.Name}");
//                    sourceTwinContentsUpdate.AppendReplace($"/{propDef.Name}", "");
                }
                await adtClient.UpdateDigitalTwinAsync(selectedTwinId, sourceTwinContentsUpdate);
                await adtClient.DeleteRelationshipAsync(selectedTwinId, relationship.Id);
                linkedTwins.Remove(selectedTwinIdForUnlinking);
                buttonUnlink.IsEnabled = false;
                MessageBox.Show($"Unlinked - {relationship.Id}");
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
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
