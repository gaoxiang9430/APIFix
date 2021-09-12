using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ProgramSynthesis.Transformation.Tree.Utils;
using Microsoft.ProgramSynthesis.Utils.Interactive;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpEngine
{
    public class Edit
    {
        private List<SyntaxNodeOrToken> oldNodes;
        private List<SyntaxNodeOrToken> newNodes;

        private Node oldStructureNodes;
        private Node newStructureNodes;
        private List<Node> selectedNodes = null;

        private Node absOldStructureNodes;
        private Node absNewStructureNodes;

        public InvokeType oldTypeInfo;
        public InvokeType newTypeInfo;

        public string oldNodeText = "";
        public string newNodeText = "";
        public string inputPath;
        public string outputPath;
        public string id;

        public Edit() { }

        public Edit(List<SyntaxNodeOrToken> oldNodes, List<SyntaxNodeOrToken> newNodes, string id)
        {
            this.newNodes = newNodes;
            this.oldNodes = oldNodes;

            foreach (var str in oldNodes.Select(e => e.ToString()))
                oldNodeText += "- " + str + "\n";

            foreach (var str in newNodes.Select(e => e.ToString()))
                newNodeText += "+ " + str + "\n";

            oldStructureNodes = Translator.Translate(oldNodes);
            newStructureNodes = Translator.Translate(newNodes);

            // just consider the type of API invocation statement
            if(Config.CompilationMode && oldNodes.Count() == 1)
                oldTypeInfo = InvocationNodeType.GenerateType(oldNodes[0].AsNode(), "old");
            if(Config.CompilationMode && newNodes.Count() == 1)
                newTypeInfo = InvocationNodeType.GenerateType(newNodes[0].AsNode(), "new");

            this.id = id;
        }

        public Edit(Node oldNode, Node newNode, string id)
        {
            oldStructureNodes = oldNode;
            newStructureNodes = newNode;

            oldNodeText = oldNode.GenerateCode() + "\n";
            newNodeText = newNode.GenerateCode() + "\n";

            this.id = id;
        }

        public List<Node> CalculateSelectNode(string target = null) {
            if (selectedNodes != null)
                return selectedNodes;

            selectedNodes = new List<Node>();
            var oldNodeChildren = Descendants(oldStructureNodes).Select(e => e.GenerateCode());

            Stack<Node> visitedNode = new Stack<Node>();
            visitedNode.Push(newStructureNodes);
            while (!visitedNode.IsEmpty()) {
                var currentNode = visitedNode.Pop();
                if ((currentNode.Label.Contains("Token") && !currentNode.Label.Equals("IdentifierToken")) || currentNode.GenerateCode().Equals(target))
                    continue;
                if (oldNodeChildren.Contains(currentNode.GenerateCode()) && !ContainIdentifier(currentNode, target))
                    selectedNodes.Add(currentNode);
                else {
                    foreach (var child in currentNode.Children)
                        visitedNode.Push(child);
                }
            }
            absOldStructureNodes = AbstractNodeWithSelectedNode(oldStructureNodes, selectedNodes);
            absNewStructureNodes = AbstractNodeWithSelectedNode(newStructureNodes, selectedNodes);
            return selectedNodes;
        }

        public bool ContainIdentifier(Node node, string target) {
            var children = Descendants(node);
            foreach (var child in children) {
                if (child.GenerateCode().Equals(target))
                    return true;
            }
            return false;
        }

        private static Node AbstractNodeWithSelectedNode(Node node, List<Node> selectedNode)
        {
            var newNode = node.DeepClone();

            var selectNodeCode = selectedNode.Select(e => e.GenerateCode());
            Stack<Node> visitedNode = new Stack<Node>();
            visitedNode.Push(newNode);
            while (!visitedNode.IsEmpty())
            {
                var currentNode = visitedNode.Pop();
                if (selectNodeCode.Contains(currentNode.GenerateCode()))
                {
                    if (currentNode.Parent != null) { 
                        if (currentNode.Label.Equals("PredefinedType") || currentNode.Label.Equals("IdentifierName")
                            || currentNode.Children.Count() == 0)
                            currentNode.Parent.RemoveChild(currentNode);
                        else
                            DCap(currentNode, 1);
                    }
                }
                else
                {
                    foreach (var child in currentNode.Children)
                        visitedNode.Push(child);
                }
            }
            return newNode;
        }

        private static void DCap(Node node, int D)
        {
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
                    DCap(children[i], D-1);
                }
            }
        }

        public static List<Node> Descendants(Node node)
        {
            var descedants = new List<Node>();
            descedants.Add(node);
            foreach (var child in node.Children)
            {
                descedants.AddRange(Descendants(child));
            }
            return descedants;
        }


        public override bool Equals(object obj)
        {
            var other = obj as Edit;
            if (other == null)
                return false;
            return oldStructureNodes.GenerateCode().Equals(other.GetOldStructNode().GenerateCode());
        }

        public override int GetHashCode()
        {
            /*int ret = id.GetHashCode();
            foreach (var str in oldNodes.Select(e => e.ToString()))
                ret += str.GetHashCode();
            foreach (var str in newNodes.Select(e => e.ToString()))
                ret += str.GetHashCode();*/
            int ret = id.GetHashCode();
            ret += 119 * oldStructureNodes.GetHashCode();
            ret += 131 * newStructureNodes.GetHashCode();
            return ret;
        }

        public void SetStructureNode(Node oldSNode, Node newSNode)
        {
            oldStructureNodes = oldSNode;
            newStructureNodes = newSNode;
        }

        public Node GetOldStructNode() => oldStructureNodes;
        public Node GetNewStructNode() => newStructureNodes;

        public Node GetAbsOldStructNode() => absOldStructureNodes;
        public Node GetAbsNewStructNode() => absNewStructureNodes;

        public string GetInputPath() => inputPath;
        public string GetOutputPath() => outputPath;

        public override String ToString()
        {
            string ret = "";
            ret += oldNodeText; // + " => " + absOldStructureNodes.GenerateCode();
            ret += "----------------\n";
            ret += newNodeText; // + " => " + absNewStructureNodes.GenerateCode(); ;
            /*foreach (var str in oldNodes.Select(e => e.ToString()))
                ret += "- " + str + "\n";
            foreach (var str in newNodes.Select(e => e.ToString()))
                ret += "+ " + str + "\n";
            foreach (var str in selectedNodes)
                ret += "selectNode: " + str.GenerateCode() + ":" + str.Label + "\n";*/
            return ret;
        }
    }
}
