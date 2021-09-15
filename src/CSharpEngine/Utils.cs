using DiffMatchPatch;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CSharpEngine {

    public enum ChangeType {
        None, //0
        Insert, //1
        Delete, //2
        Rename, //3
        ChangeVisibility, //4
        ChangeType, //5
        ChangeArg, //6
    }


    public class Record<T1, T2>{

        public T1 Item1;
        public T2 Item2;
        public Record(T1 item1, T2 item2){
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }

    public class Record<T1, T2, T3>{

        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public Record(T1 item1, T2 item2, T3 item3){
            this.Item1 = item1;
            this.Item2 = item2;
            this.Item3 = item3;
        }
    }

    public class BreakingChange
    {
        public string library;
        public string source;
        public string target;
        public string old_class;
        public string new_class;
        public List<string> old_apis;
        public List<string> new_apis;

        public BreakingChange(string library, string source, string target, string old_class, string new_class, List<string> old_apis, List<string> new_apis)
        {
            this.library = library;
            this.source = source;
            this.target = target;
            this.old_class = old_class;
            this.new_class = new_class;
            this.old_apis = old_apis;
            this.new_apis = new_apis;
        }

        public static BreakingChange loadInterestingAPI(string metadataFile)
        {
            return JsonConvert.DeserializeObject<BreakingChange>(File.ReadAllText(metadataFile));
        }

        public bool Matches(Record<MatchedClass, MatchedMethod> modifiedInter)
        {
            return MatchesWithNewAPI(modifiedInter) && MatchesWithOldAPI(modifiedInter);
        }

        public bool MatchesWithNewAPI(Record<MatchedClass, MatchedMethod> modifiedInter)
        {
            if (modifiedInter.Item1 == null || modifiedInter.Item2 == null)
                return false;
            Class refClass;
            Method refMethod;
            refClass = modifiedInter.Item1.class2;
            refMethod = modifiedInter.Item2.method2;
            if (refClass == null || refMethod == null)
                return false; ;
            if (!new_class.Equals(refClass.nameSpace + "." + refClass.className) ||
                !new_apis.Contains(refMethod.methodName))
                return false;
            return true;
        }

        public bool MatchesWithOldAPI(Record<MatchedClass, MatchedMethod> modifiedInter)
        {
            if (modifiedInter.Item1 == null || modifiedInter.Item2 == null)
                return false;
            Class refClass;
            Method refMethod;
            refClass = modifiedInter.Item1.class1;
            refMethod = modifiedInter.Item2.method1;
            if (refClass == null || refMethod == null)
                return false;
            if (!old_class.Equals(refClass.nameSpace + "." + refClass.className) ||
                !old_apis.Contains(refMethod.methodName))
                return false;
            return true;
        }

        override
        public string ToString() {
            string str = "library: " + library + "\n" +
                   "source: " + source + "\n" +
                   "target: " + target + "\n" +
                   "old_class: " + old_class + "\n" +
                   "new_class: " + new_class + "\n" +
                   "old_apis: [";
            foreach (var item in old_apis)
                str += item;
            str += "]\nnew_apis: [";
            foreach (var item in new_apis)
                str += item;
            str += "]";
            return str;
        }
    }

    public class Config{
        public static double DisThreshold = 0.4;
        public static bool CompilationMode = false;

        public static string oldPath = null;
        public static string newPath = null;
    }

    public class Utils{
        public static void LogTest(string message){
            using (var writer = new System.IO.StreamWriter(System.Console.OpenStandardOutput()))
            {
                DateTime localDate = DateTime.Now;
                writer.WriteLine("[" + localDate.ToString() + "] " + message);
            }
        }

        public static string GetMatchedFileFromGit(string newFp) {
            var f1 = Path.GetFullPath(newFp).Replace(Path.GetFullPath(Config.newPath) + "\\", "");

            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
            pProcess.StartInfo.FileName = "git";

            pProcess.StartInfo.Arguments = "log --follow --pretty=\"\" --name-only " + f1;
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.RedirectStandardOutput = true;
            pProcess.StartInfo.WorkingDirectory = Config.newPath;

            pProcess.Start();
            string strOutput = pProcess.StandardOutput.ReadToEnd();
            pProcess.WaitForExit();

            return strOutput;
        }

        public static bool IsMatchedFile(string newFp, string oldFp, string matchedFiles) {
            var f1 = Path.GetFullPath(newFp).Replace(Path.GetFullPath(Config.newPath) + "\\", "");
            var f2 = Path.GetFullPath(oldFp).Replace(Path.GetFullPath(Config.oldPath) + "\\", "");
            if (f1 == f2)
                return true;

            /*Console.WriteLine("============================================");
            Console.WriteLine(Config.newPath);
            Console.WriteLine(f1 + " => " + f2);
            Console.WriteLine(strOutput);
            Console.WriteLine(strOutput.Contains(f2));*/

            var transferredF2 = f2.Replace("\\", "/");
            return matchedFiles.Contains(f2) || matchedFiles.Contains(transferredF2);
        }

        public static double CloneDetectionDis(SyntaxNode n1, SyntaxNode n2) {
            var vec1 = GenerateTypeCountVec(n1);
            var size1 = vec1.Select(e => e.Value).Sum();
            var vec2 = GenerateTypeCountVec(n2);
            var size2 = vec1.Select(e => e.Value).Sum();

            double dis = 0;
            foreach (var ele in vec1)
            {
                if (vec2.ContainsKey(ele.Key))
                {
                    dis += Math.Abs(ele.Value - vec2[ele.Key]);
                    vec2.Remove(ele.Key);
                }
                else
                {
                    dis += Math.Abs(ele.Value);
                }
            }

            foreach (var ele in vec2)
                dis += Math.Abs(ele.Value);

             return (dis) / (size1 + size2);
        }

        private static Dictionary<string, int> GenerateTypeCountVec(SyntaxNode node, Dictionary<string, int> vec = null)
        {
            if (vec == null)
                vec = new Dictionary<string, int>();
            var lable = node.GetType().ToString();
            if (vec.ContainsKey(lable))
                vec[lable] += 1;
            else
                vec.Add(lable, 1);

            foreach (var child in node.ChildNodes())
            {
                GenerateTypeCountVec(child, vec);
            }
            return vec;
        }

        public static double ContentDistance(string content1, string content2) {
            diff_match_patch dmp = new diff_match_patch();
            List<Diff> diff = dmp.diff_main(content1, content2);
            
            return (double)dmp.diff_levenshtein(diff) / System.Math.Max(content1.Count(), content2.Count());
        }
    }
}
