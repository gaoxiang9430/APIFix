using Microsoft.ProgramSynthesis.Wrangling.Tree;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using Microsoft.ProgramSynthesis.Transformation.Tree;
using Microsoft.ProgramSynthesis.Wrangling.Constraints;
using System.Collections.Generic;
using CSharpEngine;
using System;
using System.Linq;
using Microsoft.ProgramSynthesis.Utils.Interactive;

namespace Synthesizer{
    public class Synthesizer {

        public Example<Node, Node> CreateExample(Node inputNode, Node outputNode){
            return new Example<Node, Node>(inputNode, outputNode, true);
        }

        public Program Learn(Example<Node, Node>[] examples){
            return Learner.Instance.Learn(examples);
        }

        public void SynthesisProgram(Synthesizer synthesisEngine, List<Cluster> clusters)
        {
            Attributes.SetKnownSoftAttributes(new string[] { });
            int givenExamples = Config.GivenExample;
            int totalTestCases = 0;
            int totalCorrect = 0;

            var oldUsageResult = new Dictionary<int, int>();

            for (int i = 0; i < Global.NumOldUsage + 1; i++)
                oldUsageResult[i] = 0;

            foreach (var cluster in clusters)
            {
                Global.Log(cluster.ToString());

                var oldUsages = cluster.oldUsages;
                if (oldUsages.Count() == 0)
                    continue;
                if (Config.UseTypedUsage && Config.UseAdditionalOutput){
                    oldUsages = FilterViaObjectType(cluster.edits, cluster.newUsages, oldUsages);
                    // oldUsages = FilterViaArgType(cluster.edits, cluster.newUsages, oldUsages);
                }

                var clusterEdits = cluster.edits;
                var trainingSet = clusterEdits.GetRange(0, Math.Min(givenExamples, clusterEdits.Count()));
                var editExamples = trainingSet;

                if (Config.UseAdditionalOutput && Config.UseAdditionalInput && cluster.newUsages.Count() > 0) {
                    var _newUsages = cluster.newUsages.Select(e => e.Item1).ToList();
                    var _oldUsages = cluster.oldUsages.Select(e => e.Item1).ToList();
                    editExamples = GenerateNewExampleBasedOnAdditionalOutputAndInput(trainingSet, _newUsages, _oldUsages);
                }
                else if (Config.UseAdditionalOutput && cluster.newUsages.Count() > 0) { 
                    editExamples = GenerateNewExampleBasedOnAdditionalOutput(trainingSet, cluster.newUsages.Select(e => e.Item1).ToList());
                }             
                else if (Config.UseAdditionalInput && cluster.oldUsages.Count() > 0) { 
                    editExamples = GenerateNewExampleBasedOnAdditionalInput(trainingSet, cluster.oldUsages.Select(e => e.Item1).ToList());
                }

                if (editExamples.Count < givenExamples)
                    continue;

                var trainingSize = Math.Min(10, editExamples.Count());
                Example<Node, Node>[] examples = new Example<Node, Node>[trainingSize];
                Global.Log("Input to synthesize the program:");
                for (int i = 0; i < trainingSize; i++)
                {
                    Global.Log(editExamples[i].GetOldStructNode().GenerateCode());
                    Global.Log("---------------------");
                    Global.Log(editExamples[i].GetNewStructNode().GenerateCode());
                    examples[i] = synthesisEngine.CreateExample(editExamples[i].GetOldStructNode(), editExamples[i].GetNewStructNode());
                }

                Program program = null;
                try {
                    program = synthesisEngine.Learn(examples);
                    if (program == null) {
                        Global.Log("program is null");
                        continue;
                    }
                }
                catch (ArgumentNullException e)
                {
                    Global.Log("System.ArgumentNullException: " + e.Message);
                }

                if(clusterEdits.Count() > givenExamples) { 
                    var testingSet = clusterEdits.GetRange(givenExamples, clusterEdits.Count() - givenExamples);
                    totalTestCases += testingSet.Count();
                    for (int i = 0; i < testingSet.Count; i++)
                    {
                        var predict = program.Run(testingSet[i].GetOldStructNode());
                        if (predict != null) { 
                            Global.Log("predict is " + predict.GenerateCode());
                            if (predict.GenerateCode().Equals(testingSet[i].GetNewStructNode().GenerateCode()))
                                totalCorrect += 1;
                        }
                        else
                            Global.Log("predict is null");

                        Global.Log("output  is " + testingSet[i].GetNewStructNode().GenerateCode());
                        Global.Log("----------------------------------------------------");
                    }
                }

                TransformOldUsage(program, oldUsages, oldUsageResult);
            }

            float acc = 0;
            if (totalTestCases != 0)
                acc = (float)totalCorrect / totalTestCases;
            Console.WriteLine("total correct cases : " + totalCorrect + " totalTestCases "+
                                totalTestCases + " Successful rate is " + acc);
            Console.WriteLine("The successful rate for transforming old usages: " + 
                oldUsageResult.Where(e => e.Value==1).Count() + " / " +  oldUsageResult.Where(e => e.Value!=0).Count());
        }

