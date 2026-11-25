#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Booking.Verification;

public static class AttachmentPolicy
{
    private const string MsTest = "Microsoft.VisualStudio.TestTools.UnitTesting.";
    private const string Attachment = "forbidden-attachment";
    private const string Unsupported = "unsupported-execution";
    private const string Unresolved = "unresolved-metadata";
    private const string Invalid = "invalid-metadata";

    private static readonly HashSet<string> ApprovedReferences = new(StringComparer.Ordinal)
    {
        "System.Runtime", "System.Text.Json", "System.Collections.Immutable", "System.Collections",
        "System.Xml.XDocument", "System.Diagnostics.Process", "System.Linq", "System.Threading",
        "System.ComponentModel", "System.Text.RegularExpressions", "System.Security.Cryptography",
        "System.Memory", "System.Xml.ReaderWriter", "System.Console",
        "Microsoft.Testing.Platform", "Microsoft.Testing.Extensions.MSBuild", "Microsoft.Testing.Extensions.Telemetry",
        "MSTest.TestAdapter", "MSTest.TestFramework", "MSTest.TestFramework.Extensions", "Microsoft.Testing.Extensions.TrxReport",
        "Microsoft.Testing.Extensions.CodeCoverage"
    };

    private static readonly HashSet<string> MetadataReflection = new(StringComparer.Ordinal)
    {
        "System.Type.GetTypeFromHandle", "System.Type.op_Equality", "System.Type.get_Assembly",
        "System.Type.get_FullName", "System.Type.GetMethods",
        "System.Reflection.Assembly.GetTypes", "System.Reflection.Assembly.GetName",
        "System.Reflection.AssemblyName.get_Name", "System.Reflection.MemberInfo.get_Name",
        "System.Reflection.MemberInfo.get_DeclaringType", "System.Reflection.MemberInfo.IsDefined",
        "System.Reflection.CustomAttributeExtensions.GetCustomAttributes",
        "System.Reflection.CustomAttributeExtensions.GetCustomAttribute",
        "System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray",
        "System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan"
    };

    private static readonly Dictionary<ushort, OpCode> Instructions = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .Where(op => op.OpCodeType != OpCodeType.Nternal)
        .ToDictionary(op => unchecked((ushort)op.Value));

    public sealed record Verdict(int policyVersion, string outcome, string? failureCode, int assembliesChecked,
        int methodsChecked, string[] assemblySha256);

