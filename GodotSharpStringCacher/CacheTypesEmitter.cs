using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace GodotSharpStringCacher;

internal class CacheTypesEmitter(Context ctx)
{
	public const string STRING_NAME_CACHE_TYPE_NAME = "?_StringNameCache";
	public const string NODE_PATH_CACHE_TYPE_NAME = "?_NodePathCache";

	FieldSignature StringNameFieldSig = null!;
	FieldSignature NodePathFieldSig = null!;

	internal readonly Dictionary<string, FieldDefinition> StringNamesToCache = [];
	internal readonly Dictionary<string, FieldDefinition> NodePathsToCache = [];

	public void Reset(bool regenerateSignatures)
	{
		if (regenerateSignatures)
		{
			StringNameFieldSig = new(ctx.Imported_StringNameType.ToTypeSignature(false));
			NodePathFieldSig = new(ctx.Imported_NodePathType.ToTypeSignature(false));
		}
		StringNamesToCache.Clear();
		NodePathsToCache.Clear();
	}

	public FieldDefinition AddStringName(string value)
	{
		if (StringNamesToCache.TryGetValue(value, out var fld))
			return fld;
		var fieldName = ctx.Config.UseLongNames ? GetFieldName(value, StringNamesToCache.Values) : $"_{StringNamesToCache.Count}";
		var field = new FieldDefinition(fieldName, FieldAttributes.Public | FieldAttributes.Static, StringNameFieldSig);
		StringNamesToCache.Add(value, field);
		return field;
	}

	public FieldDefinition AddNodePath(string value)
	{
		if (NodePathsToCache.TryGetValue(value, out var fld))
			return fld;
		
		var fieldName = ctx.Config.UseLongNames ? GetFieldName(value, NodePathsToCache.Values) : $"_{NodePathsToCache.Count}";
		var field = new FieldDefinition(fieldName, FieldAttributes.Public | FieldAttributes.Static, NodePathFieldSig);
		NodePathsToCache.Add(value, field);
		return field;
	}

	/// <summary>
	/// Emits the static types that cache NodePath and StringName values
	/// </summary
	public void EmitTypes()
	{
		var objectType = ctx.Module.CorLibTypeFactory.Object.GetUnderlyingTypeDefOrRef();
		var staticConstructorSig = new MethodSignature(CallingConventionAttributes.Default, ctx.Module.CorLibTypeFactory.Void, null);
		TypeDefinition EmitType(string name, Dictionary<string, FieldDefinition> namesToCache, IMethodDescriptor ctorMethod)
		{
			var type = new TypeDefinition("", name, TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit)
			{
				BaseType = objectType
			};

			/*
				FYI .cctor is the name of a type's static constructor.
				Writing `static Foo bar = new Foo();` is syntaxic sugar for

				static Foo bar;

				static DeclaringClass()
				{
					bar = new Foo();
				}
			*/
			var cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.RuntimeSpecialName | MethodAttributes.SpecialName, staticConstructorSig)
			{
				CilMethodBody = new CilMethodBody()
			};
			var instructions = cctor.CilMethodBody.Instructions;

			foreach (var kv in namesToCache)
			{
				var value = kv.Key;
				var field = kv.Value;
				type.Fields.Add(field);

				instructions.Add(CilOpCodes.Ldstr, value);
				instructions.Add(CilOpCodes.Newobj, ctorMethod);
				instructions.Add(CilOpCodes.Stsfld, field);
			}
			instructions.Add(CilOpCodes.Ret);
			type.Methods.Add(cctor);
			ctx.Module.TopLevelTypes.Add(type);

			return type;
		}

		if (StringNamesToCache.Count != 0)
			EmitType(STRING_NAME_CACHE_TYPE_NAME, StringNamesToCache, ctx.Imported_StringName_StringCtor);

		if (NodePathsToCache.Count != 0)
			EmitType(NODE_PATH_CACHE_TYPE_NAME, NodePathsToCache, ctx.Imported_NodePath_StringCtor);
	}
	
	/// <summary>
	/// Turns a string value to a CIL field name with a closely resembling name.
	/// This can help static analysis, but makes patching slower
	/// </summary>
	/// <param name="existingFields">Existing field names to check for duplicates</param>
	string GetFieldName(string value, ICollection<FieldDefinition> existingFields)
	{
		// TODO: surely it can't be that simple, even if CIL accepts unicode
		string sanitized = value.Replace(' ', '_');

		string attempt = $"_{sanitized}";
		// Check if a field already has this name
		if (!existingFields.Any(x => x.Name == attempt))
			return attempt;
		int trailing = 0;
		do
		{
			attempt = $"_{sanitized}_{trailing}";
			trailing++;
		}
		while (existingFields.Any(x => x.Name == attempt));
		return attempt;
	}
}
