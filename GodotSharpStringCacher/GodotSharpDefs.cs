using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace GodotSharpStringCacher;

internal class GodotSharpDefs
{
	private GodotSharpDefs(ModuleDefinition godotSharpModule)
	{
		Module = godotSharpModule;
		
		var signature = MethodSignature.CreateInstance(Module.CorLibTypeFactory.Void, [Module.CorLibTypeFactory.String]);

		StringNameType = Module.CreateTypeReference("Godot", "StringName");
		StringName_StringCtor = StringNameType.CreateMethodReference(".ctor", signature);
		
		NodePathType = Module.CreateTypeReference("Godot", "NodePath");
		NodePath_StringCtor = NodePathType.CreateMethodReference(".ctor", signature);
	}

	public readonly ModuleDefinition Module;

	public readonly TypeReference StringNameType;
	public readonly IMethodDescriptor StringName_StringCtor;
	public readonly TypeReference NodePathType;
	public readonly IMethodDescriptor NodePath_StringCtor;

	public static GodotSharpDefs FromReferencingModule(ModuleDefinition module, IAssemblyResolver assemblyResolver)
	{
		var godotSharpRef = module.AssemblyReferences.FirstOrDefault(x => x.Name == "GodotSharp") ?? throw new NoGodotSharpReferenceExeption(module);

		var result = assemblyResolver.Resolve(godotSharpRef, module, out var definition);

		if (result != ResolutionStatus.Success)
			throw new FileLoadException($"Could not load {godotSharpRef}: {result}");

		return new (definition!.ManifestModule!);
	}
}
