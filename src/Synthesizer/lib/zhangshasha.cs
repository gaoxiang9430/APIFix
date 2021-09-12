using System;
using System.IO;
using System.Collections.Generic;
using CSharpEngine;

namespace ZSS{
    
    public enum Operator{
        REMOVE,
        INSERT,
        UPDATE,
        MATCH,
    }

    public class Operation <T>{
        public Operator op = Operator.MATCH;
        public Node<T> node1, node2;
        public Operation(Operator op, Node<T> node1 = null, Node<T> node2 = null){
            this.op = op;
            this.node1 = node1;
            this.node2 = node2;
        }

        public override string ToString(){
            if (op == Operator.REMOVE)
                return "<Operation Remove: " + node1.label + ">";
            else if (op == Operator.INSERT)
                return "<Operation Insert: " + node2.label + ">";
            else if (op == Operator.UPDATE)
                return "<Operation Update: " + node1.label + " to " + node2.label + ">";
            else
                return "<Operation Match: " + node1.label + " to " + node2.label + ">";
        }

        public override bool Equals(object otherObj){
            var other = otherObj as Operation<T>;
            if (other == null) return false;
            return op == other.op && node1 == other.node1 && node2 == other.node2;
        }

        public override int GetHashCode(){
            return op.GetHashCode() + node1.GetHashCode() + node2.GetHashCode();
        }
    }

    public class AnnotatedTree <T>{
        public Node<T> root;
        public List<Node<T>> nodes;
        public List<int> ids;
        public List<int> lmds;

        // k and k' are nodes specified in the post-order enumeration.
        // keyroots = {k | there exists no k'>k such that lmd(k) == lmd(k')}
        // see paper for more on keyroots
        public List<int> keyroots;

        public override string ToString(){
            string ret = "node:";
            foreach(var node in nodes){
                ret += " " + node.ToString();
            }
            ret += "\nids:";
            foreach(var id in ids){
                ret += " " + id;
            }
            ret += "\nlmds:";
            foreach(var lmd in lmds){
                ret += " " + lmd;
            }
            return ret;
        }

        public AnnotatedTree(Node<T> root){
            this.root = root;
            nodes = new List<Node<T>>(); // a post-order enumeration of the nodes in the tree
            ids = new List<int>();       // a matching list of ids
            lmds = new List<int>();      // left most descendents

            var stack = new Stack<Record<Node<T>, List<int>>>();
            var pstack = new Stack<Record<Node<T>, int, List<int>>>();
            
            stack.Push(new Record<Node<T>, List<int>>(root, new List<int>()));
            var j = 0;
            while (stack.Count > 0){
                var rec = stack.Pop();
                var n = rec.Item1;
                var anc = rec.Item2;
                var nid = j;

                foreach (var c in n.GetChildren()){
                    var a = new List<int>(anc);
                    a.Insert(0, nid);
                    stack.Push(new Record<Node<T>, List<int>>(c, a));
                }
                pstack.Push(new Record<Node<T>, int, List<int>>(n, nid, anc));
                j += 1;
            }

            var lmds_temp = new Dictionary<int, int>();
            var keyroots_temp = new Dictionary<int, int>();
            var i = 0;
            while(pstack.Count > 0){
                var rec = pstack.Pop();
                var n = rec.Item1;
                var nid = rec.Item2;
                var anc = rec.Item3;
                
                nodes.Add(n);
                ids.Add(nid);
                var lmd = i;
                if (n.GetChildren() == null || n.GetChildren().Count == 0){                    
                    foreach(var a in anc){
                        if(!lmds_temp.ContainsKey(a)) lmds_temp[a] = i;
                        else break;
                    }
                }
                else{
                    lmd = lmds_temp[nid];
                }
                lmds.Add(lmd);
                keyroots_temp[lmd] = i;
                i += 1;
            }
            keyroots = new List<int>(keyroots_temp.Values);
            keyroots.Sort();
        }    
    }
    public class ZhangShaSha <T>{
        public AnnotatedTree<T> A, B;
        int size_a, size_b;
        float[,] treedists;
        List<Operation<T>>[,] operations;

        public ZhangShaSha(Node<T> node1, Node<T> node2){
            this.A = new AnnotatedTree<T>(node1);
            this.B = new AnnotatedTree<T>(node2);

            // Utils.LogTest(A.ToString());
            // Utils.LogTest(B.ToString());

            size_a = A.nodes.Count;
            size_b = B.nodes.Count;
            treedists = new float[size_a, size_b];
            operations = new List<Operation<T>>[size_a, size_b];
            for(int ii = 0; ii < size_a; ii++)
                for (int jj = 0; jj < size_b; jj++)
                    operations[ii, jj] = new List<Operation<T>>();
            
            foreach (var i in A.keyroots){
                foreach(var j in B.keyroots)
                    treedist(i, j);
            }
        }

        public int insert_cost(Node<T> node2) {
            return 1;
            // return node2.label.Length;
        }
        
        public int remove_cost(Node<T> node1) {
            return 1;
            // return node1.label.Length;
        }

        public float update_cost(Node<T> node1, Node<T> node2){
            if(node1 == null || node2 == null) return 1;
            if (node1.Equals(node2)) {
                return 0;
            }
            else
                return node1.Distance(node2);
        }

        public float simple_distance(){
            return treedists[size_a-1, size_b-1];
        }

        public List<Operation<T>> simple_edit(){
            return operations[size_a-1, size_b-1];
        }

