using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.IO;
using Newtonsoft.Json;
using System;

namespace CSharpEngine
{
    class EditMiner
    {
        public static List<string> blockStatement = new List<string>{"DoStatement", "ForEachStatement", "ForStatement", "IfStatement", "LockStatement",
            "SwitchStatement", "UsingStatement", "TryStatement", "WhileStatement", "LocalFunctionStatement", "UsingStatement"};

        private string outputPath;
        private List<MatchedClass> matchedClasses;
        private ChangeSummary cs;
        private BreakingChange breakingChanges;
        public EditMiner(List<MatchedClass> matchedClasses, ChangeSummary cs, BreakingChange breakingChanges, string outputPath) {
            this.cs = cs;
            this.matchedClasses = matchedClasses;
            this.outputPath = outputPath;
            this.breakingChanges = breakingChanges;
        }

        public bool SearchEdits()
        {
            var edits = new List<Edit>();
            var modifiedInterfaces = cs.extractModifiedInterfaceName();

            var modifiedMethodPairs = new List<MatchedMethod>();
            foreach (var matchedClass in matchedClasses)
                // travel all the modified method and search for the edit related to the library update
                modifiedMethodPairs.AddRange(matchedClass.GetMatchedMethods().Where(e => !e.IsUnmodifiedMethod()));
            
            foreach (var modifiedInter in modifiedInterfaces)
            {
                if (!breakingChanges.Matches(modifiedInter))
                    continue;
                foreach(var modifiedMethodPair in modifiedMethodPairs) { 
                    if (IsEmptyMethod(modifiedMethodPair.method1) || IsEmptyMethod(modifiedMethodPair.method2))
                        continue;

                    var refClass = modifiedInter.Item1.class1;

                    //if (inter.Item2 == null && !MatchingPolice.Contains(matchedMethod.method1.GetSyntax().Body, refClass.className))
                    //    continue;

                    // coarsely filter out
                    if (!MatchingPolice.Contains(modifiedMethodPair.method1.GetSyntax().Body, modifiedInter.Item2.method1.methodName))
                        continue;

                    // fine-grained filter out & generate edits
                    var subEdits = TrimEdit(modifiedMethodPair.method1, modifiedMethodPair.method2, modifiedInter);
                    edits.AddRange(subEdits.Where(e => !edits.Contains(e)));
                }
            }
            edits = edits.OrderBy(o => o.id).ToList();
            if (edits.Count() != 0)
                SaveRelevantEdit(edits);
            return edits.Count() > 0;
        }

        public List<Edit> TrimEdit(Method method1, Method method2, Record<MatchedClass, MatchedMethod> reference)
        {
            var children1 = UnRollNode(method1.GetSyntax().Body);
            var children2 = UnRollNode(method2.GetSyntax().Body);
            var matchedChildren = GetMatchedChild(children1, children2);

            var edits = new List<Edit>();
            for (int i = 0; i < matchedChildren.Count - 1; i++)
            {
                var oldList = new List<SyntaxNodeOrToken>();
                var newList = new List<SyntaxNodeOrToken>();
                for (var j = matchedChildren[i].Item1 + 1; j < matchedChildren[i + 1].Item1; j++)
                    oldList.Add(children1[j]);

                for (var j = matchedChildren[i].Item2 + 1; j < matchedChildren[i + 1].Item2; j++)
                    newList.Add(children2[j]);

                if ((oldList.Count == 0 && newList.Count == 0) || MatchingPolice.TokenSame(oldList, newList))
                    continue;

                var invocation1 = MatchingPolice.SearchNode(oldList, reference.Item1.class1, reference.Item2.method1, "old");
                if (invocation1 != null)
                {
                    if (reference.Item2.method2 != null)
                    { // change method signature
                        SyntaxNodeOrToken invocation2 = MatchingPolice.SearchNode(newList, reference.Item1.class2, reference.Item2.method2, "new");
                        if (invocation2 != null && (reference.Item2.changeType == ChangeType.ChangeType || !MatchingPolice.TokenSame(invocation1, invocation2)))
                        {
                            // save the invocation and its surrounding context
                            edits.Add(new Edit(oldList, newList, reference.Item2.method1.methodName));

                            var onlyInvocationEditInput = new List<SyntaxNodeOrToken>();
                            onlyInvocationEditInput.Add(invocation1);
                            var onlyInvocationEditOutput = new List<SyntaxNodeOrToken>();
                            onlyInvocationEditOutput.Add(invocation2);
                            // save the invocation only
                            edits.Add(new Edit(onlyInvocationEditInput, onlyInvocationEditOutput, reference.Item2.method1.methodName));
                        }
                    }
                    else // deleted method
                        edits.Add(new Edit(oldList, newList, reference.Item2.method1.methodName));
                }
            }
            return edits;
        }


