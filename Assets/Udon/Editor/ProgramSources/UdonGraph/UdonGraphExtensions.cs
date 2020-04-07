using System;
using System.Collections.Generic;
using System.Linq;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;

namespace VRC.Udon.Editor.ProgramSources
{
    internal static class UdonGraphExtensions
    {
        private static readonly Dictionary<string, string> FriendlyNameCache;

        static UdonGraphExtensions()
        {
            FriendlyNameCache = new Dictionary<string, string>();
            StartsWithCache = new Dictionary<(string s, string prefix), bool>();
        }

        public static string FriendlyNameify(this string typeString)
        {
            if (typeString == null)
            {
                return null;
            }
        
            if (FriendlyNameCache.ContainsKey(typeString))
            {
                return FriendlyNameCache[typeString];
            }
            string originalString = typeString;
            typeString = typeString.Replace("Single", "float");
            typeString = typeString.Replace("Int32", "int");
            typeString = typeString.Replace("String", "string");
            typeString = typeString.Replace("VRCUdonCommonInterfacesIUdonEventReceiver", "UdonBehaviour");
            typeString = typeString.Replace("IUdonEventReceiver", "UdonBehaviour");
            typeString = typeString.Replace("Const_VRCUdonCommonInterfacesIUdonEventReceiver", "UdonBehaviour");
            typeString = typeString.Replace("Array", "[]");
            // ReSharper disable once StringLiteralTypo
            if (typeString.Replace("ector", "").Contains("ctor")) //Handle "Vector/vector"
            {
                typeString = typeString.ReplaceLast("ctor", "constructor");    
            }

            if (typeString == "IUdonEventReceiver")
            {
                typeString = "UdonBehaviour";
            }
            FriendlyNameCache.Add(originalString, typeString);
            return typeString;
        }

        private static readonly Dictionary<(string s, string prefix), bool> StartsWithCache;
        public static bool StartsWithCached(this string s, string prefix)
        {
            if (StartsWithCache.ContainsKey((s, prefix)))
            {
                return StartsWithCache[(s, prefix)];
            }
            bool doesStartWith = s.StartsWith(prefix); 
            StartsWithCache.Add((s, prefix), doesStartWith);
            return doesStartWith;
        }
        
