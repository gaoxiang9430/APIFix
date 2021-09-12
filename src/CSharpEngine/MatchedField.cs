using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;

namespace CSharpEngine {
    public class MatchedField{

        public Field field1 = null, field2 = null;
        public ChangeType changeType = ChangeType.None;
        public MatchedField(Field field1, Field field2){
            this.field1 = field1;
            this.field2 = field2;
            changeType = getChangeType();
        }

        private ChangeType getChangeType(){
            if (field1 == null)
                return ChangeType.Insert;
            else if (field2 == null)
                return ChangeType.Delete;
            else if (field1.identifier != field2.identifier)
                return ChangeType.Rename;
            else if (field1.type != field2.type)
                return ChangeType.ChangeType;
            else if (field1.modifier != field2.modifier)
                return ChangeType.ChangeVisibility;
            return ChangeType.None;
        }

        public bool IsUnmodifiedFieldSignature() => field1 != null && field2 != null && field1.Equals(field2);

        public bool IsUnmodifiedField() => field1 != null && field2 != null && field1.ContentEqual(field2);

        public override string ToString(){
            string ret = "";
            if (field1 == null)
                ret += "null";
            else
                ret += field1.ToString();

            ret += " ==> ";

            if (field2 == null)
                ret += "null";
            else
                ret += field2.ToString();
            return ret;
        }
    }

    public class Field{
        public string modifier;
        public string type;
        public string identifier;
        private FieldDeclarationSyntax declarationSyntax;

        public Field (string modifier, string type, string identifier, 
                    FieldDeclarationSyntax declarationSyntax){
            this.modifier = modifier;
            this.type = type;
            this.identifier = identifier;
            this.declarationSyntax = declarationSyntax;
        }

        public override bool Equals(object otherObj){
            var other = otherObj as Field;
            if(other == null) return false;
            return modifier == other.modifier && type == other.type && 
                    identifier == other.identifier;
        }

        public bool ContentEqual(Field other){
            if(other == null) return false;
            return declarationSyntax.ToString() == other.GetSyntax().ToString();
        }

        public override int GetHashCode(){
            return (type + modifier + identifier).GetHashCode();
        }

        public FieldDeclarationSyntax GetSyntax(){
            return declarationSyntax;
        }

        public double Distance(Field other){
            return Utils.ContentDistance(identifier, other.identifier);
        }

        public override string ToString(){
            return modifier + " " + type + " " + identifier;
        }
    }
}
