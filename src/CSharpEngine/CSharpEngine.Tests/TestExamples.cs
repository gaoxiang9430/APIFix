
using Xunit;
using System;

using Microsoft.ProgramSynthesis.Transformation.Tree;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using Microsoft.ProgramSynthesis.Wrangling.Constraints;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace CSharpEngine.Tests
{
    public class TestExamples{
        [Fact]
        public void TestTTree () {
            Attributes.SetKnownSoftAttributes(new string[] {});
            Example<Node, Node> example1 = CreateExample("a","b");
            Example<Node, Node> example2 = CreateExample("c","d");
            Example<Node, Node>[] examples = new[] { example1, example2 };

            Program program = Learner.Instance.Learn(examples);
            Assert.NotNull(program);
            
            Example<Node, Node> example3 = CreateExample("e","f");
            Node output = program.Run(example3.Input);
            Utils.LogTest(output.GenerateCode());
            foreach(var child in output.Children)
                Utils.LogTest(child.GenerateCode());
            Assert.Equal(example3.Output, output);
        } 

        private Example<Node, Node> CreateExample(string lhsExp, string rhsExp) {
            Node inputNode = StructNode.Create("binaryExp", BuildValueAttribute("=="),
                                            new Node[] {
                                                StructNode.Create("lhs", BuildValueAttribute(lhsExp)),
                                                StructNode.Create("rhs", BuildValueAttribute(rhsExp)) 
                                            });
            Node outputNode = StructNode.Create("binaryExp", BuildValueAttribute("=="),
                                            new Node[] {
                                                StructNode.Create("rhs", BuildValueAttribute(rhsExp)),
                                                StructNode.Create("lhs", BuildValueAttribute(lhsExp))
                                            });
            return  new Example<Node, Node>(inputNode, outputNode);
        }

        [Fact]
        public void TestSemanticModel(){
            var tree = CSharpSyntaxTree.ParseText(@"
                public class MyClass {
                    int Method1() { return 0; }
                    void Method2()
                    {
                        string a = new String();
                        a.ToString().Substring(0,2);
                    }                    
                }");

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create("HelloWorld")
                .AddReferences(MetadataReference.CreateFromFile(
                    typeof(string).Assembly.Location))
                .AddSyntaxTrees(tree);
            var model = compilation.GetSemanticModel(tree);

            foreach (var invocationSyntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>()){
                Console.WriteLine(invocationSyntax);         //MyClass.Method1()
                var invokedSymbol = model.GetSymbolInfo(invocationSyntax).Symbol; //Same as MyClass.Method1

                if (invokedSymbol != null){
                    Console.WriteLine(invokedSymbol.ToString());         
                    Console.WriteLine(invokedSymbol.ContainingSymbol);   
                }
            }
        }
    }
}