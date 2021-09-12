using System;
using System.Collections.Generic;
using CSharpEngine;

namespace ZSS{

    public abstract class Node<T>
    {
        public string label;
        
        public abstract override string ToString();
        public abstract List<Node<T>> GetChildren();
        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();
        public abstract float Distance(Node<T> other);
    }

    public class SimpleNode : Node<SimpleNode> {
        private List<Node<SimpleNode>> children = new List<Node<SimpleNode>>();
        public SimpleNode(string label){
            this.label = label;
        }

        public override string ToString() {
            return label;
        }

        public override List<Node<SimpleNode>> GetChildren(){
            return children;
        }

        public override bool Equals(object obj){
            var other = obj as SimpleNode;
            if (other == null)
                return false;
            else
                return label == other.label; //TODO
        }

        public override int GetHashCode(){
            return label.GetHashCode();
        }

        public SimpleNode AddChild(SimpleNode child){
            children.Add(child);
            return this;
        }

        public override float Distance(Node<SimpleNode> other)
        {
            var labelDis = (float)Utils.ContentDistance(label, other.label);
            return labelDis;
        }
    }
}