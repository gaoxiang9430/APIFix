using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CSharpEngine {
    public class MatchedClass{
        public Class class1 = null;
        public Class class2 = null;
        public ChangeType changeType = ChangeType.None;
        public List<MatchedMethod> modifiedMethods = null;
        public List<MatchedField> modifiedFields = null;

        private List<MatchedMethod> matchedMethods = null;
        private List<MatchedField> matchedFields = null;
        private bool isInsertOrDelete;
        // private bool isModifiedClassID = true;
        private bool isUnModifiedClass = false;

        public MatchedClass(){}
        public MatchedClass(Class class1, Class class2) {
            this.class1 = class1;
            this.class2 = class2;

            isInsertOrDelete = (class1 == null || class2 == null);
            changeType = getChangeType();
            if(!isInsertOrDelete)
                isUnModifiedClass = class1.ContentEquals(class2);

            matchedMethods = _getMatchedMethods();
            matchedFields = _getMatchedFields();
            if (matchedMethods.Any())
                modifiedMethods = matchedMethods.Where(e => !e.IsUnmodifiedMethodSignature()).ToList();
            if(matchedFields.Any())
                modifiedFields = matchedFields.Where(e => !e.IsUnmodifiedField()).ToList();
        }

        private ChangeType getChangeType(){
            if (class1 == null)
                return ChangeType.Insert;
            else if (class2 == null)
                return ChangeType.Delete;
            else if (class1.className != class2.className)
                return ChangeType.Rename;
            else if (class1.nameSpace != class2.nameSpace)
                return ChangeType.ChangeType;
            else if (class1.modifier != class2.modifier)
                return ChangeType.ChangeVisibility;
            return ChangeType.None;
        }

        private List<MatchedMethod> _getMatchedMethods(){            
            var matchedMethods = new List<MatchedMethod>();
            //if (isInsertOrDelete || isUnModifiedClass) return matchedMethods;
            if (isUnModifiedClass) return matchedMethods;
            List<Method> methods1 = new List<Method>(), methods2 = new List<Method>();
            if (class1 != null)
                methods1 = extractMethods(class1.getSyntax());
            if (class2 != null)
                methods2 = extractMethods(class2.getSyntax());

            var filteredMethods1 = new List<Method>();
            var filteredMethods2 = new List<Method>(methods2);

            foreach (var method1 in methods1){
                if(methods2.Contains(method1)){
                    matchedMethods.Add(new MatchedMethod(method1, filteredMethods2.FirstOrDefault(e => e.Equals(method1))));
                    filteredMethods2.Remove(method1);
                } else
                    filteredMethods1.Add(method1);
            }

            var disMatrix = new double[filteredMethods1.Count, filteredMethods2.Count];
            for (int i = 0; i < filteredMethods1.Count; i++){
                var method1 = filteredMethods1[i];
                // bool findMatch = false;
                for (int j = 0; j < filteredMethods2.Count; j++){
                    var method2 = filteredMethods2[j];
                    var dis = method1.Distance(method2);
                    if (dis < Config.DisThreshold){
                        disMatrix[i, j] = dis;
                    } else
                        disMatrix[i, j] = Config.DisThreshold;    
                }
            }

            var rMinIndex = new int[filteredMethods1.Count];
            var cMinIndex = new int[filteredMethods2.Count];

            for (int i = 0; i < filteredMethods1.Count; i++){
                if(filteredMethods2.Count == 0){
                    rMinIndex[i] = -1;
                    continue;
                }
                double[] rarray = GetRData(disMatrix, i, filteredMethods2.Count);
                double min = rarray.Min();
                if (min >= Config.DisThreshold)
                    rMinIndex[i] = -1;
                else
                    rMinIndex[i] = Array.IndexOf(rarray, min);
            }

            for (int j = 0; j < filteredMethods2.Count; j++){
                if(filteredMethods1.Count == 0){
                    cMinIndex[j] = -1;
                    continue;
                }
                double[] carray = GetCData(disMatrix, j, filteredMethods1.Count);
                double min = carray.Min();
                if (min >= Config.DisThreshold)
                    cMinIndex[j] = -1;
                else
                    cMinIndex[j] = Array.IndexOf(carray, min);
            } 

            for (int i = 0; i < filteredMethods1.Count; i++){
                if(rMinIndex[i] == -1)
                    matchedMethods.Add(new MatchedMethod(filteredMethods1[i], null));
                else{
                    var indexJ = rMinIndex[i];
                    if (cMinIndex[indexJ] == i){
                        matchedMethods.Add(new MatchedMethod(filteredMethods1[i], filteredMethods2[indexJ]));
                        cMinIndex[indexJ] = -2;
                    }
                    else
                        matchedMethods.Add(new MatchedMethod(filteredMethods1[i], null));
                }
            }

            for (int j = 0; j < filteredMethods2.Count; j++){
                if(cMinIndex[j] != -2)
                    matchedMethods.Add(new MatchedMethod(null, filteredMethods2[j]));
            }

            return matchedMethods;
        }

        private double[] GetRData(double[,] matrix, int row, int length){
            double[] ret = new double[length];
            for(int i = 0; i < length; i++)
                ret[i] = matrix[row, i];
            return ret;
        }

        private double[] GetCData(double[,] matrix, int col, int length){
            double[] ret = new double[length];
            for(int i = 0; i < length; i++)
                ret[i] = matrix[i, col];
            return ret;
        }

        private List<MatchedField> _getMatchedFields(){
            var matchedFields = new List<MatchedField>();
            if (isInsertOrDelete || isUnModifiedClass) return matchedFields;
            
            var fieldsOfClass1 = extractFields(class1.getSyntax());
            var fieldsOfClass2 = extractFields(class2.getSyntax());

            var filteredField1 = new List<Field>();
            var filteredField2 = new List<Field>(fieldsOfClass2);

            foreach (var field1 in fieldsOfClass1){
                if(filteredField2.Contains(field1))
                    filteredField2.Remove(field1);
                else
                    filteredField1.Add(field1);
            }

            foreach (var field1 in filteredField1){
                bool findMatch = false;
                foreach (var field2 in filteredField2){
                    if(field1.Distance(field2) < 0.4){
                        // regard they are same field if the relative distance between variable names is less threshold 0.3
                        matchedFields.Add(new MatchedField(field1, field2));
                        filteredField2.Remove(field2);
                        findMatch = true;
                        break;
                    }
                }
                if (!findMatch)
                    matchedFields.Add(new MatchedField(field1, null));
            }
            foreach (var field2 in filteredField2)
                matchedFields.Add(new MatchedField(null, field2));

            return matchedFields;
        }

       public List<Method> extractMethods(TypeDeclarationSyntax cls){
            var methodList = new List<Method>();
            
            foreach (var constructor in cls.ChildNodes().OfType<ConstructorDeclarationSyntax>()){
                string modifier = "";
                foreach (var mdf in constructor.Modifiers)
                    modifier += mdf.ToString() + " ";

                // if (modifier.Contains("private") || modifier.Contains("internal"))
                //    continue;
                var paraList = new List<Record<string, bool>>();
                if(constructor.ParameterList != null){
                    foreach (var para in constructor.ParameterList.Parameters){
                        if(para.Default != null)
                            paraList.Add(new Record<string, bool>(para.Type.ToString(), true));
                        else
                            paraList.Add(new Record<string, bool>(para.Type.ToString(), false));
                    }
                }

                methodList.Add(new Method(modifier, "", constructor.Identifier.ToString(), "", paraList, constructor));
            }

            foreach (var method in cls.ChildNodes().OfType<MethodDeclarationSyntax>()) {
                string methodName = method.Identifier.ToString();
                string modifier = "", thisName = null;
                foreach (var mdf in method.Modifiers)
                    modifier += mdf.ToString() + " ";

                // if (modifier.Contains("private") || modifier.Contains("internal"))
                //     continue;
                string returnType = method.ReturnType.ToString();
                var paraList = new List<Record<string, bool>>();
                if(method.ParameterList != null){
                    foreach (var para in method.ParameterList.Parameters) {
                        if (para.ToFullString().Contains("this ")){              // ignore the "this" parameter
                            thisName = para.Type.ToString();
                            paraList.Add(new Record<string, bool>(para.Type.ToString(), true));
                        }
                        else if(para.Default != null)
                            paraList.Add(new Record<string, bool>(para.Type.ToString(), true));
                        else
                            paraList.Add(new Record<string, bool>(para.Type.ToString(), false));
                    }
                }

                string typeParameterList = "";
                if(method.TypeParameterList != null)
                    typeParameterList = method.TypeParameterList.ToString();

                methodList.Add(new Method(modifier, returnType, methodName, typeParameterList, paraList, method, thisName));
            }

            return methodList;
        }

        private List<Field> extractFields(TypeDeclarationSyntax cls){
            var fieldsOfClass = new List<Field>();
            foreach (var child in cls.ChildNodes().OfType<FieldDeclarationSyntax>()) {
                string modifiers = "";
                foreach (var modifier in child.Modifiers)
                    modifiers += modifier.ToString() + " ";
                // ignore private or internal fields
                if (modifiers.Contains("private") || modifiers.Contains("internal"))
                    continue;
                string type = child.Declaration.Type.ToString();
                foreach (var attribute in child.Declaration.Variables){
                    fieldsOfClass.Add(new Field(modifiers, type, attribute.Identifier.ToString(), child));
                }
            }
            return fieldsOfClass;
        }

        // public bool IsUnmodifiedClass() => isUnModifiedClass;

        public List<MatchedMethod> GetMatchedMethods(){
            return matchedMethods;
        }

        public List<MatchedField> GetMatchedFields(){
            return matchedFields;
        }

        public bool IsInsertOrDelete() => isInsertOrDelete;

        public bool ModifiedClassSignature() {
            if ((class1 == null || class1.IsPrivate()) && (class2 == null || class2.IsPrivate()))
                return false;
            return (changeType != ChangeType.None && changeType != ChangeType.ChangeType) ||
                   (modifiedFields != null && modifiedFields.Count != 0) ||
                   (modifiedMethods != null && modifiedMethods.Count != 0);
        }

        public override string ToString(){
            string ret = "";
            if (class1==null)
                ret += "null";
            else
                ret += class1.ToString();

            ret += " ==> ";

            if (class2==null)
                ret += "null";
            else
                ret += class2.ToString();
            return ret;
        }
    }

    public class Class{
        private string filePath;
        public string nameSpace;
        public string className;
        public string typeParameterList;
        public string fileName;
        public string modifier;
        private TypeDeclarationSyntax declarationSyntax;

        public Class(string filePath, string nameSpace, string modifier,
                    string className, string typeParameterList, TypeDeclarationSyntax declarationSyntax){
            this.nameSpace = nameSpace;
            this.className = className;
            this.typeParameterList = typeParameterList;
            this.modifier = modifier;
            this.filePath = filePath;
            this.declarationSyntax = declarationSyntax;
            fileName = Path.GetFileName(filePath);
        }

        public override bool Equals(object otherObj){
            var other = otherObj as Class;
            if(other == null) return false;
            var sigEqual = // fileName == other.fileName &&
                   nameSpace == other.nameSpace &&
                   modifier == other.modifier &&
                   className == other.className &&
                   typeParameterList == other.typeParameterList;
            if(modifier.Contains("partial"))
                return sigEqual && fileName == other.fileName;
            return sigEqual;
        }        
        
        public override int GetHashCode(){
            if(modifier.Contains("partial"))
                return (fileName+modifier+nameSpace+className+typeParameterList).GetHashCode();
            return (modifier+nameSpace+className+typeParameterList).GetHashCode();
        }

        public bool EqualSignature(Class other){
            return className == other.className &&
                   typeParameterList == other.typeParameterList &&
                   modifier == other.modifier;
        }

        public string GetSignature(){
            return nameSpace + "." + className; // + typeParameterList;
        }

        public TypeDeclarationSyntax getSyntax(){ 
            return declarationSyntax;
        }

        public bool IsPrivate() {
            return modifier.Contains("private") || modifier.Contains("internal");
        }

        public bool ContentEquals(Class other){
            if(other == null) return false;
            return declarationSyntax.ToString() == other.getSyntax().ToString();
        }

        public double Distance(Class other, string matchedFiles) {
            if (!Utils.IsMatchedFile(filePath, other.filePath, matchedFiles))
                return 1;

            var sig = nameSpace + className + typeParameterList;
            var otherSig = other.nameSpace + other.className + other.typeParameterList;
            if (sig.Equals(otherSig))
                return 0;

            return Utils.CloneDetectionDis(declarationSyntax, other.declarationSyntax);
        }

        public string GetFilePath() => filePath;

        /*public double Distance(Class other){
            double ret; 
            var sig = modifier + nameSpace + className + typeParameterList;
            var otherSig = other.modifier + other.nameSpace + other.className + other.typeParameterList;
            if(modifier.Contains("partial")){
                sig = fileName + sig;
                otherSig = other.fileName + otherSig;
            }
            ret = Utils.ContentDistance(sig, otherSig);
            if(ret >= Config.DisThreshold){
                var bodyDis = Utils.ContentDistance(declarationSyntax.ToString(), other.declarationSyntax.ToString());
                if (bodyDis < ret)
                    ret = bodyDis;
            }
            return ret;
        }*/

        public override string ToString(){
            return fileName + ":" + nameSpace + " " + className + typeParameterList;
        }
    }

}
