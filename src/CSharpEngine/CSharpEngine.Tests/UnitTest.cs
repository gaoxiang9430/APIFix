using Xunit;
using System;
using Microsoft.ProgramSynthesis.Transformation.Tree;
using static Microsoft.ProgramSynthesis.Transformation.Tree.Utils.Utils;
using Microsoft.ProgramSynthesis.Wrangling.Constraints;
using Microsoft.ProgramSynthesis.Wrangling.Tree;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.ProgramSynthesis.Extraction.Web.Build.NodeTypes;
using Xunit.Abstractions;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace CSharpEngine.Tests
{
    public class Test
    {
        private string[] test1 = {"../../../../../test/test1/v1.cs",
                                  "../../../../../test/test1/v2.cs"};
        private string[] test2 = {"../../../../../test/test2/v1.cs",
                                  "../../../../../test/test2/v2.cs"};
        private string[] test3 = {"../../../../../test/test3/v1.cs",
                                  "../../../../../test/test3/v2.cs"};

        private string[] test4 = {"../../../../../test/test4/v1.cs",
                                  "../../../../../test/test4/v2.cs"};
        private string[] test5 = {"../../../../../test/test5/v1.cs",
                                  "../../../../../test/test5/v2.cs"};
              
        private string[] test6 = {"../../../../../test/test6/v1.cs",
                                  "../../../../../test/test6/v2.cs"};

        private string[] test7 = {"../../../../../test/test7/v1.cs",
                                  "../../../../../test/test7/v2.cs"};
        private string[] test8 = {"../../../../../test/test8/v1.cs",
                                  "../../../../../test/test8/v2.cs",
                                  "../../../../../test/test8/v3.cs"};
        private string[] test9 = {"../../../../../test/test9/v1.cs",
                                  "../../../../../test/test9/v2.cs"};

        [Fact]
        public void TestExtractClass(){
            var cls = ClassExtractor.ExtractClassesFromFile(test7[0]);
            Assert.Equal(2, cls.Count);
        }

        [Fact]
        public void TestExtractMatchedClassPairs(){
            var cls1 = ClassExtractor.ExtractClassesFromFile(test1[0]);
            var cls2 = ClassExtractor.ExtractClassesFromFile(test1[1]);

            Assert.Single(cls1);
            Assert.Single(cls2);

            var matchedClasses = ClassExtractor.CalculateMatchedClass(cls1, cls2);
            foreach (var matchedClass in matchedClasses){
                Utils.LogTest(matchedClass.ToString());
            }
        }

        [Fact]
        public void TestExtractMethods(){
            var exc = new ClassExtractor();
            var matchClass = buildMatchedClass(test1);

            var methods = matchClass.extractMethods(matchClass.class1.getSyntax());
            /*foreach (var method in methods){
                Utils.LogTest("ExtractMethod: " + method.ToString());
            }*/
            Assert.Equal(5, methods.Count);
        }

        [Fact]
        public void TestExtractParameters()
        {
            var exc = new ClassExtractor();
            var matchClass = buildMatchedClass(test9);

            var methods = matchClass.extractMethods(matchClass.class1.getSyntax());
            foreach (var method in methods){
                Utils.LogTest("ExtractMethod: " + method.ToString());
                if (method.GetSyntax().ParameterList != null)
                {
                    int count = 0;
                    foreach (var para in method.GetSyntax().ParameterList.Parameters)
                    {
                        if (!para.ToString().Contains("this "))
                            count ++;
                    }
                    Assert.Equal(count, method.GetSyntax().ParameterList.Parameters.Count - 1);
                }
            }
            
        }
        
        [Fact]
        public void TestGetMatchededMethod(){
            var matchClass = buildMatchedClass(test1);;
            var matchedMethods = matchClass.GetMatchedMethods();

            Assert.Equal(5, matchedMethods.Count);
        }

        [Fact]
        public void TestGetModifiedMethod(){
            var matchClass = buildMatchedClass(test4);;
            var modifiedMethods = matchClass.GetMatchedMethods();
                Utils.LogTest("size = " + modifiedMethods.Count);
            foreach(var mm in modifiedMethods){
                Utils.LogTest(mm.ToString());
            }
        }

        [Fact]
        public void TestGetModifiedField()
        {
            var matchClass = buildMatchedClass(test1);
            var modifiedFields = matchClass.GetMatchedFields();
            Assert.Single(modifiedFields);

            Assert.Equal("public  string i ==> public  int i", modifiedFields[0].ToString());
        }

        
        [Fact]
        public void TestAttribute(){
            var matchClass = buildMatchedClass(test1);
            var matchedMethods = matchClass.GetMatchedMethods();
            foreach (var method in matchedMethods){
                Utils.LogTest("TestAttribute --- Matched method: " + method.ToString());
            }
        }

        [Fact]
        public void TestContains(){
            var matchClass = buildMatchedClass(test1);
            var matchedMethods = matchClass.GetMatchedMethods();
            var method = matchedMethods[0];
            Assert.True(MatchingPolice.Contains(method.method1.GetSyntax(), "AsyncPreExecutePolicy"));
        }

        private MatchedClass buildMatchedClass(string[] test){
            var cls1 = ClassExtractor.ExtractClassesFromFile(test[0]);
            var cls2 = ClassExtractor.ExtractClassesFromFile(test[1]);

            var matchClass = new MatchedClass(cls1[0], cls2[0]);

            return matchClass;
        }

        [Fact]
        public void TestSynthesis(){
            Attributes.SetKnownSoftAttributes(new string[] {});

            string content = File.ReadAllText(test8[0]);
            var node1 = CSharpSyntaxTree.ParseText(content).GetRoot();
            var inputNode = Translator.Translate(node1);
            Utils.LogTest("Hash code of input node is: " + inputNode.GetHashCode());

            content = File.ReadAllText(test8[1]);            
            var node2 = CSharpSyntaxTree.ParseText(content).GetRoot();
            var outputNode = Translator.Translate(node2);

            var l1 = new List<SyntaxNodeOrToken>();
            l1.Add(node1);
            var l2 = new List<SyntaxNodeOrToken>();
            l2.Add(node2);
            MatchingPolice.TokenSame(l1, l2);

            Utils.LogTest(inputNode.GenerateCode());
            Utils.LogTest(outputNode.GenerateCode());

            var example = Translator.CreateExample(inputNode, outputNode);
            Example<Node, Node>[] examples = new[] { example };

            Program program = Translator.Learn(examples);
            Assert.NotNull(program);

            Node output = program.Run(inputNode);
            if(output != null)
                Utils.LogTest(output.GenerateCode());
        }

        private readonly ITestOutputHelper output;

        public Test(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestSemanticModel() {
            var rtc = RTCompilation.Init();
            rtc.CompileSolution(@"..\..\..\..\..\benchmark\Polly\library\Polly-6.1.2\src\Polly.sln", "old");
            rtc.CompileSolution(@"..\..\..\..\..\benchmark\Polly\library\Polly-7.0.0\src\Polly.sln", "new");
            var classes = ClassExtractor.ExtractClasses(rtc.newSyntexNodes.Select(e => new Record<SyntaxNode, string>(e.GetCompilationUnitRoot(), e.FilePath)).ToList());
            Assert.NotNull(rtc.oldCompilations);
            Assert.NotEmpty(classes);
        }
    }
}
