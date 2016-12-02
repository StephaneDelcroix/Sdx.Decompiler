//
// Decompiler.cs
//
// Author:
//       Stephane Delcroix <stdelc@microsoft.com>
//
// Copyright (c) 2016 
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Options;
using ICSharpCode.Decompiler;
using System.IO;
using Mono.Cecil;
using ICSharpCode.Decompiler.Ast;

namespace Sdx.Decompiler
{
	class Decompiler
	{
		static readonly string helpString = "decompile.exe - utility for decompiling IL.";
		static readonly string usageString = "decompile.exe [options] assembly.dll. Type `decompile.exe -h` for help";

		public static void Main(string[] args)
		{
			bool help = false;
			int verbosity = 1;
			string outputDir = "Decompiled";
			string restrictType = null;
			IList<string> extra = null;


			var p = new OptionSet {
				{ "h|?|help", "Print this help message", v => help = true },
				{ "d=", "Output directory", v => outputDir = v },
				{ "t=", "Only decompile this type", v => restrictType = v},
				{ "v=|verbosity=", "0 is quiet, 1 is normal, 2 is verbose", v => verbosity = int.Parse(v) },
			};

			try {
				extra = p.Parse(args);
			} catch (OptionException oe) {
				if (verbosity > 0)
					Console.Error.WriteLine($"decompile.exe: argument error:{oe.Message}.\n{usageString}");
				Environment.Exit(0);
			}

			if (help) {
				ShowHelp(p);
				return;
			}

			if (extra.Count != 1) {
				if (verbosity > 0)
					Console.Error.WriteLine($"decompile.exe: missing assembly parameter.\n{usageString}.");
				Environment.Exit(0);
			}

			var assemblyPath = extra[0];
			if (!File.Exists(assemblyPath)) {
				if (verbosity > 0)
					Console.Error.WriteLine($"decompile.exe: assembly not found.\n{usageString}.");
				Environment.Exit(0);
			}

			IAssemblyResolver resolver = new DefaultAssemblyResolver();

			var dir = Path.GetDirectoryName(assemblyPath);
			if (!string.IsNullOrEmpty(dir)) {
				var assemblies = from file in Directory.GetFiles(dir)
								 where file != assemblyPath && (Path.GetExtension(file) == ".exe" || Path.GetExtension(file) == ".dll")
								 select file;

				resolver = new PathResolver();
				foreach (var asm in assemblies)
					((PathResolver)resolver).AddAssembly(asm);
			}

			var directory = Directory.CreateDirectory(outputDir);
			var assemblyDef = AssemblyDefinition.ReadAssembly(Path.GetFullPath(assemblyPath), new ReaderParameters {
				AssemblyResolver = resolver 
			});

			var settings = new DecompilerSettings {
			};

			var types = from moduleDef in assemblyDef.Modules
						from type in moduleDef.Types
						select type;

			foreach (var typeDef in types) {
				if (typeDef.IsSpecialName)
					continue;
				if (!string.IsNullOrEmpty(restrictType) && typeDef.FullName != restrictType)
					continue;
				var filepath = Path.Combine(directory.Name, typeDef.FullName + ".cs");
				if (verbosity >= 2)
					Console.Write($"Decompiling {typeDef.FullName} into {filepath}...");
				var decompilerContext = new DecompilerContext(typeDef.Module) {
					Settings = settings
				};
				using (var writer = new StreamWriter(filepath)) {
					var output = new PlainTextOutput(writer);
					var builder = new AstBuilder(decompilerContext);
					builder.AddType(typeDef);
					builder.GenerateCode(output);
				}

				if (verbosity >= 2)
					Console.WriteLine($"done.");
			}
		}

		static void ShowHelp(OptionSet ops)
		{
			Console.WriteLine($"{helpString}\n{usageString}\n");
			ops.WriteOptionDescriptions(Console.Out);
		}
	}
}