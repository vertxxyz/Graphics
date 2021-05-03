using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;


// TODO: should be able to represent functions that use return values (not just output parameters)
// maybe just the output parameter labeled "return"?

[Serializable]
public class ShaderFunction : ShaderFunctionSignature
{
    public override int latestVersion => 1;

    // public API
    public IEnumerable<ShaderFunctionSignature> FunctionsCalled => functionsCalled?.SelectValue();
    public IEnumerable<string> IncludePaths => includePaths.AsReadOnly();
    public string Body { get { return body; } }
    public virtual bool isGeneric => false;

    // serialized state
    [SerializeField]
    List<string> includePaths;

    [SerializeField]
    List<JsonData<ShaderFunctionSignature>> functionsCalled;

    [SerializeField]
    string body;

    // constructor is internal, public must use the Builder class instead
    internal ShaderFunction(string name, List<Parameter> parameters, string body, List<JsonData<ShaderFunctionSignature>> functionsCalled, List<string> includePaths) : base(name, parameters)
    {
        this.body = body;                           // only null if externally implemented
        this.functionsCalled = functionsCalled;     // can be null or empty
        this.includePaths = includePaths;           // can be null or empty
    }

    // "new" here means hide the inherited ShaderFunctionSignature.Builder, and replace it with this declaration
    public new class Builder : ShaderFunctionSignature.Builder
    {
        ShaderBuilder body;
        List<JsonData<ShaderFunctionSignature>> functionsCalled;
        List<string> includePaths;
        List<SandboxValueType> genericTypeParameters;
        List<JsonData<ShaderFunctionSignature>> genericFunctionParameters;

        // builder-only state
        int currentIndent;
        int tabSize;

        public Builder(string name) : base(name)
        {
            this.body = new ShaderBuilder();
            this.currentIndent = 4;
            this.tabSize = 4;
        }

        public SandboxValueType AddGenericTypeParameter(string name)
        {
            // create a local placeholder type with the given name
            var type = new SandboxValueType(name, SandboxValueType.Flags.Placeholder);
            return AddGenericTypeParameter(type);
        }

        public SandboxValueType AddGenericTypeParameter(SandboxValueType placeholderType)
        {
            if (placeholderType == null)
                return null;

            if (!placeholderType.IsPlaceholder)
                return null;        // TODO: error?  can only use placeholder types as generic type parameters

            if (genericTypeParameters == null)
                genericTypeParameters = new List<SandboxValueType>();
            else
            {
                // find any existing parameter with the same name
                var existing = genericTypeParameters.Find(x => x.Name == placeholderType.Name);
                if (existing != null)
                {
                    // if found, it must be exactly the same
                    if (existing.ValueEquals(placeholderType))
                        return existing;
                    else
                    {
                        Debug.LogError("Generic Type Parameter Name Collision: " + placeholderType.Name);
                        return null;
                    }
                }
            }

            genericTypeParameters.Add(placeholderType);

            return placeholderType;
        }

/*
        public ShaderFunctionSignature AddGenericFunctionParameter(ShaderFunctionSignature placeholderFunctionSignature)
        {
            if (placeholderFunctionSignature == null)
                return null;

            if (!placeholderFunctionSignature.isFunctionSignatureOnly)
                return null;

            if (genericFunctionParameters == null)
                genericFunctionParameters = new List<JsonData<ShaderFunctionSignature>>();
            else
            {
                // find any existing parameter with the same name
                var existing = genericFunctionParameters.Find(x => x == placeholderFunctionSignature);
                if (existing != null)
                {
                    if (existing.ValueEquals(placeholderFunctionSignature))
                        return existing;
                    else
                    {
                        Debug.LogError("Generic Function Parameter Name Collision: " + placeholderType.Name);
                        return null;
                    }
                }
            }

            genericFunctionParameters.Add(placeholderFunctionSignature);

            return placeholderFunctionSignature;
        }
*/

        public void ImplementedInIncludeFile(string filePath)
        {
            // TODO
            // declares that the function is implemented in an external include file
            // this flags the function name as external, non-renamable, and
            // automatically adds the include file as a dependency of the function
            AddIncludeFileDependency(filePath);
        }

        public void AddIncludeFileDependency(string filePath)
        {
            if (includePaths == null)
                includePaths = new List<string>();
            includePaths.Add(filePath);
        }

        public void CallFunction(ShaderFunctionSignature func, params string[] arguments)
        {
            if (arguments.Length == func.Parameters.Count)
            {
                using (var args = CallFunction(func))
                {
                    foreach (var p in arguments)
                        args.Add(p);
                }
            }
            else
            {
                Debug.Log("CallFunction: " + func.Name + " argument count mismatch, " + arguments.Length + " arguments provided, but function takes " + func.Parameters.Count + " parameters.");
            }
        }

        public Arguments CallFunction(ShaderFunctionSignature func)
        {
            if (functionsCalled == null)
                functionsCalled = new List<JsonData<ShaderFunctionSignature>>();

            if (!functionsCalled.Contains(func))
                functionsCalled.Add(func);

            // Do we want to insert a special string marker to body,
            // so we can translate the function name at generation time?
            // Maybe name is enough as long as it is a locally unique identifier.
            Indentation();
            body.Add(func.Name);
            body.Add("(");

            return new Arguments(body, func.Parameters.Count);
        }

        public ref struct Arguments
        {
            internal ShaderBuilder body;
            bool first;
            int paramCount;
            int argCount;

            internal Arguments(ShaderBuilder body, int paramCount)
            {
                this.body = body;
                this.first = true;
                this.argCount = 0;
                this.paramCount = paramCount;
            }

            public Arguments Add(string arg)
            {
                if (argCount <= paramCount)
                {
                    if (!first)
                        body.Add(", ");

                    body.Add(arg);
                    first = false;
                }
                else
                {
                    Debug.Log("CallFunction: too many arguments");
                }
                argCount++;
                return this;
            }

            public void Dispose()
            {
                if (argCount != paramCount)
                {
                    // ERROR: mismatched argument count
                    Debug.Log("CallFunction: mismatched argument count");
                }
                body.AddLine(");");
                body = null;
            }
        }

        public void NewLine()
        {
            body.NewLine();
        }

        // could do generic implementation
        public void AddLine(string l0)
        {
            Indentation();
            body.Add(l0);
            body.NewLine();
        }

        public void AddLine(string l0, string l1)
        {
            Indentation();
            body.Add(l0);
            body.Add(l1);
            body.NewLine();
        }

        public void AddLine(string l0, string l1, string l2)
        {
            Indentation();
            body.Add(l0);
            body.Add(l1);
            body.Add(l2);
            body.NewLine();
        }

        public void AddLine(string l0, string l1, string l2, string l3)
        {
            Indentation();
            body.Add(l0);
            body.Add(l1);
            body.Add(l2);
            body.Add(l3);
            body.NewLine();
        }

        public void AddLine(string l0, string l1, string l2, string l3, string l4)
        {
            Indentation();
            body.Add(l0);
            body.Add(l1);
            body.Add(l2);
            body.Add(l3);
            body.Add(l4);
            body.NewLine();
        }

        public void AddLine(string l0, string l1, string l2, string l3, string l4, string l5)
        {
            Indentation();
            body.Add(l0);
            body.Add(l1);
            body.Add(l2);
            body.Add(l3);
            body.Add(l4);
            body.Add(l5);
            body.NewLine();
        }

        public Line LineScope()
        {
            Indentation();
            return new Line(body);
        }

        public Block BlockScope()
        {
            AddLine("{");
            Indent();
            return new Block(this);
        }

        public void Indent()
        {
            currentIndent += tabSize;
        }

        public void Deindent()
        {
            currentIndent -= tabSize;
        }

        public void Indentation()
        {
            // TODO: need a better way
            // for (int i = 0; i < currentIndent; i++)
            //    body.Add(" ");
        }

        public readonly ref struct Line
        {
            readonly ShaderBuilder body;

            // TODO: ideally we have a write-only ShaderBuilder here, without the ability to ConvertToString
            public ShaderBuilder sb => body;

            public Line(ShaderBuilder body)
            {
                this.body = body;
            }

            public void Add(string s0)
            {
                body.Add(s0);
            }

            public void Add(string s0, string s1)
            {
                body.Add(s0);
                body.Add(s1);
            }

            public void Add(string s0, string s1, string s2)
            {
                body.Add(s0);
                body.Add(s1);
                body.Add(s2);
            }

            public void Add(string s0, string s1, string s2, string s3)
            {
                body.Add(s0);
                body.Add(s1);
                body.Add(s2);
                body.Add(s3);
            }

            public void Add(string s0, string s1, string s2, string s3, string s4)
            {
                body.Add(s0);
                body.Add(s1);
                body.Add(s2);
                body.Add(s3);
                body.Add(s4);
            }

            public void Dispose()
            {
                // Debug.Log("Line Dispose");
                body.NewLine();
            }
        }

        public readonly ref struct Block
        {
            readonly ShaderFunction.Builder parent;

            public Block(ShaderFunction.Builder parent)
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                Debug.Log("Block.Dispose");
                parent.Deindent();
                parent.AddLine("}");
            }
        }