        public void treedist(int i, int j){
            var Al = A.lmds;
            var Bl = B.lmds;
            var An = A.nodes;
            var Bn = B.nodes;

            var m = i - Al[i] + 2;
            var n = j - Bl[j] + 2;
            var fd = new float[m, n];
            var partial_ops = new List<Operation<T>>[m, n];
            for(int ii = 0; ii < m; ii++)
                for (int jj = 0; jj < n; jj++)
                    partial_ops[ii, jj] = new List<Operation<T>>();

            var ioff = Al[i] - 1;
            var joff = Bl[j] - 1;

            for(var x = 1; x < m; x++){ // δ(l(i1)..i, θ) = δ(l(1i)..1-1, θ) + γ(v → λ)
                var node = An[x+ioff];
                fd[x, 0] = fd[x-1, 0] + remove_cost(node);
                var op = new Operation<T>(Operator.REMOVE, node);
                partial_ops[x, 0] = new List<Operation<T>>(partial_ops[x-1, 0]);
                partial_ops[x, 0].Add(op);
            }
            for(var y=1; y<n; y++){ // δ(θ, l(j1)..j) = δ(θ, l(j1)..j-1) + γ(λ → w)
                var node = Bn[y+joff];
                fd[0, y] = fd[0, y-1] + insert_cost(node);
                var op = new Operation<T>(Operator.INSERT, null, node);
                partial_ops[0, y] = new List<Operation<T>>(partial_ops[0, y-1]);
                partial_ops[0, y].Add(op);
            }

            for (var x=1; x<m; x++){  // the plus one is for the xrange impl
                for (var y=1; y<n; y++){
                    // x+ioff in the fd table corresponds to the same node as x in
                    // the treedists table (same for y and y+joff)
                    var node1 = An[x+ioff];
                    var node2 = Bn[y+joff];
                    // only need to check if x is an ancestor of i
                    // and y is an ancestor of j
                    if (Al[i] == Al[x+ioff] && Bl[j] == Bl[y+joff]){
                        //                   +-
                        //                   | δ(l(i1)..i-1, l(j1)..j) + γ(v → λ)
                        // δ(F1 , F2 ) = min-+ δ(l(i1)..i , l(j1)..j-1) + γ(λ → w)
                        //                   | δ(l(i1)..i-1, l(j1)..j-1) + γ(v → w)
                        //                   +-
                        float cost = 0, min_index = 0;
                        if(fd[x-1, y] + remove_cost(node1) <= fd[x, y-1] + insert_cost(node2)){
                            cost = fd[x-1, y] + remove_cost(node1);
                            min_index = 0;
                        } else{
                            cost = fd[x, y-1] + insert_cost(node2);
                            min_index = 1;
                        }

                        if(fd[x-1, y-1] + update_cost(node1, node2) < cost){
                            cost = fd[x-1, y-1] + update_cost(node1, node2);
                            min_index = 2;
                        }
                        fd[x, y] = cost;

                        if (min_index == 0){
                            var op = new Operation<T>(Operator.REMOVE, node1, null);
                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[x-1, y]);
                            partial_ops[x, y].Add(op);
                        }
                        else if (min_index == 1){
                            var op = new Operation<T>(Operator.INSERT, null, node2);
                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[x, y - 1]);
                            partial_ops[x, y].Add(op);
                        }
                        else{
                            var op_type = Operator.MATCH;
                            if (fd[x, y] != fd[x-1, y-1])
                                op_type = Operator.UPDATE;
                            var op = new Operation<T>(op_type, node1, node2);
                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[x - 1, y - 1]);
                            partial_ops[x, y].Add(op);
                        }

                        operations[x + ioff, y + joff] = partial_ops[x, y];
                        treedists[x+ioff, y+joff] = fd[x, y];
                    }
                    else{
                        //                   +-
                        //                   | δ(l(i1)..i-1, l(j1)..j) + γ(v → λ)
                        // δ(F1 , F2 ) = min-+ δ(l(i1)..i , l(j1)..j-1) + γ(λ → w)
                        //                   | δ(l(i1)..l(i)-1, l(j1)..l(j)-1)
                        //                   |                     + treedist(i1,j1)
                        //                   +-
                        var p = Al[x+ioff]-1-ioff;
                        var q = Bl[y+joff]-1-joff;

                        float cost = 0, min_index = 0;
                        if(fd[x-1, y] + remove_cost(node1) <= fd[x, y-1] + insert_cost(node2)){
                            cost = fd[x-1, y] + remove_cost(node1);
                            min_index = 0;
                        } else{
                            cost = fd[x, y-1] + insert_cost(node2);
                            min_index = 1;
                        }
                        if(fd[p, q] + treedists[x+ioff, y+joff] < cost){
                            cost = fd[p, q] + treedists[x+ioff, y+joff];
                            min_index = 2;
                        }
                        fd[x, y] = cost;
                        
                        if(min_index == 0){
                            var op = new Operation<T>(Operator.REMOVE, node1);

                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[x-1, y]);
                            partial_ops[x, y].Add(op);
                        }
                        else if (min_index == 1){
                            var op = new Operation<T>(Operator.INSERT, null, node2);
                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[x, y-1]);
                            partial_ops[x, y].Add(op);
                        }
                        else{
                            partial_ops[x, y] = new List<Operation<T>>(partial_ops[p, q]);
                            partial_ops[x, y].AddRange(operations[x+ioff, y+joff]);
                        }
                    }
                }
            }
        }
    }
}