        private List<Record<Node, int, InvokeType>> FilterViaObjectType(
            List<Edit> edits, 
            List<Record<Node, InvokeType>> newUsages, 
            List<Record<Node, int, InvokeType>> oldUsages) 
        {
            var newObjectTypes = new List<string>();
            var oldObjectTypes = new List<string>();
            bool keepTypeInTrans = true;
            foreach (var edit in edits) {
                if(edit.newTypeInfo != null)
                    newObjectTypes.Add(edit.newTypeInfo.className);
                if(edit.oldTypeInfo != null)
                    oldObjectTypes.Add(edit.oldTypeInfo.className);
                if (edit.newTypeInfo != null && edit.oldTypeInfo != null &&
                    !edit.newTypeInfo.className.Equals(edit.oldTypeInfo.className))
                    keepTypeInTrans = false;
            }
            foreach (var usage in newUsages)
                newObjectTypes.Add(usage.Item2.className);

            /*var newObjectTypeGroups = newObjectTypes.GroupBy( i => i );
            // only consider the types that occur multiple times
            newObjectTypes = newObjectTypeGroups.Where(e=>e.Count()>1).Select(e=>e.Key).ToList();
            if (newObjectTypes.Count() == 0)
                return oldUsages;*/
            newObjectTypes = newObjectTypes.Distinct().ToList();

            var refinedOldUsages = new List<Record<Node, int, InvokeType>>();
            foreach (var oldUsage in oldUsages) {
                if (!(keepTypeInTrans^ObjTypeContains(newObjectTypes, oldUsage.Item3.className)))
                    refinedOldUsages.Add(oldUsage);
            }
            return refinedOldUsages;
        }

        private bool ObjTypeContains (List<string> newObjectTypes, string targetObjType){
            targetObjType = RemoveGeneticPara(targetObjType);
            foreach(var newObjType in newObjectTypes){
                var baseTypes = RemoveGeneticPara(newObjType).Split(":");
                var targetBaseTypes = targetObjType.Split(":");
                foreach(var ti in baseTypes){
                    foreach(var tj in targetBaseTypes){
                        if(ti.Equals(tj))
                            return true;
                    }
                }
            }
            return false;
        }

        private List<Record<Node, int, InvokeType>> FilterViaArgType(
            List<Edit> edits, 
            List<Record<Node, InvokeType>> newUsages, 
            List<Record<Node, int, InvokeType>> oldUsages) 
        {
            var newArgTypes = new List<List<string>>();
            var oldArgTypes = new List<string>();
            bool keepTypeInTrans = false;
            foreach (var edit in edits) {
                if(edit.newTypeInfo != null)
                    newArgTypes.Add(edit.newTypeInfo.argTypes.Select(e => RemoveGeneticPara(e)).ToList());
                if(edit.oldTypeInfo != null)
                    oldArgTypes.Add(edit.oldTypeInfo.className);
                if (edit.newTypeInfo != null && edit.oldTypeInfo != null &&
                    edit.newTypeInfo.argTypes.Equals(edit.oldTypeInfo.argTypes))
                    keepTypeInTrans = true;
            }
            foreach (var usage in newUsages)
                newArgTypes.Add(usage.Item2.argTypes.Select(e => RemoveGeneticPara(e)).ToList());
            
            var refinedOldUsages = new List<Record<Node, int, InvokeType>>();
            foreach (var oldUsage in oldUsages) {
                var curArgType = oldUsage.Item3.argTypes.Select(e => RemoveGeneticPara(e)).ToList();
                if (!(keepTypeInTrans^Contains(newArgTypes, curArgType)))
                    refinedOldUsages.Add(oldUsage);
            }
            return refinedOldUsages;
        }

        private bool Contains(List<List<string>> argTypes, List<string> targetArgType){
            foreach (var argType in argTypes){
                if (argType.Count() != targetArgType.Count())
                    continue;
                var sameType = true;
                for(int i = 0; i < argType.Count(); i++){
                    if(!argType[i].Equals(targetArgType[i])){
                        sameType = false;
                        break;
                    }
                }
                if (sameType) return true;
            }
            return false;
        }