        private List<Record<int, int>> GetMatchedChild(List<SyntaxNodeOrToken> nodes1, List<SyntaxNodeOrToken> nodes2)
        {
            var n = nodes1.Count;
            var m = nodes2.Count;

            var matchedChild = new List<Record<int, int>>[m + 1, n + 1];
            for (int ii = 0; ii < m + 1; ii++)
                for (int jj = 0; jj < n + 1; jj++)
                    matchedChild[ii, jj] = new List<Record<int, int>>();

            int[,] d = new int[m + 1, n + 1];

            for (var i = 0; i < m + 1; i++)
                d[i, 0] = i;

            for (var j = 1; j < n + 1; j++)
                d[0, j] = j;

            for (var j = 1; j < n + 1; j++)
            {
                for (var i = 1; i < m + 1; i++)
                {
                    var substitutionCost = 1;
                    if (nodes1[j - 1].ToString().Equals(nodes2[i - 1].ToString()))
                        substitutionCost = 0;

                    d[i, j] = Math.Min(d[i - 1, j] + 1,                             // deletion
                                       Math.Min(d[i, j - 1] + 1,                    // insertion
                                                d[i - 1, j - 1] + substitutionCost)); // substitution
                    if (d[i, j] == d[i - 1, j - 1] + substitutionCost && substitutionCost == 0)
                    {
                        matchedChild[i, j].AddRange(matchedChild[i - 1, j - 1]);
                        matchedChild[i, j].Add(new Record<int, int>(j - 1, i - 1));
                    }
                    else if (d[i, j] == d[i, j - 1] + 1)
                        matchedChild[i, j].AddRange(matchedChild[i, j - 1]);
                    else
                        matchedChild[i, j].AddRange(matchedChild[i - 1, j]);
                }
            }
            var ret = matchedChild[m, n];
            ret.Add(new Record<int, int>(n, m));
            return ret;
        }

        private List<SyntaxNodeOrToken> UnRollNode(SyntaxNodeOrToken node)
        {
            var nodes = new List<SyntaxNodeOrToken>();
            foreach (var child in node.ChildNodesAndTokens())
            {
                string kind = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(child).ToString();
                if (child.IsToken
                   || (kind.Contains("Statement") && !blockStatement.Contains(kind))
                   || kind.Contains("Expression"))
                    nodes.Add(child);
                else
                {
                    nodes.AddRange(UnRollNode(child.AsNode()));
                }
            }
            return nodes;
        }

        public bool IsEmptyMethod(Method method) {
            return method == null || method.GetSyntax() == null || method.GetSyntax().Body == null;
        }

        public void SaveRelevantEdit(List<Edit> relevantEdits)
        {
            string _outputPath = Path.Combine(outputPath, "edits");
            if (Config.CompilationMode)
                _outputPath = Path.Combine(outputPath, "typed_edits");
            if (!File.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
            int index = 0;
            foreach (var edit in relevantEdits)
            {
                var inputNode = edit.GetOldStructNode();
                var outputNode = edit.GetNewStructNode();

                // save each edit as xml format
                if (inputNode != null)
                {
                    edit.inputPath = "inputNode" + index + ".xml";
                    var editFullPath = Path.Combine(_outputPath, edit.inputPath);
                    Translator.storeNode(inputNode, editFullPath);
                }
                if (outputNode != null)
                {
                    edit.outputPath = "outputNode" + index + ".xml";
                    var editFullPath = Path.Combine(_outputPath, edit.outputPath);
                    Translator.storeNode(outputNode, editFullPath);
                }
                index++;
            }

            var metadataFile = Path.Combine(_outputPath, "edit_metadata.json");
            string json_content = JsonConvert.SerializeObject(relevantEdits, Formatting.Indented);
            using (StreamWriter outputFile = new StreamWriter(metadataFile))
                outputFile.Write(json_content);

            var editfile = Path.Combine(_outputPath, "edit.txt");
            Utils.LogTest("Number of relevant human adapations: " + relevantEdits.Count());
            Utils.LogTest("The mining results are save at " + metadataFile);
            foreach (var edit in relevantEdits)
            {
                using (StreamWriter outputFile = File.AppendText(editfile))
                {
                    outputFile.WriteLine("========================================================== " + edit.id);
                    outputFile.WriteLine("---- inputNode: " + edit.inputPath);
                    outputFile.WriteLine("---- outputNode: " + edit.outputPath);
                    outputFile.WriteLine(edit.ToString());
                }
            }
        }
    }
}
