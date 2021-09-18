using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Microsoft.ProgramSynthesis.Wrangling.Tree;

namespace CSharpEngine{
     public class APIUsageMiner{

        private string outputPath, clientName, version;
        private List<Class> classes;
        private ChangeSummary cs;
        private BreakingChange breakingChanges;

        public APIUsageMiner(string clientName, List<Class> classes, ChangeSummary cs, BreakingChange breakingChanges, string outputPath)
        {
            this.cs = cs;
            this.classes = classes;
            this.outputPath = outputPath;
            this.breakingChanges = breakingChanges;
            this.clientName = clientName;
        }

        public bool SearchClient(string version)
        {
            // _outputPath = outputPath;
            this.version = version;
            List<RelevantNodes> relevantNodes = new List<RelevantNodes>();

            var modifiedInterfaces = cs.extractModifiedInterfaceName();

            foreach (var modifiedInter in modifiedInterfaces)
            {
                if (version.Equals("new") && !breakingChanges.MatchesWithNewAPI(modifiedInter))
                    continue;
                else if(!breakingChanges.MatchesWithOldAPI(modifiedInter))
                    continue;

                Class refClass = modifiedInter.Item1.class1;
                Method refMethod = modifiedInter.Item2.method1;
                if (version.Equals("new")) {
                    refClass = modifiedInter.Item1.class2;
                    refMethod = modifiedInter.Item2.method2;
                }
                foreach (var cl in classes){
                    // coarsely filter out
                    if (!cl.getSyntax().ToString().Contains(refMethod.methodName) &&
                        !cl.getSyntax().ToString().Contains(refClass.className))
                        continue;
                    relevantNodes.AddRange(SearchForNodes(cl, refClass, refMethod));
                }
            }
            if(relevantNodes.Count() > 0)
                SaveAPIUsages(relevantNodes); 
            return relevantNodes.Count() > 0;
        }

        private List<RelevantNodes> SearchForNodes(Class cl, Class refClass, Method refMethod) {
            List<RelevantNodes> relevantNodes = new List<RelevantNodes>();
            var methodList = new List<SyntaxNode>();
            methodList.AddRange(cl.getSyntax().ChildNodes().OfType<ConstructorDeclarationSyntax>());
            methodList.AddRange(cl.getSyntax().ChildNodes().OfType<MethodDeclarationSyntax>());

            foreach (var method in methodList)
            {
                if (!method.ToString().Contains(refMethod.methodName) &&
                    !method.ToString().Contains(refClass.className))
                    continue;
                var children = method.ChildNodesAndTokens().ToList();
                if (children != null && children.Count != 0)
                {
                    /*if (refMethod == null) {
                        if (MatchingPolice.Contains(method, refClass.className)) {
                            SaveMethod(method, refClass.className);
                            findNode = true;
                        }
                    }*/
                    var instance = MatchingPolice.SearchNode(children, refClass, refMethod, version);
                    if (instance != null) {
                        InvokeType typeInfo = null;
                        if (Config.CompilationMode)
                            typeInfo = InvocationNodeType.GenerateType(instance.AsNode(), version);
                        // save relevant client metadata
                        var rc = new RelevantNodes(refMethod.methodName, "placeholder", clientName, typeInfo, instance.AsNode().ToString());
                        rc.SetSyntaxNode(instance.AsNode());
                        relevantNodes.Add(rc);
                        /*if (Config.CompilationMode)
                            SaveMethod(instance.AsNode(), refMethod.methodName);
                        else
                            SaveMethod(method, refMethod.methodName);*/
                    }
                }
            }
            return relevantNodes;
        }

        private void SaveAPIUsages(List<RelevantNodes> usages) {
            List<int> savedNodes = new List<int>();

            string _outputPath = Path.Combine(outputPath, version + "_usages");
            if (Config.CompilationMode)
                _outputPath = Path.Combine(outputPath, version + "_typed_usages");

            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
            var metadataFile = Path.Combine(_outputPath, "relevant_usage_metadata.json");

            List<RelevantNodes> totalUsages;
            if (File.Exists(metadataFile))
                totalUsages = JsonConvert.DeserializeObject<List<RelevantNodes>>(File.ReadAllText(metadataFile));
            else
                totalUsages = new List<RelevantNodes>();

            int index = totalUsages.Count();
            foreach (var usage in usages) {
                int hashcode = usage.GetHashCode() + 117 * usage.id.GetHashCode();
                if (savedNodes.Contains(hashcode))
                    return;
                else
                    savedNodes.Add(hashcode);

                // save relevant node
                index++;
                var storedName = "relevant_usage_" + index + ".xml";
                var storedPath = Path.Combine(_outputPath, storedName);

                var structNode = Translator.Translate(usage.GetSyntaxNode());
                Translator.storeNode(structNode, storedPath);
                usage.path = storedPath;
            }

            Utils.LogTest("Number of relevant usage: " + usages.Count());
            Utils.LogTest("The mining results are save at " + metadataFile);
            totalUsages.AddRange(usages);
            string json_content = JsonConvert.SerializeObject(totalUsages, Formatting.Indented);
            using (StreamWriter outputFile = new StreamWriter(metadataFile))
                outputFile.Write(json_content);
        }
    }

    public class RelevantNodes {
        public string id;
        public string path;
        public string text;
        public string clientname;
        public InvokeType invocationType;
        private Node StructNode;
        private SyntaxNode syntaxNode;
        public RelevantNodes(string id, string path, string clientname, InvokeType invocationType, string text) { 
            this.id = id;
            this.path = path;
            this.text = text;
            this.clientname = clientname;
            this.invocationType = invocationType;
        }

        public void SetStructNode(Node node) {
            StructNode = node;
        }

        public void SetSyntaxNode(SyntaxNode node)
        {
            syntaxNode = node;
        }

        public Node GetStructNode() => StructNode;

        public SyntaxNode GetSyntaxNode() => syntaxNode;
    }
}