        public List<Edit> GenerateNewExampleBasedOnAdditionalOutputAndInput(List<Edit> editExamples, List<Node> newUsages, List<Node> oldUsages) {
            // TODO anti-unify new usages, anti-unify old usages, extract useful transformation rule, generate additional examples;
            return editExamples;
        }

        public List<Edit> GenerateNewExampleBasedOnAdditionalOutput(List<Edit> editExamples, List<Node> newUsages) {
            List<Edit> newExamples = new List<Edit>();
            Node generalizedOutput = GeneralizeOutput(editExamples);
            Global.Log("generalized output is: " + generalizedOutput.GenerateCode());

            var interestingUsages = new List<Node>();
            foreach(var newUsage in newUsages){
                var s1 = new Stack<Node>();
                var s2 = new Stack<Node>();
                s1.Push(newUsage);
                s2.Push(generalizedOutput);

                while (!s1.IsEmpty() && !s2.IsEmpty()) {
                    var curUsage = s1.Pop();
                    var curGOutput = s2.Pop();

                    if (curGOutput.Label.Equals("Hole"))
                        continue;
                    else if (!curGOutput.Label.Equals(curUsage.Label) || // interesting new usages
                            curGOutput.Children.Count() != curUsage.Children.Count() ||
                            curGOutput.Children.Count()==0 && !curGOutput.GenerateCode().Equals(curUsage.GenerateCode()))
                    {
                        interestingUsages.Add(newUsage);
                        /*var parentNode = curGOutput.Parent;
                        if (parentNode != null) { 
                            for (int i = 0; i < parentNode.Children.Count(); i++)
                            {
                                if (NodeEqual(parentNode.Children[i], curGOutput))
                                {
                                    parentNode.ReplaceChildAt(i, StructNode.Create("Hole", BuildValueAttribute("hole"), null, parentNode));
                                    break;
                                }
                            }
                        }*/
                        break;
                    } else {
                        for (var i = 0; i < curGOutput.Children.Count(); i++){
                            s1.Push(curUsage.Children[i]);
                            s2.Push(curGOutput.Children[i]);
                        }
                    }
                }
            }

            foreach(var interestingUsage in interestingUsages){
                if(newExamples.Count() != 0 && 
                    newExamples.Select(e=>e.GetNewStructNode().GenerateCode()).Contains(interestingUsage.GenerateCode()))
                    continue;
                Console.WriteLine("Interesting new usage: " + interestingUsage.GenerateCode());
                var input = InferInput(editExamples.First(), interestingUsage);
                Console.WriteLine("Correponding input: " + input.GenerateCode());
                var newEdit = new Edit(input, interestingUsage, editExamples.First().id);
                if (newExamples.Count() == 0 ||
                    !newExamples.Select(e => e.GetOldStructNode().GenerateCode()).Contains(input.GenerateCode()))
                    newExamples.Add(newEdit);
            }

            if (!Config.OnlyNewUsage)
                newExamples.AddRange(editExamples);

            return newExamples;
        }

        public List<Edit> GenerateNewExampleBasedOnAdditionalInput(List<Edit> editExamples, List<Node> oldUsages) {
            List<Edit> newExamples = new List<Edit>(editExamples);

            foreach(var oldUsage in oldUsages){//.GetRange(0, Math.Min(consideredInput, oldUsages.Count()))){
                if(newExamples.Count() == 0 || newExamples.Select(e=>e.GetOldStructNode().GenerateCode()).Contains(oldUsage.GenerateCode()))
                    continue;
                Console.WriteLine("Old usage: " + oldUsage.GenerateCode());
                var output = InferOutput(editExamples.First(), oldUsage);
                Console.WriteLine("Correponding output: " + output.GenerateCode());
                var newEdit = new Edit(oldUsage, output, editExamples.First().id);
                newExamples.Add(newEdit);
            }

            return newExamples;
        }

        public static void TransformOldUsage(Program program, 
            List<Record<Node, int, InvokeType>> oldUsages, Dictionary<int, int> oldUsageResult)
        {
            foreach (var oldUsage in oldUsages)
            {
                Global.Log("old usage is " + oldUsage.Item1.GenerateCode());
                var predict = program.Run(oldUsage.Item1);

                if (predict != null && !predict.GenerateCode().Equals(oldUsage.Item1.GenerateCode())) {
                    Global.Log("predict is " + predict.GenerateCode());
                    oldUsageResult[oldUsage.Item2] = 1;
                }
                else{
                    Global.Log("predict is null");
                    if (oldUsageResult[oldUsage.Item2] != 1)
                        oldUsageResult[oldUsage.Item2] = -1;
                }
            }
        }

