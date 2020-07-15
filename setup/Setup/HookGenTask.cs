﻿using Mono.Cecil;
using MonoMod;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using XnaToFna;
using static Terraria.ModLoader.Setup.Program;

namespace Terraria.ModLoader.Setup
{
	internal class HookGenTask : SetupOperation
	{
		const string libsPath = "src/tModLoader/Terraria/Libraries";

		public HookGenTask(ITaskInterface taskInterface) : base(taskInterface)
		{
		}

		public override void Run()
		{
			string targetExePath = @"src/tModLoader/Terraria/bin/WindowsDebug/net45/Terraria.exe";
			if (!File.Exists(targetExePath)) {
				var result = MessageBox.Show($"\"{targetExePath}\" does not exist. Use Vanilla exe instead?", "tML exe not found", MessageBoxButton.YesNo);
				if (result != MessageBoxResult.Yes) {
					taskInterface.SetStatus("Cancelled");
					return;
				}

				if (!File.Exists(TerrariaPath))
					throw new FileNotFoundException(TerrariaPath);

				targetExePath = TerrariaPath;
			}
			var outputPath = Path.Combine(libsPath, "XNA", "TerrariaHooks.dll");
			if (File.Exists(outputPath))
				File.Delete(outputPath);

			taskInterface.SetStatus($"Hooking: Terraria.exe -> XNA/TerrariaHooks.dll");
			HookGen(targetExePath, outputPath);

			taskInterface.SetStatus($"XnaToFna: XNA/TerrariaHooks.dll -> FNA/TerrariaHooks.dll");

			var fnaPath = Path.Combine(libsPath, "FNA", "TerrariaHooks.dll");
			if (File.Exists(fnaPath))
				File.Delete(fnaPath);

			File.Copy(outputPath, fnaPath);
			XnaToFna(fnaPath);

			File.Delete(Path.ChangeExtension(fnaPath, "pdb"));

			MessageBox.Show("Success. Make sure you diff tModLoader after this");
		}

		public static void HookGen(string inputPath, string outputPath)
		{
			using var mm = new MonoModder {
				InputPath = inputPath,
				OutputPath = outputPath,
				ReadingMode = ReadingMode.Deferred,
				
				DependencyDirs = { 
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5",
					Path.Combine(libsPath, "Common")
				},
				MissingDependencyThrow = false,
			};

			mm.Read();
			mm.MapDependencies();

			var gen = new HookGenerator(mm, "TerrariaHooks") {
				HookPrivate = true,
			};
			gen.Generate();
			RemoveModLoaderTypes(gen.OutputModule);
			gen.OutputModule.Write(outputPath);
		}

		private static void RemoveModLoaderTypes(ModuleDefinition module)
		{
			for (int i = module.Types.Count - 1; i >= 0; i--)
				if (module.Types[i].FullName.Contains("Terraria.ModLoader"))
					module.Types.RemoveAt(i);
		}

		public static void XnaToFna(string inputPath)
		{
			using var xnaToFnaUtil = new XnaToFnaUtil {
				HookCompat = false,
				HookHacks = false,
				HookEntryPoint = false,
				HookBinaryFormatter = false,
				HookReflection = false,
				AddAssemblyReference = false
			};
			var fnaPath = Path.Combine(libsPath, "FNA", "FNA.dll");
			xnaToFnaUtil.ScanPath(fnaPath);
			xnaToFnaUtil.ScanPath(inputPath);

			AppDomain.CurrentDomain.AssemblyResolve += (sender, resArgs) => new AssemblyName(resArgs.Name).Name == "FNA" ? Assembly.Load(File.ReadAllBytes(fnaPath)) : null;
			xnaToFnaUtil.RelinkAll();
		}
	}
}