    public static Verdict Inspect(string[]? paths)
    {
        var modules = new Dictionary<string, Module>(StringComparer.Ordinal);
        var digests = new List<string>();
        int assembliesChecked = 0;
        int methodsChecked = 0;
        string? failure = null;
        try
        {
            Require(paths is { Length: > 0 }, "missing-assembly-input");
            foreach (string path in paths!)
            {
                byte[] bytes = File.ReadAllBytes(path);
                digests.Add(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
                var module = new Module(bytes);
                if (!modules.TryAdd(module.Name, module))
                {
                    module.Dispose();
                    throw new PolicyFailure("duplicate-assembly-input");
                }
            }

            foreach (var module in modules.Values)
            {
                Validate(module, modules, ref methodsChecked);
                assembliesChecked++;
            }
        }
        catch (PolicyFailure rejected)
        {
            failure = rejected.Code;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            failure = "unreadable-assembly";
        }
        catch (Exception error) when (error is BadImageFormatException or InvalidOperationException or OverflowException
            or IndexOutOfRangeException)
        {
            failure = Invalid;
        }
        finally
        {
            foreach (var module in modules.Values)
                module.Dispose();
        }

        return new(1, failure is null ? "passed" : "failed", failure, assembliesChecked, methodsChecked, digests.ToArray());
    }

    private static void Validate(Module module, Dictionary<string, Module> modules, ref int methodsChecked)
    {
        var reader = module.Reader;
        Require(reader.AssemblyFiles.Count == 0, Unsupported);
        foreach (var reference in reader.AssemblyReferences)
        {
            string name = reader.GetString(reader.GetAssemblyReference(reference).Name);
            Require(name is not ("Microsoft.CSharp" or "System.Linq.Expressions" or "System.Reflection.Emit"
                or "System.Reflection.Emit.ILGeneration" or "System.Runtime.Loader"), Unsupported);
            Require(ApprovedReferences.Contains(name) || modules.ContainsKey(name), Unresolved);
        }

        var signatures = new SignatureTypes(module, modules);
        foreach (var reference in reader.TypeReferences)
            signatures.Resolve(reference);
        foreach (var reference in reader.MemberReferences)
            ValidateMember(reader, signatures, reference);

        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (MetadataTokens.GetRowNumber(handle) == 1)
            {
                Require(reader.GetString(type.Name) == "<Module>", Invalid);
                Require(type.GetMethods().Count == 0, Unsupported);
            }
            foreach (var implementation in type.GetInterfaceImplementations())
            {
                string implemented = signatures.Resolve(reader.GetInterfaceImplementation(implementation).Interface);
                Require(!implemented.StartsWith("Microsoft.Testing.Platform.Extensions.TestFramework.", StringComparison.Ordinal)
                    && !implemented.StartsWith("Microsoft.Testing.Platform.Extensions.TestHostControllers.", StringComparison.Ordinal),
                    Unsupported);
            }
            var baseType = type.BaseType.IsNil ? null : signatures.Resolve(type.BaseType);
            Require(baseType is null || !baseType.StartsWith(MsTest, StringComparison.Ordinal), Unsupported);
            if (Attributes(reader, signatures, type.GetCustomAttributes()).Contains(MsTest + "TestClassAttribute"))
            {
                Require(baseType == "System.Object" && (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public
                    && (type.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed)) == TypeAttributes.Sealed
                    && type.GetGenericParameters().Count == 0, Unsupported);
            }
        }

        foreach (var handle in reader.CustomAttributes)
        {
            var attribute = reader.GetCustomAttribute(handle);
            string type = AttributeType(reader, signatures, attribute);
            if (type.StartsWith(MsTest, StringComparison.Ordinal))
            {
                bool supported = type switch
                {
                    MsTest + "TestClassAttribute" => attribute.Parent.Kind == HandleKind.TypeDefinition,
                    MsTest + "TestMethodAttribute" or MsTest + "TestCategoryAttribute" => attribute.Parent.Kind == HandleKind.MethodDefinition,
                    _ => false
                };
                Require(supported, Unsupported);
            }
        }

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            Require((method.Attributes & MethodAttributes.PinvokeImpl) == 0
                && (method.ImplAttributes & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.InternalCall
                    | MethodImplAttributes.ManagedMask)) == MethodImplAttributes.IL, Unsupported);
            Require(reader.GetString(method.Name) != "AddResultFile", Attachment);
            var signature = method.DecodeSignature(signatures, null);
            if (Attributes(reader, signatures, method.GetCustomAttributes()).Contains(MsTest + "TestMethodAttribute"))
            {
                var type = reader.GetTypeDefinition(method.GetDeclaringType());
                Require(Attributes(reader, signatures, type.GetCustomAttributes()).Contains(MsTest + "TestClassAttribute")
                    && (method.Attributes & (MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Abstract))
                        == MethodAttributes.Public
                    && signature.Header.CallingConvention == SignatureCallingConvention.Default
                    && signature.ParameterTypes.Length == 0 && signature.GenericParameterCount == 0
                    && (signature.ReturnType is "Void" or "System.Threading.Tasks.Task"), Unsupported);
            }

