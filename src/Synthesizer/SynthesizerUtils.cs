using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSharpEngine;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using Newtonsoft.Json;
using System.Xml.Linq;
using System;

namespace Synthesizer
{
    class SynthesizerUtils
    {
        public static List<Edit> LoadEdit(string editPath)
        {
            var metadataFile = Path.Combine(editPath, "edit_metadata.json");
            var relevantEdits = JsonConvert.DeserializeObject<List<Edit>>(File.ReadAllText(metadataFile));
            relevantEdits = relevantEdits.Where(e => (e.inputPath!=null && e.outputPath!=null)).ToList();

            foreach (var e in relevantEdits)
            {
                var oldNode = loadNode(Path.Combine(editPath, e.inputPath));
                var newNode = loadNode(Path.Combine(editPath, e.outputPath));
                e.SetStructureNode(oldNode, newNode);
            }
            relevantEdits = relevantEdits.Where(e => (e.GetNewStructNode() != null && e.GetOldStructNode() != null)).ToList();
            return relevantEdits;
        }

        public static List<RelevantClient> LoadClientUsage(string usagePath, string targetAPI, int n = 0) {
            if (Directory.Exists(usagePath))
            {
                var metadataFile = Path.Combine(usagePath, "relevant_client_metadata.json");
                List<RelevantClient> relevantClients = JsonConvert.DeserializeObject<List<RelevantClient>>(File.ReadAllText(metadataFile));

                var relevantUsages = relevantClients.Where(e => e.reference.Equals(targetAPI)).ToList();
                if (n > 0) {
                    relevantUsages = relevantUsages.GetRange(0, Math.Min(n, relevantUsages.Count()));
                }

                // set structure node for old/new usages
                foreach (var e in relevantUsages)
                {
                    var node = loadNode(Path.Combine(usagePath, e.path));
                    e.SetStructNode(node);
                }
                return relevantUsages;
            }
            else
            {
                Global.Log("The old usage path does not exist!");
                return new List<RelevantClient>();
            }
        }

        public static StructNode loadNode(string fileName)
        {
            if (!File.Exists(fileName))
                return null;
            var xmlNode = XElement.Load(fileName);
            return StructNode.DeserializeFromXml(xmlNode);
        }
    }
}
