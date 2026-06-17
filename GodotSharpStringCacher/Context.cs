using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;

namespace GodotSharpStringCacher;

public class Context
{
	internal ModuleDefinition Module { get; private set; } = null!;

	internal RuntimeContext RuntimeContext { get; private set; } = null!;

	internal string FileName { get; private set; } = null!;

	internal string? LastRunDirectory { get; private set; } = null;

	public Config Config { get; set; }

	internal GodotSharpDefs Defs { get; private set; } = null!;

	internal ITypeDefOrRef Imported_StringNameType { get; private set; } = null!;
	internal IMethodDescriptor Imported_StringName_StringCtor { get; private set; } = null!; 
	internal ITypeDefOrRef Imported_NodePathType { get; private set; } = null!;
	internal IMethodDescriptor Imported_NodePath_StringCtor { get; private set; } = null!;

	internal readonly CacheTypesEmitter CacheTypesEmitter;

	public Context(Config? config = null)
	{
		Config = config ?? Config.Default;

		CacheTypesEmitter = new(this);
	}

	public int NumberOfStringNamesWritten { get; set; }
	public int NumberOfNodePathsWritten { get; set; }

	public void RunAndSave(string inputFile, string outputFile)
	{
		FileName = inputFile;

		var directory = Path.GetDirectoryName(FileName) ?? throw new ArgumentException("Could not resolve directory name from module path");
		if (LastRunDirectory == null || LastRunDirectory != directory)
		{
			Module = ModuleDefinition.FromFile(FileName, createRuntimeContext: true);
			RuntimeContext = Module.RuntimeContext!;

			// since we are in a different directory, the GodotSharp assembly may not be the same, so we reload everything.
			var resolver = PathAssemblyResolver.FromSearchDirectories([directory]);

			Defs = GodotSharpDefs.FromReferencingModule(Module, resolver);
			Imported_StringNameType = Module.DefaultImporter.ImportType(Defs.StringNameType);
			Imported_StringName_StringCtor = Module.DefaultImporter.ImportMethod(Defs.StringName_StringCtor);
			Imported_NodePathType = Module.DefaultImporter.ImportType(Defs.NodePathType);
			Imported_NodePath_StringCtor = Module.DefaultImporter.ImportMethod(Defs.NodePath_StringCtor);
			
			CacheTypesEmitter.Reset(true);
			LastRunDirectory = directory;
		}
		else
		{
			Module = RuntimeContext.LoadAssembly(inputFile).ManifestModule ?? throw new NullReferenceException("ManifestModule is null");
			CacheTypesEmitter.Reset(false);
		}

		foreach (var moduleType in Module.GetAllTypes())
		{
			PatchType(moduleType);
		}
		CacheTypesEmitter.EmitTypes();
		Module.Write(outputFile);
		NumberOfStringNamesWritten = CacheTypesEmitter.StringNamesToCache.Count;
		NumberOfNodePathsWritten = CacheTypesEmitter.NodePathsToCache.Count;
	}

	void PatchType(TypeDefinition type)
	{
		foreach (var typeMethod in type.Methods.Where(x => x.CilMethodBody != null))
		{
			// No need to patch if we're already in a static constructor
			if (typeMethod.Name != ".cctor")
				MatchAndPatch(typeMethod);
		}
	}

	void MatchAndPatch(MethodDefinition method)
	{
		var instructions = method.CilMethodBody!.Instructions;
		bool hasExpandedMacros = false;

		// We are looking for this pattern:
		// IL ldstr "MY_CONSTANT"
		// IL call (Godot.StringName/Godot.NodePath)::op_Implicit(System.String)

		// Which we will replace with
		// IL ldsfld our_generated_field

		bool Match(int index)
		{
			// Poor man's code matching
			return instructions[index].OpCode == CilOpCodes.Ldstr && instructions[index + 1].OpCode == CilOpCodes.Call;
		}

		for (int i = 0; i < instructions.Count - 1; i++)
		{
			if (!Match(i))
				continue;
			var ldstrInstruction = instructions[i];
			var callInstruction = instructions[i + 1];

			if (callInstruction.Operand is not MemberReference calledMethod)
				continue;
			
			void MakeEdit(FieldDefinition field)
			{
				if (!hasExpandedMacros)
				{
					instructions.ExpandMacros();
					hasExpandedMacros = true;
				}
				instructions[i].ReplaceWith(CilOpCodes.Ldsfld, field);
				instructions.RemoveAt(i + 1);
			}

			if (IsStringToStringNameImplicitOp(calledMethod))
			{
				var field = CacheTypesEmitter.AddStringName((string)ldstrInstruction.Operand!);
				MakeEdit(field);
			}
			else if (IsStringToNodePathImplicitOp(calledMethod))
			{
				var field = CacheTypesEmitter.AddNodePath((string)ldstrInstruction.Operand!);
				MakeEdit(field);
			}
		}

		if (hasExpandedMacros)
			instructions.OptimizeMacros();
	}

	static bool IsStringToStringNameImplicitOp(IMethodDefOrRef method)
	{
		return method.Name == "op_Implicit" &&
			string.CompareOrdinal(method.DeclaringType?.FullName, "Godot.StringName") == 0 &&
			string.CompareOrdinal(method.Signature!.ReturnType.FullName, "Godot.StringName") == 0 &&
			method.Signature.GetTotalParameterCount() == 1 &&
			string.CompareOrdinal(method.Signature.ParameterTypes[0].FullName, "System.String") == 0;
	}

	static bool IsStringToNodePathImplicitOp(IMethodDefOrRef method)
	{
		return method.Name == "op_Implicit" &&
			string.CompareOrdinal(method.DeclaringType?.FullName, "Godot.NodePath") == 0 &&
			string.CompareOrdinal(method.Signature!.ReturnType.FullName, "Godot.NodePath") == 0 &&
			method.Signature.GetTotalParameterCount() == 1 &&
			string.CompareOrdinal(method.Signature.ParameterTypes[0].FullName, "System.String") == 0;
	}
}
