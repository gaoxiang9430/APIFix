using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace CSharpEngine
{
    class RTCompilation
    {
        public List<Compilation> oldCompilations;
        public List<Compilation> newCompilations;

        public List<SyntaxTree> oldSyntexNodes = null;
        public List<SyntaxTree> newSyntexNodes = null;

        private static RTCompilation rtCompilation = null;

        public RTCompilation() {

            var registeredInstance = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault();
            if (registeredInstance == null)
            {
                throw new Exception("No Visual Studio instances found.");
            }
            MSBuildLocator.RegisterInstance(registeredInstance);
            
            // if (oldSlnPath != null)
            //     CompileSolution(oldSlnPath, "old");

            // if (newSlnPath != null)
            //     CompileSolution(newSlnPath, "new");
        }

        private static int RunCommand(string command, string path) {
            var proc1 = new ProcessStartInfo();

            proc1.WorkingDirectory = path;

            proc1.RedirectStandardInput = false;
            proc1.RedirectStandardOutput = true;
            proc1.RedirectStandardError = true;
            proc1.UseShellExecute = false;
            proc1.CreateNoWindow = true;

            proc1.FileName = "cmd.exe";
            proc1.Verb = "runas";
            proc1.Arguments = "/c " + command;
            proc1.WindowStyle = ProcessWindowStyle.Hidden;
            // Console.WriteLine("running command " + command);
            Process P = Process.Start(proc1);
            P.WaitForExit();

            return P.ExitCode;
        }

        private static int CommandCompile(string oldSlnPath)
        {
            var slnPath = Path.GetDirectoryName(oldSlnPath);
            var restoreStatus = RunCommand("dotnet restore", slnPath);
            if (restoreStatus != 0)
                return restoreStatus;
            Console.WriteLine("restore status: " + restoreStatus);

            var buildStatus = RunCommand("MSBuild.exe", slnPath);
            Console.WriteLine("build status: " + buildStatus);
            return buildStatus;
        }

        public static RTCompilation Init() {
            /*var retCode = CommandCompile(oldSlnPath);
            if (retCode != 0)
                return null;
            retCode = CommandCompile(newSlnPath);
            if (retCode != 0)
                return null;*/

            if (rtCompilation == null)
                //rtCompilation = new RTCompilation(oldSlnPath, newSlnPath);
                rtCompilation = new RTCompilation();
            return rtCompilation;
        }

        public static RTCompilation GetRTCompilation()
        {
            if (rtCompilation == null)
                throw new Exception("The RTComilation has not been initialzed yet!!!");
            return rtCompilation;
        }

        public ITypeSymbol GetSemanticType(SyntaxNode node, string version) {
            var semanticModel = GetSemanticModel(node, version);
            if (semanticModel != null) { 
                var type = semanticModel.GetTypeInfo(node).Type;
                if (type != null)
                    return type;
                else
                    return null;
            }
            return null;
        }

        public ISymbol GetSemanticSymbol(SyntaxNode node, string version) {
            var semanticModel = GetSemanticModel(node, version);
            if (semanticModel != null)
                return semanticModel.GetSymbolInfo(node).Symbol;
            return null;
        }
        public SemanticModel GetSemanticModel(SyntaxNode node, string version) {
            List<Compilation> compilations;
            if (version == "new") {
                compilations = newCompilations;
            }
            else {
                compilations = oldCompilations;
            }

            foreach (var compilation in compilations) {
                try {
                    var semanticModel = compilation.GetSemanticModel(node.SyntaxTree);
                    if (semanticModel != null)
                        return semanticModel;
                }
                catch { // (Exception e) {
                    // Console.WriteLine("Exception: " + e.ToString());
                }
            }
            return null;
        }

        public void CompileSolution(string solutionUrl, string version)
        {
            Console.WriteLine("Start compiling the " + version + " project!");
            int success = 0;
            List<Compilation> compilations = new List<Compilation>();
            var syntaxNodes = new List<SyntaxTree>();

            MSBuildWorkspace workspace = MSBuildWorkspace.Create(new Dictionary<string, string>() { { "Configuration", "Debug" }, { "Platform", "Any CPU" } });
            Solution solution = workspace.OpenSolutionAsync(solutionUrl).Result;

            ProjectDependencyGraph projectGraph = solution.GetProjectDependencyGraph();
            
            foreach (ProjectId projectId in projectGraph.GetTopologicallySortedProjects())
            {
                var refers = solution.GetProject(projectId).MetadataReferences;
                Compilation projectCompilation = solution.GetProject(projectId).GetCompilationAsync().Result;

                syntaxNodes.AddRange(projectCompilation.SyntaxTrees.ToList()); 

                if (null != projectCompilation && !string.IsNullOrEmpty(projectCompilation.AssemblyName)) {
                    using (var stream = new MemoryStream())
                    {
                        EmitResult result = projectCompilation.Emit(stream);
                        if (result.Success) {
                            compilations.Add(projectCompilation);

                            /*foreach(var tree in projectCompilation.SyntaxTrees.ToList()){ 
                                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                                var model = projectCompilation.GetSemanticModel(tree);

                                foreach (var invocationSyntax in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                                {
                                    var invokedSymbol = model.GetSymbolInfo(invocationSyntax).Symbol;
                                    if (invokedSymbol == null)
                                        Console.WriteLine("cannot symbol for " + invocationSyntax.ToString());

                                    if (invokedSymbol != null && invokedSymbol.ContainingSymbol.ToString().Contains("SerializingCacheProviderAsync"))
                                    {
                                        Console.WriteLine(invokedSymbol.ContainingSymbol + " => ");
                                        Console.WriteLine(MatchingPolice.RemoveGeneticPara(invokedSymbol.ContainingSymbol.ToString()));
                                        int index = 0;
                                        if(invocationSyntax.ArgumentList != null)
                                        foreach (var arg in invocationSyntax.ArgumentList.Arguments)
                                        {
                                            var type = model.GetTypeInfo(arg.ChildNodes().First()).Type;
                                            if (type != null)
                                            {
                                                Console.WriteLine("argument " + index + " type is " + type);
                                                index++;
                                            }
                                        }
                                    }
                                }
                            }*/

                            success++;
                        } else {
                            var errors = new List<string>();

                            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                                diagnostic.IsWarningAsError ||
                                diagnostic.Severity == DiagnosticSeverity.Error);

                            foreach (Diagnostic diagnostic in failures)
                                errors.Add($"{diagnostic.Id}: {diagnostic.GetMessage()}");

                            //Throw new Exception(String.Join("\n", errors));
                        }
                    }
                }
            }
            Console.WriteLine(success + " / " + projectGraph.GetTopologicallySortedProjects().Count() + " of projects has been successfully compiled!");
            if (version == "new"){
                newCompilations = compilations;
                newSyntexNodes = syntaxNodes;
            }
            else {
                oldCompilations = compilations;
                oldSyntexNodes = syntaxNodes;
            }
        }
    }
}