        public Node GeneralizeOutput(List<Edit> editExamples)
        {
            var exampleOutputs = editExamples.Select(e => e.GetNewStructNode()).ToList();
            Node generalizedNode = exampleOutputs[0].DeepClone();
            var s1 = new Stack<Node>();
            s1.Push(generalizedNode);

            var selectedNodes = editExamples[0].CalculateSelectNode();

            var nodeToXpath = new Dictionary<String, List<int>>();
            var xPath = new List<int>();
            nodeToXpath[generalizedNode.GenerateCode()] = xPath;

            while (!s1.IsEmpty()) {
                var currentNode = s1.Pop();
                var parentNode = currentNode.Parent;

                var currentNodeText = currentNode.GenerateCode();
                var childrenNodes = currentNode.Children;
                var currentXPath = nodeToXpath[currentNode.GenerateCode()];
                var childIndex = 0;
                if (currentXPath.Count() > 0)
                    childIndex = currentXPath[currentXPath.Count() - 1];

                bool generalized = false;
                /*if (selectedNodes.Contains(currentNode)) {
                    for (int i = 0; i<currentNode.Children.Count(); i++)
                        currentNode.ReplaceChildAt(i, StructNode.Create("Hole", BuildValueAttribute("hole"), null, currentNode));
                    continue;
                }*/

                // check the corresponding nodes in the other examples to determine whether generalize current node
                for (int i = 1; i < exampleOutputs.Count(); i++) {
                    var parallelNode = GetSubNode(exampleOutputs[i], new List<int>(currentXPath));
                    if( !currentNode.Label.Equals(parallelNode.Label) ||
                        childrenNodes.Count() != parallelNode.Children.Count() ||
                        childrenNodes.Count() == 0 && !currentNodeText.Equals(parallelNode.GenerateCode()))
                    {
                        // generalize current node
                        if (parentNode == null)
                            return StructNode.Create("Hole", BuildValueAttribute("hole"), null, null);
                        parentNode.ReplaceChildAt(childIndex, StructNode.Create("Hole", BuildValueAttribute("hole"), null, parentNode));
                        generalized = true;
                        break;
                    }
                }
                if(!generalized){
                    for (int j = 0; j < childrenNodes.Count(); j++){
                        s1.Push(childrenNodes[j]);

                        var newXPath = new List<int>(currentXPath);
                        newXPath.Add(j);
                        nodeToXpath[childrenNodes[j].GenerateCode()] = newXPath;
                    }
                }
            }

            return generalizedNode;
        }

        public Node InferInput(Edit editExample, Node newUsage){
            var input = editExample.GetOldStructNode();
            var output = editExample.GetNewStructNode();

            var substitutions = CalculateSubstitution(output, newUsage, editExample.CalculateSelectNode());

            substitutions = ExtendSubstitution(substitutions, input, output, editExample.CalculateSelectNode());
            foreach (var sub in substitutions)
                Global.Log("Subsititution: " + sub.Item1.GenerateCode() + ":" + sub.Item1.Label + " -> " + sub.Item2.GenerateCode() + ":" + sub.Item2.Label);

            var newInput = input.DeepClone();
            newInput = SubstituteNode(newInput, substitutions);
            return newInput;
        }

        public Node InferOutput(Edit editExample, Node oldUsage){
            var input = editExample.GetOldStructNode();
            var output = editExample.GetNewStructNode();

            var substitutions = CalculateSubstitution(input, oldUsage, editExample.CalculateSelectNode());

            foreach (var sub in substitutions)
                Global.Log("Subsititution: " + sub.Item1.GenerateCode() + ":" + sub.Item1.Label + " -> " + sub.Item2.GenerateCode() + ":" + sub.Item2.Label);

            var newOutput = output.DeepClone();
            newOutput = SubstituteNode(newOutput, substitutions);
            return newOutput;
        }