        static string UppercaseFirst(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
        
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        
        public static string ReplaceLast(this string source, string find, string replace)
        {
            int place = source.LastIndexOf(find, StringComparison.Ordinal);

            if(place == -1)
                return source;

            string result = source.Remove(place, find.Length).Insert(place, replace);
            return result;
        }
        
        public static UdonNodeSearchMenu.NodeMenuLayer FindLayer(this UdonNodeSearchMenu.NodeMenuLayer layer, string activePath)
        {
            if (string.IsNullOrEmpty(activePath) || activePath == "/")
                return layer;
            return layer.MenuName == activePath
                ? layer
                : layer.SubNodes.First(n => n.MenuName == activePath.Split('/')[0])
                    .FindLayer(string.Join("/", activePath.Split('/').Skip(1).ToArray()));
        }
        
        public static void PopulateNodeMenu(this UdonNodeSearchMenu.NodeMenuLayer nodeLayers)
        {
            List<(string path, UdonNodeDefinition nodeDefinition)> nodePaths = new List<(string path, UdonNodeDefinition nodeDefinition)>();

            foreach (KeyValuePair<string, INodeRegistry> topRegistry in UdonEditorManager.Instance.GetNodeRegistries())
            {
                string topName = topRegistry.Key.Replace("NodeRegistry", "");
                foreach (KeyValuePair<string, INodeRegistry> registry in topRegistry.Value.GetNodeRegistries().OrderBy(s => s.Key))
                {
                    string baseRegistryName = registry.Key.Replace("NodeRegistry", "").FriendlyNameify().ReplaceFirst(topName, "");
                    string registryName = baseRegistryName.UppercaseFirst();
                    if (topName == "Udon" && (registryName == "Event" || registryName == "Type"))
                    {
                        registryName = $"{registryName}s";
                    }
                    if (registryName.EndsWith("[]"))
                    {
                        registryName = $"{registryName.Substring(0, registryName.Length - 2)}/{registryName}";
                    }
                    
                    Dictionary<string, UdonNodeDefinition> baseNodeDefinition = new Dictionary<string, UdonNodeDefinition>();
                    foreach (UdonNodeDefinition nodeDefinition in registry.Value.GetNodeDefinitions().OrderBy(s => UdonNodeSearchMenu.PrettyFullName(s)))
                    {
                        string baseIdentifier = nodeDefinition.fullName;
                        string[] splitBaseIdentifier = baseIdentifier.Split(new[] {"__"}, StringSplitOptions.None);
                        if (splitBaseIdentifier.Length >= 2)
                        {
                            baseIdentifier = $"{splitBaseIdentifier[0]}__{splitBaseIdentifier[1]}";
                        }
                        if (baseNodeDefinition.ContainsKey(baseIdentifier))
                        {
                            continue;
                        }
                        baseNodeDefinition.Add(baseIdentifier, nodeDefinition);
                    }

                    foreach (KeyValuePair<string, UdonNodeDefinition> nodeDefinitionsEntry in baseNodeDefinition)
                    {
                        string nodeName = PrettyBaseName(nodeDefinitionsEntry.Key).ReplaceFirst(baseRegistryName, "");
                        if (nodeName.Contains("."))
                        {
                            nodeName = nodeName.Split('.')[1];
                        }
                        nodeName = nodeName.UppercaseFirst();
                        
                        if(topName == "Udon") 
                        {
                            nodePaths.Add((
                                $"{registryName}/{nodeName}", nodeDefinitionsEntry.Value));
                        }
                        else
                        {
                            nodePaths.Add((
                                $"{topName}/{registryName}/{nodeName}", nodeDefinitionsEntry.Value));
                        }
                    }
                }
            }

            foreach ((string path, UdonNodeDefinition nodeDefinition) item in nodePaths)
            {
                NodeMenuBuilder(item.path, item.nodeDefinition, nodeLayers);
            }
            nodeLayers.MenuName = "";
            
            void NodeMenuBuilder(string path, UdonNodeDefinition nodeDefinition, UdonNodeSearchMenu.NodeMenuLayer container)
            {
                string[] segments = path.Split('/');
                string head = segments[0];
                string[] tail = segments.Skip(1).ToArray();
                if (tail.Length == 0)
                {
                    if (container.SubNodes.Any(n => n.MenuName == head))
                    {
                        container.SubNodes.First(n => n.MenuName == head).NodeDefinition = nodeDefinition;
                    }
                    else
                    {
                        UdonNodeSearchMenu.NodeMenuLayer nLayer = new UdonNodeSearchMenu.NodeMenuLayer();
                        nLayer.MenuName = head;
                        nLayer.NodeDefinition = nodeDefinition;
                        container.SubNodes.Add(nLayer);
                    }
                }
                else
                {
                    string head1 = head;
                    if (container.SubNodes.All(n => n.MenuName != head1))
                    {
                        UdonNodeSearchMenu.NodeMenuLayer nLayer = new UdonNodeSearchMenu.NodeMenuLayer();
                        nLayer.MenuName = head;
                        container.SubNodes.Add(nLayer);
                    }

                    foreach (UdonNodeSearchMenu.NodeMenuLayer layer in container.SubNodes)
                    {
                        if (layer.MenuName == head)
                            NodeMenuBuilder(string.Join("/", tail), nodeDefinition, layer);
                    }
                }
            }
            
            string PrettyBaseName(string baseIdentifier)
            {
                string result = baseIdentifier.Replace("UnityEngine", "").Replace("System", "");
                string[] resultSplit = result.Split(new[] {"__"}, StringSplitOptions.None);
                if (resultSplit.Length >= 2)
                {
                    result = $"{resultSplit[0]}{resultSplit[1]}";    
                }
                result = result.FriendlyNameify();
                result = result.Replace("op_", "");
                result = result.Replace("_", " ");
                return result;
            }
        }
        
    }
}
