using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpEngine
{
    class InvocationNodeType{
        public static InvokeType GenerateType(SyntaxNode node, string version){
            string nodeType = node.GetType().ToString();
            if (!nodeType.Equals("Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax") && 
                !nodeType.Equals("Microsoft.CodeAnalysis.CSharp.SyntaxObjectCreationExpressionSyntax"))
                return null;
                
            ISymbol invokedSymbol = RTCompilation.GetRTCompilation().GetSemanticSymbol(node, version);
            if(invokedSymbol != null){
                var containingType = invokedSymbol.ContainingType;
                var containingSymbolName = GetBaseTypeAndInterfaces(containingType);
                var symbolMethodName = invokedSymbol.Name.Replace("..ctor", "");

                List<string> argTypes = new List<string>();
                SeparatedSyntaxList<ArgumentSyntax> args;
                if (nodeType.Equals("Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax"))
                    args = (node as InvocationExpressionSyntax).ArgumentList.Arguments;
                else
                    args = (node as ObjectCreationExpressionSyntax).ArgumentList.Arguments;

                foreach(var arg in args){
                    var argType = RTCompilation.GetRTCompilation().GetSemanticType(arg.ChildNodes().FirstOrDefault(), version);
                    if(argType != null){
                        argTypes.Add(GetBaseTypeAndInterfaces(argType));
                    }
                }
                return new InvokeType(containingSymbolName, symbolMethodName, argTypes);
            }
            return null;
        }

        private static String GetBaseTypeAndInterfaces(ITypeSymbol containingType) {
            if (containingType == null)
                return "";

            var baseTypeAndInterfaces = RemoveGeneticPara(containingType.ToString());

            var baseType = containingType.BaseType;
            if (baseType != null) { 
                if (!baseType.ToString().Equals("Object") && !baseType.ToString().Equals("object"))
                    baseTypeAndInterfaces += ":" + GetBaseTypeAndInterfaces(baseType);
            }
        
            var interfaces = containingType.Interfaces;
            if(interfaces != null && interfaces.Count() != 0) { 
                foreach (var inter in interfaces) { 
                    baseTypeAndInterfaces += ":" + GetBaseTypeAndInterfaces(inter);
                }
            }
            return baseTypeAndInterfaces;
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

    public class InvokeType{
        public string className;
        public string methodName;
        public List<string> argTypes;

        public InvokeType(string className, string methodName, List<string> argTypes){
            this.className = className;
            this.methodName = methodName;
            this.argTypes = argTypes;
        }
    }
}