using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CSharpEngine
{
    public class ClassExtractor
    {
        public ClassExtractor(){ }

        public static List<MatchedClass> CalculateMatchedClass(List<Class> cls1, List<Class> cls2){
            var matchedPairs = new List<MatchedClass>();
            
            var filtered_classes1 = new List<Class>();
            var filtered_classes2 = new List<Class>(cls2);

            foreach (var cl in cls1){                
                if (filtered_classes2.Contains(cl)){
                    matchedPairs.Add(new MatchedClass(cl, filtered_classes2.FirstOrDefault(e => e.Equals(cl))));
                    filtered_classes2.Remove(cl);
                }
                else
                    filtered_classes1.Add(cl);
            }

            foreach (var cl2 in filtered_classes2){
                bool findMatch = false;
                var matchedFiles = Utils.GetMatchedFileFromGit(cl2.GetFilePath());
                foreach (var cl1 in filtered_classes1) {
                    if(cl2.Distance(cl1, matchedFiles) < 0.5){
                        // regard they are same file if the relative content distance is less threshold 0.3
                        matchedPairs.Add(new MatchedClass(cl1, cl2));
                        filtered_classes1.Remove(cl1);
                        findMatch = true;
                        break;
                    }
                }
                if (!findMatch)
                    matchedPairs.Add(new MatchedClass(null, cl2));
            }
            foreach (var cl1 in filtered_classes1)
                matchedPairs.Add(new MatchedClass(cl1, null));
            
            return matchedPairs;
        }

        public static List<Class> ExtractClasses(List<Record<SyntaxNode, string>> rootNodes)
        {
            var classes = new List<Class>();
            var passeredFile = new List<string>();
            foreach (var node in rootNodes) {
                if (passeredFile.Contains(node.Item2))
                    continue;
                else
                    passeredFile.Add(node.Item2);
                classes.AddRange(ParseClass(node.Item1, node.Item2));
            }
            return classes;
        }

        public static List<Class> ExtractClasses(string path){
            var classes = new List<Class>();

            if (!Directory.Exists(path) && !File.Exists(path))
                return classes;
            var subDirs = Directory.GetDirectories(path);
            foreach (var subDir in subDirs){
                var subclasses = ExtractClasses(subDir);
                subclasses.ToList().ForEach(x => classes.Add(x));
            }
            var files = Directory.GetFiles(path);
            foreach (var file in files){
                if (file.Contains(".cs") && !file.Contains("csproj")){
                    var cls = ExtractClassesFromFile(file);
                    cls.ToList().ForEach(x => classes.Add(x));
                }
            }
            return classes;
        }

        public static List<Class> ExtractClassesFromFile(string filePath){
            string content = File.ReadAllText(filePath);
            content = content.Replace("default)", "default(CancellationToken))"); // FIXME: Hard code b/o unknown error

            var node = CSharpSyntaxTree.ParseText(content).GetRoot();
            return ParseClass(node, filePath);
        }

        public static List<Class> ParseClass(SyntaxNode node, string filePath) {
            var classes = new List<Class>();
            
            var namespaces = node.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
            foreach (var ns in namespaces)
            {
                var classNodes = ns.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var classNode in classNodes)
                {
                    if (classNode != null)
                    {
                        string modifiers = "";
                        foreach (var modifier in classNode.Modifiers)
                            modifiers += modifier.ToString() + " ";
                        // if (modifiers.Contains("private") || modifiers.Contains("internal"))
                        //    continue;
                        // Get the name of the model class
                        string className = classNode.Identifier.Text;

                        string typeParameterList = "";
                        if (classNode.TypeParameterList != null)
                            typeParameterList = classNode.TypeParameterList.ToString();

                        var nameSpace = ns.Name.ToString();
                        var classParent = classNode.Parent;
                        while (classParent as TypeDeclarationSyntax != null && classParent != ns)
                        {
                            nameSpace += "." + (classParent as TypeDeclarationSyntax).Identifier.Text;
                            classParent = classParent.Parent;
                        }

                        classes.Add(new Class(filePath, nameSpace,
                                    modifiers, className, typeParameterList, classNode));
                    }
                }
            }
            return classes;
        }
    }
}
