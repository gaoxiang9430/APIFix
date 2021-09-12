using System;
using System.Collections.Generic;
using System.Linq;
using CSharpEngine;
using Microsoft.ProgramSynthesis.Transformation.Tree.Utils;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using Microsoft.ProgramSynthesis.Utils.Interactive;
using Microsoft.ProgramSynthesis.Wrangling.Tree;

namespace Synthesizer{

    public class ClusterAlgo {

        public static int Threshold = 2;
        public static double Threshold2 = 0.2;
        //public static double Threshold3 = 0.35;

        public static double Threshold3 = 0.01;
        public static double Threshold4 = 0.1;

        public static List<Cluster> ClusterBasedOnCloneDection(List<Edit> edits, string targetAPI)
        {
            var clusters = new List<Cluster>();
            //edits = TrimEdit(edits, targetAPI);

            foreach (var edit in edits)
            {
                bool findCluster = false;
                foreach (var cluster in clusters)
                {
                    double minDis = Threshold2;
                    foreach(var refer in cluster.edits){
                        var dis = CloneDetectionDistance(edit.GetOldStructNode(), refer.GetOldStructNode());
                        if (dis < minDis)
                            minDis = dis;
                    }
                    
                    if (minDis < Threshold2)
                    {
                        cluster.AddToCluster(edit);
                        findCluster = true;
                        break;
                    }
                }
                if (!findCluster)
                    clusters.Add(new Cluster(edit));
            }

            return clusters;
        }

