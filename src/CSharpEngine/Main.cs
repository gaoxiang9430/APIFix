using System.Linq;
using System.IO;
using CommandLine;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace CSharpEngine{

    public class MainEntry {
        public static string benchmarkPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "benchmark");
        public static string minerPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "miner");
        public static string libraryName, clientName, oldLibVersion, newLibVersion;
        public static string oldLibPath, newLibPath;
        public static string oldClientPath, newClientPath;
        public static string outputPath;
        public static string oldSlnPath, newSlnPath;
        public static string targetUsagesVersion = null;
        public static string configurationFile = null;

        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('m', "oldLib", Required = true, HelpText = "The old version of library.")]
            public string OldLib { get; set; }

            [Option('n', "newLib", Required = true, HelpText = "The new version of library.")]
            public string NewLib { get; set; }

            [Option('l', "libraryName", Required = true, HelpText = "The name of library.")]
            public string LibraryName { get; set; }

            [Option('f', "configurationFile", Required = true, HelpText = "The path to the metadata file.")]
            public string configurationFile { get; set; }

            [Option('i', Required = false, HelpText = "Extract relevent edit from library update itself.")]
            public bool Itself { get; set; }

            [Option('s', "oldClient", Required = false, HelpText = "The old version of client.")]
            public string OldClient { get; set; }

            [Option('t', "newClient", Required = false, HelpText = "The new version of client.")]
            public string NewClient { get; set; }

            [Option('c', "clientName", Required = false, HelpText = "The name of the client.")]
            public string ClientName { get; set; }

            [Option('p', "sln", Required = false, HelpText = "The path to the sln file.")]
            public string SlnPath { get; set; }

            [Option('z', Required = false, HelpText = "Extract old/new usages.")]
            public string targetUsagesVersion { get; set; }

            [Option('y', Required = false, HelpText = "Compliation mode")]
            public bool CompilatioMode { get; set; }
        }


        public static int Main(string[] args)
        {
            ParseArgs(args);

            // Utils.LogTest("Generating change summary...");
            var changeSummaryOutput = Path.Combine(outputPath, "changesummary.json");
            var cs = ChangeSummary.CreateChangeSummary(oldLibPath, newLibPath, changeSummaryOutput);

            var breakingChanges = BreakingChange.loadInterestingAPI(Path.Join(minerPath, configurationFile));
            // mining API usages
            if (targetUsagesVersion != null){
                // filter out the irrelevant edit
                var oldClasses = ExtractClasses(targetUsagesVersion);
                Utils.LogTest("Extracting usage of API from client...");
                if (configurationFile == null) { 
                    Utils.LogTest("Please specify the configuration file.");
                    return 1;
                }
                var miner = new APIUsageMiner(clientName, oldClasses, cs, breakingChanges, outputPath);
                var findRelevantClient = miner.SearchClient(targetUsagesVersion);
                return findRelevantClient ? 0 : 1;
            }
            else { // mining human adaptations
                // Utils.LogTest("Calulating the matched classes/methods between two versions...");
                var classes1 = ExtractClasses("old");
                var classes2 = ExtractClasses("new");
                List<MatchedClass> matchedClasses = ClassExtractor.CalculateMatchedClass(classes1, classes2);

                var miner = new EditMiner(matchedClasses, cs, breakingChanges, outputPath);
                Utils.LogTest("Extract the edits that are relavent to the library update...");
                var findRelevantEdits = miner.SearchEdits();
                return findRelevantEdits ? 0 : 1;
            }
        }

        public static void ParseArgs(string[] args){
            string oldClientVersion = null, newClientVersion = null;
            string slnPath = "";
            bool verbose = false, extractlibraryEdit = false;
            var result = Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o => {
                libraryName = o.LibraryName;
                oldLibVersion = o.OldLib;
                newLibVersion = o.NewLib;

                clientName = o.ClientName;
                oldClientVersion = o.OldClient;
                newClientVersion = o.NewClient;
                configurationFile = o.configurationFile;

                if (o.SlnPath != null)
                    slnPath = o.SlnPath;

                verbose = o.Verbose;
                extractlibraryEdit = o.Itself;
                targetUsagesVersion = o.targetUsagesVersion;
                if (!extractlibraryEdit && (oldClientVersion == null || newClientVersion == null)) {
                    Utils.LogTest("Please specify the old and new version of the client code.");
                    System.Environment.Exit(1);
                }
                Config.CompilationMode = o.CompilatioMode;
            });

            if (result.Tag == ParserResultType.NotParsed) {
                System.Environment.Exit(0);
            }

            if (libraryName == null || oldLibVersion == null || newLibVersion == null)
                Debug.Fail("Fail: please provide library name, old/new version id!");

            var libraryPath = Path.Combine(benchmarkPath, libraryName);
            outputPath = Path.Combine(libraryPath, libraryName + "_" + oldLibVersion + "_" + newLibVersion);
            if (!File.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            oldLibPath = Path.Combine(libraryPath, "library", libraryName + "-" + oldLibVersion);
            newLibPath = Path.Combine(libraryPath, "library", libraryName + "-" + newLibVersion);

            // take library itself as it own client
            if(extractlibraryEdit){
                oldClientPath = oldLibPath;
                newClientPath = newLibPath;

                if (Config.CompilationMode) {
                    oldSlnPath = Path.Combine(oldLibPath, slnPath);
                    newSlnPath = Path.Combine(newLibPath, slnPath);
                }

                if (targetUsagesVersion == null && !Directory.Exists(oldLibPath))
                    Debug.Fail(oldLibPath + " does not exist!");
                if (targetUsagesVersion == null && !Directory.Exists(newLibPath))
                    Debug.Fail(newLibPath + " does not exist!");
            } 
            else{
                oldClientPath = Path.Combine(benchmarkPath, libraryName, "client", clientName + "-" + oldClientVersion);
                newClientPath = Path.Combine(benchmarkPath, libraryName, "client", clientName + "-" + newClientVersion);
                if (Config.CompilationMode) {
                    oldSlnPath = Path.Combine(oldClientPath, slnPath);
                    newSlnPath = Path.Combine(newClientPath, slnPath);
                }
            }
            Config.oldPath = oldClientPath;
            Config.newPath = newClientPath;
            
            if (!Directory.Exists(oldClientPath))
                Debug.Fail(oldClientPath + " does not exist!");
            if (!Directory.Exists(newClientPath))
                Debug.Fail(newClientPath + " does not exist!");
        }

        public static List<Class> ExtractClasses(string version){
            List<Class> classes = null;
            if (Config.CompilationMode) {
                RTCompilation rtc = RTCompilation.Init();
                if (rtc == null){
                    Debug.Fail("Failed to compile the project!");
                }
                if (version.Equals("old")){
                    rtc.CompileSolution(oldSlnPath, "old");
                    classes = ClassExtractor.ExtractClasses(rtc.oldSyntexNodes.Select(e => new Record<SyntaxNode, string>(e.GetCompilationUnitRoot(), e.FilePath)).ToList());
                }
                else{
                    rtc.CompileSolution(newSlnPath, "new");
                    classes = ClassExtractor.ExtractClasses(rtc.newSyntexNodes.Select(e => new Record<SyntaxNode, string>(e.GetCompilationUnitRoot(), e.FilePath)).ToList());
                }
            }
            else {
                if (version.Equals("old"))
                    classes = ClassExtractor.ExtractClasses(oldClientPath);
                else
                    classes = ClassExtractor.ExtractClasses(newClientPath);
            }

            if (classes == null){
                Debug.Fail("failed to extract classes from project");
            }
            return classes;
        }

    }
}