            if (method.RelativeVirtualAddress == 0)
                Require((method.Attributes & MethodAttributes.Abstract) != 0, Unresolved);
            else
                ValidateBody(reader, signatures, module.Pe.GetMethodBody(method.RelativeVirtualAddress));
            methodsChecked++;
        }
    }

    private static IEnumerable<string> Attributes(MetadataReader reader, SignatureTypes signatures,
        CustomAttributeHandleCollection attributes)
        => attributes.Select(handle => AttributeType(reader, signatures, reader.GetCustomAttribute(handle)));

    private static string AttributeType(MetadataReader reader, SignatureTypes signatures, CustomAttribute attribute)
    {
        var constructor = attribute.Constructor;
        return constructor.Kind switch
        {
            HandleKind.MemberReference => signatures.Resolve(reader.GetMemberReference((MemberReferenceHandle)constructor).Parent),
            HandleKind.MethodDefinition => signatures.Resolve(reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()),
            _ => throw new PolicyFailure(Unresolved)
        };
    }

    private static void ValidateMember(MetadataReader reader, SignatureTypes signatures, MemberReferenceHandle handle)
    {
        var member = reader.GetMemberReference(handle);
        string name = reader.GetString(member.Name);
        string type = signatures.Resolve(member.Parent);
        Require(name != "AddResultFile", Attachment);
        Require(!(type == "System.Activator" || type == "System.AppDomain"
            || type is "System.Delegate" or "System.MulticastDelegate" && name is "DynamicInvoke" or "CreateDelegate"
            || type == "System.Runtime.CompilerServices.RuntimeHelpers" && (name.StartsWith("Run", StringComparison.Ordinal)
                || name.StartsWith("Prepare", StringComparison.Ordinal) || name == "GetUninitializedObject")), Unsupported);
        if (type == "System.Type" || type.StartsWith("System.Reflection.", StringComparison.Ordinal)
            || type.StartsWith("System.Runtime.InteropServices.", StringComparison.Ordinal))
        {
            bool runtimeAttribute = type.StartsWith("System.Reflection.", StringComparison.Ordinal)
                && type.EndsWith("Attribute", StringComparison.Ordinal) && name == ".ctor";
            Require(runtimeAttribute || MetadataReflection.Contains(type + "." + name), Unsupported);
        }

        if (type.StartsWith("Microsoft.Testing.Platform.Builder.", StringComparison.Ordinal)
            || type == "Microsoft.Testing.Platform.TestHost.ITestHostManager")
        {
            Require((type, name) is
                ("Microsoft.Testing.Platform.Builder.TestApplication", "CreateBuilderAsync") or
                ("Microsoft.Testing.Platform.Builder.ITestApplicationBuilder", "get_TestHost" or "BuildAsync") or
                ("Microsoft.Testing.Platform.Builder.ITestApplication", "RunAsync") or
                ("Microsoft.Testing.Platform.TestHost.ITestHostManager", "AddDataConsumer"), Unsupported);
        }

        if (member.GetKind() == MemberReferenceKind.Field)
            member.DecodeFieldSignature(signatures, null);
        else
            member.DecodeMethodSignature(signatures, null);
    }

    private static void ValidateBody(MetadataReader reader, SignatureTypes signatures, MethodBodyBlock body)
    {
        if (!body.LocalSignature.IsNil)
            reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(signatures, null);
        foreach (var region in body.ExceptionRegions)
        {
            if (region.Kind == ExceptionRegionKind.Catch)
                signatures.Resolve(region.CatchType);
        }

        var il = body.GetILReader();
        var boundaries = new HashSet<int>();
        var branches = new List<int>();
        while (il.RemainingBytes != 0)
        {
            boundaries.Add(il.Offset);
            ushort value = il.ReadByte();
            if (value == 0xfe)
                value = (ushort)(0xfe00 | il.ReadByte());
            Require(Instructions.TryGetValue(value, out var instruction), Invalid);
            Require(instruction != OpCodes.Calli && instruction != OpCodes.Jmp, Unsupported);
            switch (instruction.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineVar:
                case OperandType.ShortInlineI:
                    il.ReadByte();
                    break;
                case OperandType.InlineVar:
                    il.ReadUInt16();
                    break;
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                    il.ReadInt32();
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    il.ReadInt64();
                    break;
                case OperandType.ShortInlineBrTarget:
                    int shortDelta = il.ReadSByte();
                    branches.Add(il.Offset + shortDelta);
                    break;
                case OperandType.InlineBrTarget:
                    int delta = il.ReadInt32();
                    branches.Add(checked(il.Offset + delta));
                    break;
                case OperandType.InlineSwitch:
                    int count = il.ReadInt32();
                    Require(count >= 0 && count <= il.RemainingBytes / 4, Invalid);
                    int end = il.Offset + count * 4;
                    for (int index = 0; index < count; index++)
                        branches.Add(checked(end + il.ReadInt32()));
                    break;
                case OperandType.InlineString:
                    var text = MetadataTokens.Handle(il.ReadInt32());
                    Require(text.Kind == HandleKind.UserString && !text.IsNil, Unresolved);
                    reader.GetUserString((UserStringHandle)text);
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    ValidateToken(reader, signatures, MetadataTokens.EntityHandle(il.ReadInt32()), instruction.OperandType);
                    break;
                default:
                    throw new PolicyFailure(Unsupported);
            }
        }
        Require(branches.All(boundaries.Contains), Invalid);
    }

    private static void ValidateToken(MetadataReader reader, SignatureTypes signatures, EntityHandle handle, OperandType operand)
    {
        Require(!handle.IsNil, Unresolved);
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification
                when operand is OperandType.InlineType or OperandType.InlineTok:
                signatures.Resolve(handle);
                break;
            case HandleKind.FieldDefinition when operand is OperandType.InlineField or OperandType.InlineTok:
                reader.GetFieldDefinition((FieldDefinitionHandle)handle).DecodeSignature(signatures, null);
                break;
            case HandleKind.MethodDefinition when operand is OperandType.InlineMethod or OperandType.InlineTok:
                reader.GetMethodDefinition((MethodDefinitionHandle)handle).DecodeSignature(signatures, null);
                break;
            case HandleKind.MemberReference:
                var kind = reader.GetMemberReference((MemberReferenceHandle)handle).GetKind();
                Require(operand == OperandType.InlineTok || (operand == OperandType.InlineMethod && kind == MemberReferenceKind.Method)
                    || (operand == OperandType.InlineField && kind == MemberReferenceKind.Field), Unresolved);
                ValidateMember(reader, signatures, (MemberReferenceHandle)handle);
                break;
            case HandleKind.MethodSpecification when operand is OperandType.InlineMethod or OperandType.InlineTok:
                var method = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                Require(method.Method.Kind is HandleKind.MethodDefinition or HandleKind.MemberReference, Unresolved);
                ValidateToken(reader, signatures, method.Method, OperandType.InlineMethod);
                method.DecodeSignature(signatures, null);
                break;
            default:
                throw new PolicyFailure(Unresolved);
        }
    }

    private static void Require(bool condition, string code)
    {
        if (!condition)
            throw new PolicyFailure(code);
    }

    private sealed class PolicyFailure(string code) : Exception
    {
        internal string Code { get; } = code;
    }

    private sealed class Module : IDisposable
    {
        internal Module(byte[] bytes)
        {
            Pe = new PEReader(new MemoryStream(bytes, writable: false));
            try
            {
                Require(Pe.HasMetadata && Pe.PEHeaders.CorHeader is { Flags: var flags }
                    && (flags & (CorFlags.ILOnly | CorFlags.NativeEntryPoint)) == CorFlags.ILOnly, Invalid);
                Reader = Pe.GetMetadataReader();
                Require(Reader.IsAssembly, Invalid);
                Name = Reader.GetString(Reader.GetAssemblyDefinition().Name);
                Require(Name.Length != 0 && !ApprovedReferences.Contains(Name), Invalid);
            }
            catch
            {
                Pe.Dispose();
                throw;
            }
        }

        internal PEReader Pe { get; }
        internal MetadataReader Reader { get; }
        internal string Name { get; }
        public void Dispose() => Pe.Dispose();
    }

    private sealed class SignatureTypes(Module module, Dictionary<string, Module> modules)
        : ISignatureTypeProvider<string, object?>
    {
        private readonly HashSet<EntityHandle> resolving = new();

        internal string Resolve(EntityHandle handle)
        {
            var reader = module.Reader;
            Require(!handle.IsNil && resolving.Count < 64 && resolving.Add(handle), Unresolved);
            try
            {
                return handle.Kind switch
                {
                    HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
                    HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
                    HandleKind.TypeSpecification => reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                        .DecodeSignature(this, null),
                    _ => throw new PolicyFailure(Unresolved)
                };
            }
            finally
            {
                resolving.Remove(handle);
            }
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var definition = reader.GetTypeDefinition(handle);
            return Name(reader, definition.Namespace, definition.Name);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var reference = reader.GetTypeReference(handle);
            string name = Name(reader, reference.Namespace, reference.Name);
            var scope = reference.ResolutionScope;
            if (scope.Kind == HandleKind.TypeReference)
                name = Resolve(scope) + "+" + name;
            else if (scope.Kind == HandleKind.AssemblyReference && !scope.IsNil)
            {
                string assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
                if (modules.TryGetValue(assembly, out var owned))
                {
                    var target = owned.Reader;
                    Require(target.TypeDefinitions.Any(type => GetTypeFromDefinition(target, type, 0) == name), Unresolved);
                }
                else
                    Require(ApprovedReferences.Contains(assembly), Unresolved);
            }
            else
                Require(scope.Kind == HandleKind.ModuleDefinition && !scope.IsNil && MetadataTokens.GetRowNumber(scope) == 1, Unresolved);

            Require(!(name.StartsWith("System.Reflection.Emit.", StringComparison.Ordinal)
                || name.StartsWith("System.Runtime.Loader.", StringComparison.Ordinal)
                || name.StartsWith("System.Linq.Expressions.", StringComparison.Ordinal)
                || name is "System.Runtime.CompilerServices.UnsafeAccessorAttribute"
                    or "System.Runtime.CompilerServices.ModuleInitializerAttribute"
                    or "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"), Unsupported);
            return name;
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind)
            => Resolve(handle);

        public string GetPrimitiveType(PrimitiveTypeCode type) => type.ToString();
        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> arguments) => genericType;
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetFunctionPointerType(MethodSignature<string> signature) => throw new PolicyFailure(Unsupported);
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => elementType;

        private static string Name(MetadataReader reader, StringHandle ns, StringHandle type)
        {
            string name = reader.GetString(type);
            Require(name.Length != 0, Unresolved);
            return ns.IsNil ? name : reader.GetString(ns) + "." + name;
        }
    }
}
