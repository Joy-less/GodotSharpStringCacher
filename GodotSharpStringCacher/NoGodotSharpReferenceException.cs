using System;
using AsmResolver.DotNet;

namespace GodotSharpStringCacher;

public class NoGodotSharpReferenceExeption(ModuleDefinition module) : Exception
{
	readonly string Text = $"Module {module} does not contain a reference to GodotSharp.";

	public override string Message => Text;
}
