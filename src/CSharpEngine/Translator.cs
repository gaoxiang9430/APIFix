using Microsoft.CodeAnalysis;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using Microsoft.ProgramSynthesis.Transformation.Tree;
using Microsoft.ProgramSynthesis.Wrangling.Constraints;
using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;

namespace CSharpEngine{
    public class Translator {

        public static StructNode Translate(List<SyntaxNodeOrToken> nodes, StructNode parent = null){
            if (nodes.Count == 0)
                return null;
            else if (nodes.Count == 1)
                return Translate(nodes[0]);
            else { 
                var dummyBlock = StructNode.Create("DummyBlock", BuildValueAttribute(""), null);
                foreach(SyntaxNodeOrToken node in nodes){
                    var structNode = Translate(node, dummyBlock);
                }
                return dummyBlock;
            }
        }

        public static StructNode Translate(SyntaxNodeOrToken node, StructNode parent = null){
            if (node == null)
                return null;
            
            if (parent == null && node.Parent != null){
                var kind = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(node.Parent).ToString();                
                parent = StructNode.Create(kind, BuildValueAttribute(""), null, null);
            }

            if (node.IsToken){
                var kind = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(node).ToString();
                var leafNode = StructNode.Create(kind, BuildValueAttribute(node.AsToken().ToString()), null, parent);
                if (parent != null)
                    parent.AddChild(leafNode);
                return leafNode;
            } else if(node.IsNode){
                var kind = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(node).ToString();
                var childNodes = node.ChildNodesAndTokens();
                var length = childNodes.Count;

                var structnode = StructNode.Create(kind, BuildValueAttribute(""), null, parent);
                for(var i = 0; i < length; i++){
                    var child = node.ChildNodesAndTokens()[i];
                    Translate(child, structnode);
                }
                if (parent != null)
                    parent.AddChild(structnode);
                return structnode;
            }
            else
                return null;
        }

        public static Example<Node, Node> CreateExample(Node inputNode, Node outputNode){
            return new Example<Node, Node>(inputNode, outputNode);
        }

        public static Program Learn(Example<Node, Node>[] examples){
            return Learner.Instance.Learn(examples);
        }

        public static void storeNode(Node node, string fileName){
            var xmlNode = node.SerializeToXml();
            using (StreamWriter outputFile = new StreamWriter(fileName))
                outputFile.Write(xmlNode.ToString());
        }

        public static StructNode loadNode(string fileName){
            var xmlNode = XElement.Load(fileName);
            return StructNode.DeserializeFromXml(xmlNode);
        }
    }
}