        public static double CloneDetectionDistance(Node node1, Node node2)
        {
            var vec1 = GenerateTypeCountVec(node1);
            var size1 = vec1.Select(e => e.Value).Sum();
            var vec2 = GenerateTypeCountVec(node2);
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

        static void PrintNode(Node node) {
            Console.WriteLine(node.GenerateCode() + " : " + node.Label + " : " + node.Parent?.Label);
            foreach (var child in node.Children)
                PrintNode(child);
        }

        public static List<Cluster> ClusterBasedOnAntiUnification(List<Edit> edits, string otargetAPI, string ntargetAPI, 
            List<RelevantClient> newUsages=null, List<Record<Node, InvokeType>> oldUsages = null) {
            var clusters = new List<Cluster>();

            var unRolledEdits = UnRollEdits(edits, otargetAPI, ntargetAPI);
            Global.Log("the size of unRolledEdits is: " + unRolledEdits.Count());
            foreach (var edit in unRolledEdits) {
                var selectedNode = edit.CalculateSelectNode(ntargetAPI);

                Cluster similarCluster = null;
                double minDis = Threshold3;
                foreach (var cluster in clusters) {
                    foreach (var refer in cluster.edits) {
                        var dis1 = AntiUnificationDis(edit.GetAbsOldStructNode(), refer.GetAbsOldStructNode());
                        var dis2 = AntiUnificationDis(edit.GetAbsNewStructNode(), refer.GetAbsNewStructNode());
                        var dis = (dis1 + dis2) / 2;
                        if (dis < minDis) {
                            minDis = dis;
                            similarCluster = cluster;
                        }
                    }
                }
                if (minDis < Threshold3) {
                    similarCluster.AddToCluster(edit);
                }
                else { 
                    clusters.Add(new Cluster(edit));
                }
            }

            if (newUsages != null) {
                Console.WriteLine("size of new usage: " + newUsages.Count());
                foreach (var usage in newUsages) {
                    var interestingSubtrees = ExtractInterestingSubtree(usage.GetStructNode(), otargetAPI, Config.NewKeyWords);
                    if (interestingSubtrees == null)
                        continue;
                    foreach (var cluster in clusters) {
                        var representativeEdit = cluster.edits[0];
                        var matchedSubTrees = findMatchedSubTree(interestingSubtrees, representativeEdit.GetNewStructNode(),
                            representativeEdit.CalculateSelectNode(ntargetAPI), representativeEdit.GetAbsNewStructNode(), Config.NewUsageThreashold, ntargetAPI);
                        if (matchedSubTrees.Count() != 0) {
                            foreach(var matchedSubTree in matchedSubTrees) {
                                Global.Log("add new usage " + matchedSubTree.GenerateCode());
                                cluster.AddToNewUsage(matchedSubTree, usage.invocationType);
                            }
                            // break;
                        }
                    }
                }
            }

            if (oldUsages != null) {
                for (var usageId = 0; usageId < oldUsages.Count(); usageId++) {
                    var usage = oldUsages[usageId];
                    var interestingSubtrees = ExtractInterestingSubtree(usage.Item1, otargetAPI, Config.OldKeyWords);

                    foreach (var cluster in clusters) {
                        var representativeEdit = cluster.edits[0];
                        var matchedSubTrees = findMatchedSubTree(interestingSubtrees, representativeEdit.GetOldStructNode(),
                            representativeEdit.CalculateSelectNode(ntargetAPI), representativeEdit.GetAbsOldStructNode(), Config.OldUsageThreashold, otargetAPI);
                        if (matchedSubTrees.Count() != 0) {
                            foreach (var matchedSubTree in matchedSubTrees) {
                                Global.Log("add old usage " + matchedSubTree.GenerateCode());
                                cluster.AddToOldUsage(matchedSubTree, usageId, usage.Item2);
                            }
                            //break;
                        }
                    }
                }
            }

            return clusters;
        }

        private static List<Node> findMatchedSubTree(List<Node> usages, Node editInput, List<Node> selectedNodes, Node absNode, double threshold, string target) {
            var retNodes = new List<Node>();
            foreach (var subtree in usages) {
                if (!subtree.Label.Equals(editInput.Label))
                    continue;
                
                var absUsage = AntiUnification(subtree, editInput, selectedNodes);
                var dis = AntiUnificationDis(absUsage, absNode);
                if (subtree.GenerateCode().Contains("PolicyWrap policyWrap = Policy.WrapAsync")) {
                    Console.WriteLine(subtree.GenerateCode());
                    Console.WriteLine(editInput.GenerateCode());
                    Console.WriteLine("Abstracted " + absUsage.GenerateCode());
                    PrintNode(absUsage);
                    Console.WriteLine("Abstracted " + absNode.GenerateCode());
                    PrintNode(absNode);
                    Console.WriteLine("Dis: " + dis + "\n");
                }

                if (NodeContains(absUsage, target) && dis < threshold) {
                    retNodes.Add(subtree);
                }
            }
            return retNodes;
        }

        private static bool NodeContains(Node node, string target) {
            if (node.Children.Count() == 0) {
                if (node.GenerateCode().Equals(target))
                    return true;
            }
            else {
                foreach (var subNode in node.Children)
                    if (NodeContains(subNode, target))
                        return true;
            }
            return false;
        }

        private static List<Node> ExtractInterestingSubtree(Node usage, string targetAPI, string additionalKeywords) {
            List<Node> interestingSubtrees = new List<Node>();
            if (usage == null)
                return interestingSubtrees;
            Stack<Node> s = new Stack<Node>();
            s.Push(usage);
            while (!s.IsEmpty()) {
                var curNode = s.Pop();
                if (!curNode.GenerateCode().Contains(targetAPI))
                    continue;
                if (additionalKeywords != null && !curNode.GenerateCode().Contains(additionalKeywords))
                    continue;
                interestingSubtrees.Add(curNode);
                foreach (var subNode in curNode.Children)
                    s.Push(subNode);
            }
            return interestingSubtrees;
        }

        private static List<Edit> UnRollEdits(List<Edit> edits, string otargetAPI, string ntargetAPI) {
            List<Edit> unRolledEdits = new List<Edit>();
            foreach (var edit in edits) {
                if (!edit.oldNodeText.Contains(otargetAPI) || !edit.newNodeText.Contains(ntargetAPI))
                    continue;

                // unroll the edit into single edits
                if (edit.GetOldStructNode().Label.Equals("DummyBlock") && edit.GetNewStructNode().Label.Equals("DummyBlock"))
                {
                    var oChildren = edit.GetOldStructNode().Children;
                    var nChildren = edit.GetNewStructNode().Children;

                    Node oChild = null, nChild = null;
                    for (var i = 0; i < oChildren.Length; i++)
                    {
                        var tempChild = oChildren[i] as StructNode;
                        if (tempChild.GenerateCode().Contains(otargetAPI))
                        {
                            oChild = tempChild;
                            break;
                        }
                    }
                    for (var i = 0; i < nChildren.Length; i++)
                    {
                        var tempChild = nChildren[i] as StructNode;
                        if (tempChild.GenerateCode().Contains(ntargetAPI))
                        {
                            nChild = tempChild;
                            break;
                        }
                    }
                    if (oChild != null && nChild != null)
                        unRolledEdits.Add(new Edit(oChild, nChild, otargetAPI));
                } else {
                    if (!unRolledEdits.Contains(edit))
                        unRolledEdits.Add(edit);
                }
                
                var oInstance = FindInvocationNode(edit.GetOldStructNode(), otargetAPI);
                var nInstance = FindInvocationNode(edit.GetNewStructNode(), ntargetAPI);

                if(oInstance != null && !oInstance.GenerateCode().Equals(edit.GetOldStructNode()) && nInstance != null) {
                    if (oInstance.GenerateCode().Equals(nInstance.GenerateCode()))
                        continue;
                    var newEdit = new Edit(oInstance as StructNode, nInstance as StructNode, otargetAPI);
                    if (!unRolledEdits.Contains(newEdit))
                        unRolledEdits.Add(newEdit);
                }
            }
            return unRolledEdits;
        }

        private static Node FindInvocationNode(Node node, string targetAPI) {
            var descendants = Descendants(node);
            foreach (var descendant in descendants) {
                // if (descendant.Label.Equals("InvocationExpression"))
                //    Global.Log(descendant.GenerateCode() + " " + descendant.Children[0].GenerateCode());
                if (descendant.Label.Equals("ObjectCreationExpression") && descendant.Children[1].GenerateCode().StartsWith(targetAPI))
                    return descendant;
                else if (descendant.Label.Equals("InvocationExpression") && 
                    (descendant.Children[0].GenerateCode().EndsWith("." + targetAPI) || descendant.Children[0].GenerateCode().Equals(targetAPI)))
                    return descendant;
            }
            return null;
        }

        private static List<Node> Descendants(Node node) {
            var descedants = new List<Node>();
            descedants.Add(node);
            foreach (var child in node.Children) {
                descedants.AddRange(Descendants(child));
            }
            return descedants;
        }

        private static Dictionary<string, int> GenerateTypeCountVec(Node node, Dictionary<string, int> vec = null) {
            if (vec == null)
                vec = new Dictionary<string, int>();
            if (vec.ContainsKey(node.Label))
                vec[node.Label] += 1;
            else
                vec.Add(node.Label, 1);

            if (node.Label.Equals("IdentifierName")) { 
                if (vec.ContainsKey(node.GenerateCode()))
                    vec[node.GenerateCode()] += 1;
                else
                    vec.Add(node.GenerateCode(), 1);
            }

            foreach (var child in node.Children)
            {
                GenerateTypeCountVec(child, vec);
            }
            return vec;
        }

        public static float AntiUnificationDis(Node node1, Node node2) {
            node1 = node1.DeepClone();
            node2 = node2.DeepClone();

            if (node1.Label != node2.Label)
                return 1;

            Stack<Node> s1 = new Stack<Node>();
            s1.Push(node1);
            Stack<Node> s2 = new Stack<Node>();
            s2.Push(node2);

            float total_num = Descendants(node1).Count() + Descendants(node2).Count();
            float same_num = 0;

            float new_same_num = 1;
            float new_diff_num = 0;

            while (!s1.IsEmpty() && !s2.IsEmpty())
            {
                var n1 = s1.Pop();
                var n2 = s2.Pop();
                /*if (n1.Label.Equals("TypeArgumentList") || n2.Label.Equals("TypeArgumentList")) { // ignore genetic type when calculate distance
                    same_num += Math.Max(Descendants(n1).Count(), Descendants(n2).Count());
                    continue;
                }
                else*/
                same_num += 2;

                var children1 = new List<Node>();
                var children2 = new List<Node>();
                for (int i = 0; i < n1.Children.Count(); i++) {
                    if (n1.Children[i].Label.Equals("TypeArgumentList")) // ignore genetic type when calculate distance
                        same_num += Descendants(n1.Children[i]).Count();
                    else
                        children1.Add(n1.Children[i]);
                }

                for (int i = 0; i < n2.Children.Count(); i++) {
                    if (n2.Children[i].Label.Equals("TypeArgumentList"))
                        same_num += Descendants(n2.Children[i]).Count();
                    else
                        children2.Add(n2.Children[i]);
                }

                var size = Math.Min(children1.Count(), children2.Count());
                new_diff_num += Math.Abs(children1.Count() - children2.Count());

                // partially implement node alignments
                if(children1.Count() - children2.Count() == 1)
                    (children1, children2) = AlignmentChildren(children1, children2);
                if (children2.Count() - children1.Count() == 1)
                    (children2, children1) = AlignmentChildren(children2, children1);


                for (int i = 0; i < size; i++)
                {
                    var cn1 = children1[i];
                    var cn2 = children2[i];
                    // if ((cn1.Label.Equals("GenericName") && cn2.Label.Contains("Name")) ||
                    //    (cn2.Label.Equals("GenericName") && cn1.Label.Contains("Name"))) {
                    if ((Utils.IsGenericType(cn1) && cn2.Label.Contains("Name")) ||
                        (Utils.IsGenericType(cn2) && cn1.Label.Contains("Name")))
                    {
                        same_num += Descendants(cn1).Count() + Descendants(cn2).Count();
                        new_same_num += 1;
                    }
                    else if (cn1.Label != cn2.Label
                        || (cn1.Children.Count() == 0 && cn2.Children.Count() == 0 && cn1.GenerateCode() != cn2.GenerateCode()))
                    {
                        n1.ReplaceChildAt(i, StructNode.Create("Hole", BuildValueAttribute("hole"), null, n1));
                        n2.ReplaceChildAt(i, StructNode.Create("Hole", BuildValueAttribute("hole"), null, n2));
                        new_diff_num += 1;
                    }
                    else
                    {
                        s1.Push(cn1);
                        s2.Push(cn2);
                        new_same_num += 1;
                    }
                }
            }

            return 1 - new_same_num / (new_same_num + new_diff_num);
            //return 1 - same_num / total_num;
        }

        private static (List<Node>, List<Node>) AlignmentChildren(List<Node> children1, List<Node> children2) {
            // assume size1 - size2 = 1
            var size1 = children1.Count();
            var size2 = children2.Count();

            var skippedIndex = 0;
            int bestMatch = 0, bestSkippedIndex = 0;
            while (skippedIndex < size1) {
                int index1 = 0;
                int index2 = 0;
                int sameNode = 0;
                while (index1 < size1 && index2 < size2) {
                    if (index1 == skippedIndex)
                        index1++;
                    if (children1[index1].Label == children2[index2].Label)
                        sameNode++;
                    index1++; index2++;
                }
                if (sameNode > bestMatch) {
                    bestMatch = sameNode;
                    bestSkippedIndex = skippedIndex;
                }
                skippedIndex++;
            }
            children1.RemoveAt(bestSkippedIndex);
            return (children1, children2);
        }

        private static void DCap(Node node, int D) {
            if (D == 1)
            {
                var children = node.Children;
                for (int i = 0; i < children.Length; i++)
                {
                    node.RemoveChild(children[i]);
                }
            }
            else {
                var children = node.Children;
                for (int i = 0; i < children.Length; i++)
                {
                    DCap(children[i], D - 1);
                }
            }
        }

        public static Node AntiUnification(Node node1, Node node2, List<Node> selectedNode = null)
        {
            node1 = node1.DeepClone();
            node2 = node2.DeepClone();
            List<string> selectCode = null;
            if (selectedNode != null)
                selectCode = selectedNode.Select(e => e.GenerateCode()).ToList();

            Stack<Node> s1 = new Stack<Node>();
            s1.Push(node1);
            Stack<Node> s2 = new Stack<Node>();
            s2.Push(node2);

            while (!s1.IsEmpty() && !s2.IsEmpty())
            {
                var n1 = s1.Pop();
                var n2 = s2.Pop();

                var children1 = n1.Children;
                var children2 = n2.Children;

                var size = Math.Min(children1.Count(), children2.Count());
                for (int i = 0; i < size; i++)
                {
                    var cn1 = children1[i];
                    var cn2 = children2[i];

                    //if (selectCode.Contains(cn2.GenerateCode())) {
                    if (IsSelectNode(cn2, selectedNode)) {
                        //n1.RemoveChild(cn1);
                        if (cn1.Label.Equals("PredefinedType") || cn1.Label.Equals("IdentifierName") 
                            || cn1.Children.Count() == 0)
                            cn1.Parent.RemoveChild(cn1);
                        else
                            DCap(cn1, 1);
                    }

                    s1.Push(cn1);
                    s2.Push(cn2);
                }
            }

            return node1;
        }

        private static bool IsSelectNode(Node node, List<Node> selectedNode)
        {
            foreach (var sn in selectedNode)
            {
                if (sn.GenerateCode().Equals(node.GenerateCode()) && sn.Label == node.Label)
                    return true;
            }
            return false;
        }
    }
}