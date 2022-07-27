using CSharpEngine;
using System.Linq;
using System.IO;
using CommandLine;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using Microsoft.ProgramSynthesis.Wrangling.Tree;

namespace Synthesizer
{

    class MainEntry
    {
        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to Global.verbose messages.")]
            public bool Verbose { get; set; }

            [Option('m', "oldLib", Required = true, HelpText = "The old version of library.")]
            public string OldLib { get; set; }

            [Option('n', "newLib", Required = true, HelpText = "The new version of library.")]
            public string NewLib { get; set; }

            [Option('l', "libraryName", Required = true, HelpText = "The name of library.")]
            public string LibraryName { get; set; }

            [Option('s', "TargetAPI", Required = true, HelpText = "The name of old target API for fixing.")]
            public string oTarget { get; set; }

            [Option('t', "TargetAPI", Required = false, HelpText = "The name of new target API for fixing.")]
            public string nTarget { get; set; }
            [Option("t1", Required = false, Default = 0.15, HelpText = "The threshold for old usages [0, 1].")]
            public double t1 { get; set; }
            [Option("t2", Required = false, Default=0.25, HelpText = "The threshold for new usages [0, 1].")]
            public double t2 { get; set; }
            [Option('o', "additionalOutput", Default = false, Required = false, HelpText = "Use additionalOutput when synthesizing adaptation rule.")]
            public bool AdditionalOutput { get; set; }
            [Option('i', "additionalInput", Default = false, Required = false, HelpText = "Use additionalInput when synthesizing adaptation rule.")]
            public bool additionalInput { get; set; }
        }

        public static void Main(string[] args)
        {
            string oldLibVersion = null, newLibVersion = null;
            string libraryName = null;
            string otargetAPI = null, ntargetAPI = null;
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o => {
                libraryName = o.LibraryName;
                oldLibVersion = o.OldLib;
                newLibVersion = o.NewLib;

                otargetAPI = o.oTarget;
                ntargetAPI = o.nTarget;
                // Global.verbose = o.Verbose;
                Global.verbose = true;
                Config.OldUsageThreashold = o.t1;
                Config.NewUsageThreashold = o.t2;
                Config.UseAdditionalInput = o.additionalInput;
                Config.UseAdditionalOutput = o.AdditionalOutput;

                if (libraryName == "FluentValidation")
                    Config.NewKeyWords = "ValidationContext";
            });

            if (ntargetAPI == null)
                ntargetAPI = otargetAPI;

            if (libraryName == null || oldLibVersion == null || newLibVersion == null)
                return;

            var outputPath = Path.Combine(Global.benchmarkPath, libraryName, libraryName + "_" + oldLibVersion + "_" + newLibVersion);

            Config.PrintConfig();
            // load existing edits
            var editPath = Path.Combine(outputPath, "library");
            if (!Directory.Exists(editPath))
                Debug.Fail("The edit metadata file does not exist!");
            var edits = SynthesizerUtils.LoadEdit(editPath);
            List<Edit> relevantEdits = edits.Where(e => e.id.Equals(otargetAPI)).ToList();
            Console.WriteLine("load " + relevantEdits.Count + " relevant edits!");

            // load new usages
            List<RelevantNodes> newUsages = null;
            if (Config.UseAdditionalOutput) {
                var newUsagePath = Path.Combine(outputPath, "new_relevant_client");
                if (Config.UseTypedUsage)
                    newUsagePath = Path.Combine(outputPath, "new_typed_relevant_client");
                newUsages = SynthesizerUtils.LoadClientUsage(newUsagePath, ntargetAPI, 1000);
                Console.WriteLine("load " + newUsages.Count + " new relevant usages");
                Global.NumNewUsage = newUsages.Count;
            }

            // load old usages
            List<Record<Node, InvokeType>> oldUsages = null;
            if (!Config.Validate) {
                var oldUsagePath = Path.Combine(outputPath, "old_relevant_client");
                if (Config.UseTypedUsage)
                    oldUsagePath = Path.Combine(outputPath, "old_typed_relevant_client");
                var relevantOldUsages = SynthesizerUtils.LoadClientUsage(oldUsagePath, otargetAPI, 1000);  //new List<Node>();
                Console.WriteLine("load " + relevantOldUsages.Count + " old relevant usages");
                Global.NumOldUsage = relevantOldUsages.Count;
                oldUsages = relevantOldUsages.Select(e1 => new Record<Node, InvokeType>(e1.GetStructNode(), e1.invocationType)).ToList();
            }
            else {
                var clientPath = Path.Combine(outputPath, "clients");
                List<Edit> relevantClientEdits = new List<Edit>();
                var directoryInfo = new DirectoryInfo(clientPath);
                DirectoryInfo[] clientEditDirs = directoryInfo.GetDirectories();
                foreach(var clientEditDir in clientEditDirs) {
                    Console.WriteLine("loading " + clientEditDir.FullName);
                    var clientEdits = SynthesizerUtils.LoadEdit(clientEditDir.FullName);
                    List<Edit> relevantClientEdit = clientEdits.Where(e => e.id.Equals(otargetAPI)).ToList();
                    relevantClientEdits.AddRange(relevantClientEdit);
                }
                Console.WriteLine("load " + relevantClientEdits.Count + " relevant client edits!");
                Global.NumOldUsage = relevantClientEdits.Count;
                oldUsages = relevantClientEdits.Select(e1 => new Record<Node, InvokeType>(e1.GetOldStructNode(), e1.oldTypeInfo)).ToList();
            }


            Global.Log("invoke synthesis engine...");
            var synthesisEngine = new Synthesizer();

            var clusters = ClusterAlgo.ClusterBasedOnAntiUnification(relevantEdits, otargetAPI, ntargetAPI, newUsages, oldUsages);
            synthesisEngine.SynthesisProgram(synthesisEngine, clusters);

        }
    }
}
