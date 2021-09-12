using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System;

namespace CSharpEngine {
 
    public class MatchedMethod {

        public Method method1 = null;
        public Method method2 = null;
        public ChangeType changeType = ChangeType.None;        

        public MatchedMethod(Method method1, Method method2){
            this.method1 = method1;
            this.method2 = method2;
            changeType = getChangeType();
        }

        private ChangeType getChangeType(){
            if (method1 == null)
                return ChangeType.Insert;
            else if (method2 == null)
                return ChangeType.Delete;
            else if (method1.methodName != method2.methodName)
                return ChangeType.Rename;
            else if (method1.returnType != method2.returnType)
                return ChangeType.ChangeType;
            else if (method1.modifier != method2.modifier)
                return ChangeType.ChangeVisibility;
            else if (method1.argList.Count != method2.argList.Count)
                return ChangeType.ChangeArg;
            for (int i=0; i<method1.argList.Count; i++){
                if(!method1.argList[i].Item1.Equals(method2.argList[i].Item1))
                    return ChangeType.ChangeArg;
            }
            return ChangeType.None;
        }

        public bool IsUnmodifiedMethodSignature() {
            // ignore test methods
            if((method1 != null && method1.IsTest()) || (method2 != null && method2.IsTest()))
                return true;
            if ((method1 == null || method1.IsPrivate()) && (method2 == null || method2.IsPrivate()))
                return true;
            if (method1 == null || method2 == null)
                return false;

            return method1.Equals(method2);
        }

        public bool IsUnmodifiedMethod() {
            if (method1 == null || method2 == null)
                return false; 

            return method1.ContentEquals(method2);
        }

        public override string ToString(){
            string ret = "";
            if (method1==null)
                ret += "null";
            else
                ret += method1.ToString();

            ret += " ==> ";

            if (method2==null)
                ret += "null";
            else
                ret += method2.ToString();
            return ret;
        }
    }

    public class Method {
        public string modifier;
        public string returnType;
        public string methodName;
        public string typeParameterList;
        public List<Record<string, bool>> argList;
        private string thisName;
        private BaseMethodDeclarationSyntax declarationSyntax;
        // private SemanticModel semanticModel = null;

        public Method(string modifier, string returnType, string methodName, string typeParameterList,
                List<Record<string, bool>> argList, BaseMethodDeclarationSyntax declarationSyntax, string thisName = null){
            this.modifier = modifier;
            this.methodName = methodName;
            this.typeParameterList = typeParameterList;
            this.returnType = returnType;
            this.argList = argList;
            this.declarationSyntax = declarationSyntax;
            this.thisName = thisName;
        }

        public BaseMethodDeclarationSyntax GetSyntax(){
            return declarationSyntax;
        }

        public override bool Equals(object otherObj){
            var other = otherObj as Method;
            if(other == null) return false;
            if (methodName != other.methodName || typeParameterList != other.typeParameterList || returnType != other.returnType)
                return false;

            // ignore the difference of unnecessary arguments
            var compulsoryArgs1 = argList.Where(e => !e.Item2).ToList();
            var compulsoryArgs2 = other.argList.Where(e => !e.Item2).ToList();

            if (compulsoryArgs1.Count != compulsoryArgs2.Count)
                return false;
            for (int i=0; i< compulsoryArgs1.Count; i++){
                if(!compulsoryArgs1[i].Item1.Equals(compulsoryArgs2[i].Item1))
                    return false;
            }
            return true;
        }

        public bool ContentEquals(Method other){
            if(other == null) return false;
            if(declarationSyntax.Body == null || other.GetSyntax().Body == null) return false;
            return declarationSyntax.Body.ToString() == other.GetSyntax().Body.ToString();
        }

        public override int GetHashCode(){
            int ret = (methodName+typeParameterList+modifier+returnType).GetHashCode();
            for (int i=0; i<argList.Count; i++)
                ret += argList[i].Item1.GetHashCode();
            return ret;
        }

        /*public double Distance(Method other) { // implement clone detection algorithm
            if (Signature().Equals(other.Signature()))
                return 0;

            return Utils.CloneDetectionDis(declarationSyntax.Body, other.GetSyntax().Body);
        }*/

        public double Distance(Method other){ // implement clone detection algorithm
            double ret = Utils.ContentDistance(Signature(), other.Signature());
            if(ret >= Config.DisThreshold && declarationSyntax.Body != null && other.GetSyntax().Body != null){
                var bodyDis = Utils.ContentDistance(declarationSyntax.Body.ToString(), other.GetSyntax().Body.ToString());
                if (bodyDis < ret)
                    ret = bodyDis;
            }
            return ret;
        }

        public override string ToString(){
            string ret = modifier + " " + returnType + " " + methodName + typeParameterList + "(";
            foreach (var arg in argList)
                ret += arg.Item1 + ", ";
            ret += ")";
            return ret;
        }
        
        public bool IsTest(){
            foreach (var att in declarationSyntax.AttributeLists){
                if(att.ToString() == "[Fact]" || att.ToString().Contains("Theory")){
                    return true;
                }
            }
            return false;
        }

        public bool IsPrivate()
        {
            return modifier.Contains("private") || modifier.Contains("internal");
        }

        public string GetThisName() => thisName; 

        public string Signature(){
            string ret = returnType + " " + methodName + typeParameterList +"(";
            foreach (var arg in argList)
                ret += " " + arg.Item1;
            ret += ")";
            return ret;
        }
    }   
}
