using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAppDTDLParser
{
    public class DTDLDecl
    {
        private List<string> extends = new List<string>();
        private List<string> properties = new List<string>();
        private List<string> telemetries = new List<string>();
        private List<string> commands = new List<string>();
        private List<(string name, string target)> relationships = new List<(string name, string target)>();
        public string Name { get; set; }
        public string Id { get; set; }
        public List<string> Extends { get { return extends; } }
        public List<string> Properties { get { return properties; } }
        public List<string> Telemetries { get { return telemetries; } }
        public List<string> Commands { get { return commands; } }
        public List<(string name, string target)> Relationships { get { return relationships; } }

        public void Resolve(DTInterfaceInfo dtdlDecl)
        {
            Id = dtdlDecl.Id.AbsolutePath;
            foreach (var ifName in dtdlDecl.DisplayName)
            {
                Name = ifName.Value;
                break;
            }
            foreach(var ext in dtdlDecl.Extends)
            {
                extends.Add(ext.Id.AbsolutePath);
            }
            foreach( var contentKey in dtdlDecl.Contents.Keys)
            {
                var contentDecl = dtdlDecl.Contents[contentKey];
                switch (contentDecl.EntityKind)
                {
                    case DTEntityKind.Property:
                        var propDecl = (DTPropertyInfo)contentDecl;
                        properties.Add(propDecl.Name);
                        break;
                    case DTEntityKind.Relationship:
                        var relDecl = (DTRelationshipInfo)contentDecl;
                        relationships.Add((name:relDecl.Id.AbsolutePath,target:relDecl.Target.AbsolutePath));
                        break;
                    case DTEntityKind.Telemetry:
                        var telmDecl = (DTTelemetryInfo)contentDecl;
                        var telemetryDecl = (DTTelemetryInfo)contentDecl;
                        telemetries.Add(telemetryDecl.Name);
                        break;
                    case DTEntityKind.Command:
                        var commandDecl = (DTCommandInfo)contentDecl;
                        commands.Add(commandDecl.Name);
                        break;
                }
            }
        }

        public TreeViewData GetTreeViewData()
        {
            var result = new TreeViewData() { Name = Id, Children = new ObservableCollection<TreeViewData>() };
            if (extends.Count > 0)
            {

                var extendsTVD = new TreeViewData() { Name="extends", Children = new ObservableCollection<TreeViewData>() };
                foreach(var ext in extends)
                {
                    extendsTVD.Children.Add(new TreeViewData() { Name = ext });
                }
                result.Children.Add(extendsTVD);
            }
            if (properties.Count > 0)
            {
                var propsTVD = new TreeViewData() { Name = "properties", Children = new ObservableCollection<TreeViewData>() };
                foreach(var prop in properties)
                {
                    propsTVD.Children.Add(new TreeViewData() { Name = prop });
                }
            }
            if (relationships.Count> 0)
            {
                var relsTVD = new TreeViewData() { Name = "relationships", Children = new ObservableCollection<TreeViewData>() };
                foreach(var rel in relationships)
                {
                    relsTVD.Children.Add(new TreeViewData() { Name = $"{rel.name} -> {rel.target}" });
                }
            }
            if (telemetries.Count > 0)
            {
                var telsTVD = new TreeViewData() { Name="telemetries", Children= new ObservableCollection<TreeViewData>() };
                foreach(var tel in telemetries)
                {
                    telsTVD.Children.Add(new TreeViewData() { Name = tel, Children = new ObservableCollection<TreeViewData>() });
                }
            }
            if (commands.Count > 0)
            {
                var cmdsTVD = new TreeViewData() { Name = "commands", Children = new ObservableCollection<TreeViewData>() };
                foreach(var cmd in commands)
                {
                    cmdsTVD.Children.Add(new TreeViewData() { Name = cmd, Children = new ObservableCollection<TreeViewData>() });
                }
            }

            return result;
        }
    }
}
