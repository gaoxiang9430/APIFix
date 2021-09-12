using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Microsoft.ProgramSynthesis.Wrangling.Tree;

namespace CSharpEngine{
     public class NodeFilter{

        public static List<int> savedMethods = new List<int>();
        private static string _version;
        private static string _outputPath;
        
        public static bool FilterClient(string clientName, List<Class> classes, string outputPath, ChangeSummary cs, string version, List<string> interestingAPI)
        {
            _outputPath = outputPath;
            _version = version;
            bool findNode = false;
            var references = cs.extractModifiedInterfaceName();

            foreach (var reference in references)
            {
                Class refClass;
                Method refMethod;
                if (version.Equals("new"))
                {
                    refClass = reference.Item1.class2;
                    refMethod = reference.Item2.method2;
                }
                else
                {
                    refClass = reference.Item1.class1;
                    refMethod = reference.Item2.method1;
                }

                if (refClass == null || refMethod == null)
                    continue;

                if (!interestingAPI.Contains(refMethod.methodName))
                    continue;
                
                // DELETE ME --- filter out too many noices
                if (refMethod.methodName == "Execute" && refMethod.argList.Count < 2)
                    continue;

                foreach (var cl in classes){
                    // coarsely filter out
                    if (!cl.getSyntax().ToString().Contains(refMethod.methodName) &&
                        !cl.getSyntax().ToString().Contains(refClass.className))
                        continue;
                    if (fineGrainedCheck(clientName, cl, refClass, refMethod, version))
                        findNode = true;
                }
            }
            return findNode;
        }

        private static bool fineGrainedCheck(string clientName, Class cl, Class refClass, Method refMethod, string version) {
            bool findNode = false;
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
                    if (refMethod == null) {
                        if (MatchingPolice.Contains(method, refClass.className)) {
                            SaveMethod(clientName, method, refClass.className, version);
                            findNode = true;
                        }
                    }
                    else {
                        // var instance = DeepContains(children, refClass, refMethod);
                        var instance = MatchingPolice.DeepContains(children, refClass, refMethod, version);
                        if (instance != null) {
                            if (Config.CompilationMode)
                                SaveMethod(clientName, instance.AsNode(), refMethod.methodName, version);
                            else
                                SaveMethod(clientName, method, refMethod.methodName, version);
                            // Console.WriteLine("find usage of " + refMethod.methodName + " in client: " + clientName);
                            findNode = true;
                        }
                    }
                }
            }
            return findNode;
        }

        // check whether a node include a method invocation by referring to semantic model
        public static SyntaxNodeOrToken DeepContains(List<SyntaxNodeOrToken> nodes, Class refClass, Method refMethod)
        {
            var methodName = refMethod.methodName;
            var className = refClass.GetSignature();
            if (refMethod.GetThisName() != null)
                className = refMethod.GetThisName();
            var minArgNum = refMethod.argList.Select(e => !e.Item2).ToList().Count;
            var maxArgNum = refMethod.argList.Count;
            
            foreach (var node in nodes)
            {
                if (node.IsNode)
                {
                    foreach (var invokeSyntax in node.AsNode().DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        if (invokeSyntax.ArgumentList != null &&
                            (invokeSyntax.ArgumentList.Arguments.Count < minArgNum || invokeSyntax.ArgumentList.Arguments.Count > maxArgNum))
                            continue;
                        
                        var children = invokeSyntax.Expression.ChildNodesAndTokens();
                        if (children.Count > 0 && MatchingPolice.Contains(children[children.Count - 1], methodName))
                            return invokeSyntax;

                    }
                    foreach (var invokeSyntax in node.AsNode().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                    {
                        if (invokeSyntax.ArgumentList != null &&
                            (invokeSyntax.ArgumentList.Arguments.Count < minArgNum || invokeSyntax.ArgumentList.Arguments.Count > maxArgNum))
                            continue;
                        if (MatchingPolice.Contains(invokeSyntax.ChildNodes().FirstOrDefault(), methodName))
                            return invokeSyntax;
                    }
                }
            }
            return null;
        }

        private static void SaveMethod(string clientName, SyntaxNode node, string reference, string version) {
            int hashcode = node.GetHashCode() + 117 * reference.GetHashCode();
            if (savedMethods.Contains(hashcode))
                return;
            else
                savedMethods.Add(hashcode);
            string metadataPath = Path.Combine(_outputPath, _version + "_relevant_client");
            if(Config.CompilationMode) 
                metadataPath = Path.Combine(_outputPath, _version + "_typed_relevant_client");
            
            if (!Directory.Exists(metadataPath))
                Directory.CreateDirectory(metadataPath);
            var metadataFile = Path.Combine(metadataPath, "relevant_client_metadata.json");
            List<RelevantClient> relevantClients;
            if (File.Exists(metadataFile))
                relevantClients = JsonConvert.DeserializeObject<List<RelevantClient>>(File.ReadAllText(metadataFile));
            else
                relevantClients = new List<RelevantClient>();

            // save relevant node
            var storedName = "relevant_client_" + (relevantClients.Count() + 1) + ".xml";
            var storedPath = Path.Combine(metadataPath, storedName);
            var structNode = Translator.Translate(node);
            Translator.storeNode(structNode, storedPath);

            InvokeType typeInfo = null;
            if (Config.CompilationMode)
                typeInfo = InvocationNodeType.GenerateType(node, version);
            // save relevant client metadata
            var rc = new RelevantClient(reference, storedName, node.ToString(), clientName, typeInfo);
            relevantClients.Add(rc);
            string json_content = JsonConvert.SerializeObject(relevantClients, Formatting.Indented);
            using (StreamWriter outputFile = new StreamWriter(metadataFile))
                outputFile.Write(json_content);
        }

        public static List<string> loadInterestingAPI(string library, string source, string target, string version) {
            var metadataFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "benchmark", "interesting_api.json");
            List<InterestingApi> interestingApis = JsonConvert.DeserializeObject<List<InterestingApi>>(File.ReadAllText(metadataFile));
            foreach (var interestingApi in interestingApis) {
                if (interestingApi.library.Equals(library) && interestingApi.source.Equals(source) && interestingApi.target.Equals(target)) {
                    if (version.Equals("old"))
                        return interestingApi.old_apis;
                    else
                        return interestingApi.new_apis;
                }
            }
            return new List<string>();
        }
    }

    public class InterestingApi {
        public string library;
        public string source;
        public string target;
        public List<string> old_apis;
        public List<string> new_apis;

        public InterestingApi(string library, string source, string target, List<string> old_apis, List<string> new_apis) {
            this.library = library;
            this.source = source;
            this.target = target;
            this.old_apis = old_apis;
            this.new_apis = new_apis;
        }
    }


    public class RelevantClient {
        public string reference;
        public string path;
        public string text;
        public string clientname;
        public InvokeType invocationType;
        private Node StructNode;
        public RelevantClient(string reference, string path, string text, string clientname, InvokeType invocationType) { 
            this.reference = reference;
            this.path = path;
            this.text = text;
            this.clientname = clientname;
            this.invocationType = invocationType;
        }

        public void SetStructNode(Node node) {
            StructNode = node;
        }

        public Node GetStructNode() => StructNode;
    }
}