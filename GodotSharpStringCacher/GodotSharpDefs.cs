using AsmResolver.DotNet;

namespace GodotSharpStringCacher;

internal class GodotSharpDefs
{
	private GodotSharpDefs(ModuleDefinition godotSharpModule)
	{
		Module = godotSharpModule;

		StringNameType = Module.TopLevelTypes.First(x => x.FullName == "Godot.StringName");
		StringName_StringCtor = StringNameType.Methods.First(x => 
			x.IsConstructor &&
			x.Parameters.Count == 1 &&
			x.Parameters[0].ParameterType.FullName == "System.String"
		);
		NodePathType = Module.TopLevelTypes.First(x => x.FullName == "Godot.NodePath");
		NodePath_StringCtor = NodePathType.Methods.First(x => 
			x.IsConstructor &&
			x.Parameters.Count == 1 &&
			x.Parameters[0].ParameterType.FullName == "System.String"
		);
	}

	public readonly ModuleDefinition Module;

	public readonly TypeDefinition StringNameType;
	public readonly MethodDefinition StringName_StringCtor;
	public readonly TypeDefinition NodePathType;
	public readonly MethodDefinition NodePath_StringCtor;

	public static GodotSharpDefs FromReferencingModule(ModuleDefinition module, IAssemblyResolver assemblyResolver)
	{
		var godotSharpRef = module.AssemblyReferences.FirstOrDefault(x => x.Name == "GodotSharp") ?? throw new NoGodotSharpReferenceExeption(module);

		var result = assemblyResolver.Resolve(godotSharpRef, module, out var definition);

		if (result != ResolutionStatus.Success)
			throw new FileLoadException($"Could not load {godotSharpRef}: {result}");

		return new (definition!.ManifestModule!);
	}
}
