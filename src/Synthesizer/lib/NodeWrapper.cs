using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using ZSS;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using Microsoft.ProgramSynthesis.Transformation.Tree.Utils;

namespace Synthesizer {
    public class SNode : Node<SNode> {
        public StructNode node = null;
        public SyntaxToken token;
        public SNode(StructNode node){
            this.node = node;
            //this.label = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(node).ToString();
            this.label = node.Label;
        }

        public override string ToString() {
            return label;
        }

        public bool HasChildren() => node != null;
        public override List<Node<SNode>> GetChildren(){
            var sChildren = new List<Node<SNode>>();
            var children = node.Children;
            
            foreach(var child in children) {
                sChildren.Add(new SNode(child as StructNode));
            }
            return sChildren;
        }

        public override bool Equals(object obj){
            var other = obj as SNode;
            if (other == null)
                return false;
            else
                return node.Equals(other); //TODO
        }

        public override int GetHashCode(){
            return label.GetHashCode();
        }

        public override float Distance(Node<SNode> other)
        {
            var labelDis = (float)CSharpEngine.Utils.ContentDistance(node.GenerateCode(), (other as SNode).node.GenerateCode());
            return labelDis;
        }
    }
}