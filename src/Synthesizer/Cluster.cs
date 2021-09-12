using CSharpEngine;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Synthesizer
{
    public class Cluster
    {
        public List<Edit> edits = new List<Edit>();
        public List<Record<Node, InvokeType>> newUsages = new List<Record<Node, InvokeType>>();
        public List<Record<Node, int, InvokeType>> oldUsages = new List<Record<Node, int, InvokeType>>();

        public Cluster(Edit edit) {
            edits.Add(edit);
        }

        public void AddToCluster(Edit newEdit)
        {
            if (!edits.Contains(newEdit))
            {
                edits.Add(newEdit);
            }
        }

        public void AddToNewUsage(Node node, InvokeType invokeType)
        {
            if (!newUsages.Select(e => e.Item1.GenerateCode()).Contains(node.GenerateCode()))
                newUsages.Add(new Record<Node, InvokeType>(node, invokeType));
        }

        public void AddToOldUsage(Node node, int id, InvokeType invokeType)
        {
            oldUsages.Add(new Record<Node, int, InvokeType>(node, id, invokeType));
        }

        public override string ToString()
        {
            string ret = "============== Cluster start ==============\n";
            foreach (var edit in edits)
            {
                ret += edit.ToString() + "\n";
            }

            foreach (var usage in newUsages)
                ret += "new usages: " + usage.Item1.GenerateCode() + "\n";

            foreach (var usage in oldUsages)
                ret += "old usages: " + usage.Item1.GenerateCode() + "\n";

            ret += "============= Cluster end ==============\n";
            return ret;
        }
    }
}
