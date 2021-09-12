using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace CSharpEngine{

    public class ChangeSummary{
        private List<MatchedClass> changes;

        public static ChangeSummary CreateChangeSummary(string oldVersion, string newVersion, string changeSummaryPath){
            var changeSummary = new ChangeSummary();
            if(File.Exists(changeSummaryPath)){
                changeSummary.changes = JsonConvert.DeserializeObject<List<MatchedClass>>(File.ReadAllText(changeSummaryPath));
            } else {
                if (!Directory.Exists(oldVersion))
                    Debug.Fail(oldVersion + " does not exist!");
                if (!Directory.Exists(newVersion))
                    Debug.Fail(newVersion + " does not exist!");

                var classes1 = ClassExtractor.ExtractClasses(oldVersion);
                var classes2 = ClassExtractor.ExtractClasses(newVersion);
                var matchedClasses = ClassExtractor.CalculateMatchedClass(classes1, classes2);
                changeSummary.changes = matchedClasses.Where(e => e.ModifiedClassSignature()).ToList();
                changeSummary.LogChangeSummary(changeSummaryPath);
            }
            return changeSummary;
        }

        public void LogChangeSummary(string changeSummaryPath)
        {            
            string json_content = JsonConvert.SerializeObject(changes.ToList(), Formatting.Indented);   
            using (StreamWriter outputFile = new StreamWriter(changeSummaryPath))
                outputFile.Write(json_content);
        }

        public List<Record<MatchedClass, MatchedMethod>> extractModifiedInterfaceName(){
            var ret = new List<Record<MatchedClass, MatchedMethod>>();
            // ret.AddRange(extractModifiedClassName());
            ret.AddRange(extractModifiedMethodName());
            // ret.AddRange(extractModifiedFieldName());
            return ret;
        }

        public List<Record<Class, Method>> extractModifiedClassName(){
            var modifiedClassName = new List<Record<Class, Method>>();
            foreach(var mc in changes)
            {
                if(mc.class1 == null)
                    continue;
                if(mc.class2 == null || !mc.class1.EqualSignature(mc.class2))
                    // modifiedClassName.Add(new Record<string, string>(null, mc.class1.className));
                    modifiedClassName.Add(new Record<Class, Method>(mc.class1, null));
            }
            return modifiedClassName.Distinct().ToList();
        }
        public List<Record<MatchedClass, MatchedMethod>> extractModifiedMethodName(){
            var modifiedMethodName = new List<Record<MatchedClass, MatchedMethod>>();
            foreach (var mc in changes)
            {
                // if(mc.class1 == null || mc.class2 == null || mc.modifiedMethods == null)
                if (mc.modifiedMethods == null)
                    continue;
                foreach (var mm in mc.modifiedMethods){
                    //if(mm.method1 == null)
                    //    continue;
                    // modifiedMethodName.Add(new Record<string, string, int>(mc.class1.GetSignature(), mm.method1.methodName, mm.method1.argList.Count));
                    modifiedMethodName.Add(new Record<MatchedClass, MatchedMethod>(mc, mm));
                }
            }
            return modifiedMethodName.Distinct().ToList();
        }

        public List<string> extractModifiedFieldName(){
            var modifiedFieldName = new List<string>();
            foreach(var mc in changes)
            {
                if(mc.class1 == null || mc.class2 == null || mc.modifiedFields == null)
                    continue;
                foreach(var mm in mc.modifiedFields){
                    if(mm.field1 == null)
                        continue;                    
                    modifiedFieldName.Add(mm.field1.identifier);
                }
            }
            return modifiedFieldName.Distinct().ToList();
        }
    }
}