        private List<Record<Node, Node>> CalculateSubstitution(Node output, Node newUsage, List<Node> selectedNode) {
            var substitutions = new List<Record<Node, Node>>();
            var node1 = output.DeepClone();
            var node2 = newUsage.DeepClone();
            Stack<Node> s1 = new Stack<Node>();
            s1.Push(node1);
            Stack<Node> s2 = new Stack<Node>();
            s2.Push(node2);

            while (!s1.IsEmpty() && !s2.IsEmpty()) {
                var n1 = s1.Pop();
                var n2 = s2.Pop();

                var children1 = n1.Children;
                var children2 = n2.Children;
                if ((children1.Count() == 0 || children2.Count() == 0)
                    && !n1.GenerateCode().Equals(n2.GenerateCode()))
                {
                    substitutions.Add(new Record<Node, Node>(n1, n2));
                    continue;
                }
                else if (!n1.Label.Equals(n2.Label))
                {
                    substitutions.Add(new Record<Node, Node>(n1, n2));
                    continue;
                }
                else if (selectedNode.Contains(n1)) {
                    substitutions.Add(new Record<Node, Node>(n1, n2));
                    continue;
                }

                var size = Math.Min(children1.Count(), children2.Count());
                for (int i = 0; i < size; i++) {
                    var cn1 = children1[i];
                    var cn2 = children2[i];

                    s1.Push(cn1);
                    s2.Push(cn2);
                }
            }
            return substitutions;
        }

        private List<Record<Node, Node>> ExtendSubstitution(List<Record<Node, Node>> substitutions, Node input, Node output, List<Node> selectedNode) {
            var newSubstitutions = new List<Record<Node, Node>>(substitutions);
            var iosubs = CalculateSubstitution(output, input, selectedNode);

            foreach (var sub in substitutions) {
                var subOutput = sub.Item1;
                var newUsage = sub.Item2;
                if (newUsage.Label.Equals("GenericName") && subOutput.Label.Contains("Name")) { // transfer generic type
                    foreach (var iosub in iosubs) {
                        if (subOutput.GenerateCode().Equals(iosub.Item1.GenerateCode())) {
                            var newInput = newUsage.DeepClone();
                            var newSubstitution = new List<Record<Node, Node>>();
                            newSubstitution.Add(iosub);
                            newInput = SubstituteNode(newInput, newSubstitution);
                            newSubstitutions.Add(new Record<Node, Node>(iosub.Item2, newInput));
                        }
                    }
                }
                if (subOutput.Label.Equals("GenericName") && newUsage.Label.Contains("Name")){
                    // TODO:
                }
            }
            
            return newSubstitutions;
        }

        public Node SubstituteNode(Node node, List<Record<Node, Node>> substitutions){
            foreach (var substitution in substitutions) { 
                if (NodeEqual(substitution.Item1, node))
                    return substitution.Item2;
            }
            /*if (substitution.ContainsKey(node))
                return substitution[node];*/
            Stack<(Node, int)> s = new Stack<(Node, int)>();
            for (int i = 0; i < node.Children.Count(); i++) {
                s.Push((node.Children[i], i));
            }

            while (!s.IsEmpty()) {
                var (curNode, index) = s.Pop();
                var parentNode = curNode.Parent;
                foreach (var substitution in substitutions) {
                    if (NodeEqual(substitution.Item1, curNode) || curNode.GenerateCode().Equals("AsyncRetryPolicy") ) { //FIXME: overfit
                        /*for(int i = parentNode.Children.Count()-1; i >= 0 ; i--) { 
                            if(NodeEqual(parentNode.Children[i], curNode)) { 
                                parentNode.ReplaceChildAt(i, substitution.Item2);
                                substitution.Item2.Parent = parentNode;
                                substitutions.Remove(substitution);
                                goto loop_end;
                            }
                        }*/
                        parentNode.ReplaceChildAt(index, substitution.Item2);
                        substitution.Item2.Parent = parentNode;
                        substitutions.Remove(substitution);
                        goto loop_end;
                    }
                }
                
                for(int i = 0; i < curNode.Children.Count(); i++){
                    s.Push((curNode.Children[i], i));
                }
            loop_end: continue;
            }

            return node;
        }

        // generate a sub-node according to xPath
        public Node GetSubNode(Node node, List<int> xPath){
            if (xPath.Count() <= 0)
                return node;
            var children = node.Children;
            if (children.Count() <= 0 || children.Count() <= xPath.First())
                return null;
            else {
                var newNode = children[xPath.First()];
                xPath.RemoveAt(0);
                return GetSubNode(newNode, xPath);
            }
        }

        private bool NodeEqual(Node a, Node b) {
            if (a == null || b == null)
                return a == b;

            return a.Label.Equals(b.Label) &&
                a.GenerateCode().Equals(b.GenerateCode());
                //a.Parent.Label.Equals(b.Parent.Label);
        }

        private static string RemoveGeneticPara(string str) {
            string ret = "";
            var record = true;
            foreach (var c in str) {
                if (c == '<')
                    record = false;
                else if (c == '>')
                    record = true;
                else if (record)
                    ret += c;
            }
            return ret;
        }
    }
}