        public new ShaderFunction Build()
        {
            ShaderFunction func = null;
            if ((genericTypeParameters != null) && (genericTypeParameters.Count > 0))
            {
                Debug.LogError("Call ShaderFunction.Builder.BuildGeneric() for generic functions");
            }
            else
            {
                func = new ShaderFunction(name, parameters, body.ConvertToString(), functionsCalled, includePaths);
            }

            // clear data so we can't accidentally re-use it
            this.name = null;
            this.parameters = null;
            this.body = null;
            this.functionsCalled = null;
            this.includePaths = null;
            this.genericTypeParameters = null;

            return func;
        }

        public GenericShaderFunction BuildGeneric()
        {
            GenericShaderFunction func = null;
            if ((genericTypeParameters == null) || (genericTypeParameters.Count <= 0))
            {
                Debug.LogError("Call ShaderFunction.Builder.Build() for non-generic functions");
            }
            else
            {
                func = new GenericShaderFunction(name, parameters, body.ConvertToString(), functionsCalled, includePaths, genericTypeParameters);
            }

            // clear data so we can't accidentally re-use it
            this.name = null;
            this.parameters = null;
            this.body = null;
            this.functionsCalled = null;
            this.includePaths = null;
            this.genericTypeParameters = null;

            return func;
        }
    }

    public sealed override bool Equals(object other)
    {
        return (other.GetType() == this.GetType()) && ValueEquals(other as ShaderFunction);
    }

    public bool Equals(ShaderFunction other)
    {
        return (other.GetType() == this.GetType()) && ValueEquals(other);
    }

    public override bool ValueEquals(ShaderFunctionSignature other)
    {
        // same reference means they are trivially equal
        // this is important to check first to reduce the work involved
        if (other == this)
            return true;

        if (other == null)
            return false;

        var otherFunc = other as ShaderFunction;
        if (otherFunc == null)
            return false;

        // Signature comparison
        if (!base.ValueEquals(other))
            return false;

        if (body != otherFunc.body)
            return false;

        if (parameters != otherFunc.parameters)
        {
            int inputCount = parameters?.Count ?? 0;
            int otherInputCount = otherFunc.parameters?.Count ?? 0;
            if (inputCount != otherInputCount)
                return false;

            // not obvious, but if either is null, count will be zero here
            for (int i = 0; i < inputCount; i++)
                if (!parameters[i].Equals(otherFunc.parameters[i]))
                    return false;
        }

        if (includePaths != otherFunc.includePaths)
        {
            int count = includePaths?.Count ?? 0;
            int otherCount = otherFunc.includePaths?.Count ?? 0;
            if (count != otherCount)
                return false;

            // not obvious, but if either is null, count will be zero here
            for (int i = 0; i < count; i++)
                if (!includePaths[i].Equals(otherFunc.includePaths[i]))
                    return false;
        }

        // this can get expensive if we have long function dependency chains that
        // use separate instances but are otherwise identical.
        // Try to minimize that by re-using shared global static ShaderFunctions
        // instead of creating multiple instances to represent the same function.
        if (functionsCalled != otherFunc.functionsCalled)
        {
            int count = functionsCalled?.Count ?? 0;
            int otherCount = otherFunc.functionsCalled?.Count ?? 0;
            if (count != otherCount)
                return false;

            // not obvious, but if either is null, count will be zero here
            for (int i = 0; i < count; i++)
                if (!functionsCalled[i].value.ValueEquals(otherFunc.functionsCalled[i].value))
                    return false;
        }

        return true;
    }

    public sealed override int GetHashCode()
    {
        // names should be unique enough that we can directly use them as the hash
        return name.GetHashCode();
    }

    /*
    internal string GetHLSLDeclarationString(string nameOverride = null)
    {
        ShaderStringBuilder sb = new ShaderStringBuilder();
        AppendHLSLDeclarationString(sb, nameOverride);
        return sb.ToCodeBlock();
    }
    */

    internal void AppendHLSLDeclarationString(ShaderStringBuilder sb, string nameOverride = null, string precision = "float")
    {
        var funcName = name;
        if (nameOverride != null)
            funcName = nameOverride;
        /*
                sb.EnsureCapacity(sb.Capacity + 64 +
                                    funcName.Length +
                                    body.Length +
                                    (inputs?.Count ?? 0) * 30 +
                                    (outputs?.Count ?? 0) * 35);
        */

        sb.Add("void ", funcName);
        /*
                if (genericTypes != null)
                {
                    for (int g = 0; g < genericTypes.Count; g++)
                    {
                        var gt = genericTypes[g].value;
                        var at = sb.FindLocalGenericType(gt.LocalName);
                        if (at == null)
                        {
                            sb.Add("_$");
                            sb.Add(gt.LocalName);
                        }
                        else
                        {
                            sb.Add("_", at.Name);
                        }
                    }
                }
        */
        sb.Add("(");

        bool first = true;
        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                if (!first)
                    sb.Add(", ");
                first = false;

                if (p.IsInOut)
                    sb.Add("inout ");
                else if (p.IsInput)
                    sb.Add("in ");
                else if (p.IsOutput)
                    sb.Add("out ");

                p.Type.AddHLSLVariableDeclarationString(sb, p.Name);
            }
        }

        sb.AddLine(")");
        sb.AddLine("{");
        sb.Add(body);
        sb.AddLine("}");
    }
}
