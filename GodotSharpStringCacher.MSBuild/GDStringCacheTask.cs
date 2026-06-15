using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace GodotSharpStringCacher.MSBuild
{
	public class GDStringCacheTask : Task
	{
		[Required]
		public string OutputPath { get; set; }

		[Required]
		public string AssemblyName { get; set; }

		[Required]
		public bool CacheMainAssemblyStrings { get; set; }

		[Required]
		public bool UseShortNames { get; set; }

		bool CacheOne(string path, string assemblyName, Config config)
		{
			try
			{
				var ctx = new Context(path, config);
				ctx.Run();
				ctx.Write(path);
				Log.LogMessage($"{assemblyName}: StringNames cached: {ctx.NumberOfStringNamesWritten}");
				Log.LogMessage($"{assemblyName}: NodePaths cached: {ctx.NumberOfNodePathsWritten}");
			}
			catch (NoGodotSharpReferenceExeption ex)
			{
				Log.LogWarning($"{assemblyName}: {ex.Message}");
			}
			catch (IOException ex)
			{
				Log.LogError($"{assemblyName}: An IO error occured: {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				if (ex.InnerException is IOException)
					Log.LogError($"{assemblyName}: An IO error occured: {ex.InnerException.Message}: {ex.Message}");
				else
					Log.LogError($"{assemblyName}: An unhandled exception occured: {ex}");
				return false;
			}
			return true;
		}

		public override bool Execute()
		{
			var config = new Config(UseShortNames);
			if (CacheMainAssemblyStrings)
			{
				string path = $"{OutputPath}{AssemblyName}.dll";
				if (!CacheOne(path, AssemblyName, config))
					return false;
			}
			return true;
		}
	}
}
