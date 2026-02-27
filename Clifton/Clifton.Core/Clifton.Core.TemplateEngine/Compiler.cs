/* The MIT License (MIT)
* 
* Copyright (c) 2015 Marc Clifton
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using Clifton.Core.ExtensionMethods;

namespace Clifton.Core.TemplateEngine
{
	public static class Compiler
	{
		public static string TemplateEnginePath = null;

		public static Assembly Compile(string code, out List<string> errors, List<string> references = null)
		{
			errors = null;
			var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp7_3);
			var syntaxTree = CSharpSyntaxTree.ParseText(code, parseOptions);
			var metadataReferences = ResolveMetadataReferences(references);
			var compilation = CSharpCompilation.Create(
				"RuntimeCompiled_" + Guid.NewGuid().ToString("N"),
				new[] { syntaxTree },
				metadataReferences,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));

			using (var peStream = new MemoryStream())
			using (var pdbStream = new MemoryStream())
			{
				EmitResult emitResult = compilation.Emit(peStream, pdbStream);

				if (!emitResult.Success)
				{
					errors = emitResult.Diagnostics
						.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
						.Select(diagnostic => diagnostic.ToString())
						.ToList();
					return null;
				}

				peStream.Position = 0;
				pdbStream.Position = 0;
				return Assembly.Load(peStream.ToArray(), pdbStream.ToArray());
			}
		}

		private static List<MetadataReference> ResolveMetadataReferences(List<string> references)
		{
			var metadataReferences = new List<MetadataReference>();
			var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
				.Split(Path.PathSeparator)
				.ToList();

			trustedPlatformAssemblies
				.ForEach(path => metadataReferences.Add(MetadataReference.CreateFromFile(path)));

			var templateEngineAssembly = TemplateEnginePath ?? typeof(TemplateEngine).Assembly.Location;
			if (File.Exists(templateEngineAssembly))
			{
				metadataReferences.Add(MetadataReference.CreateFromFile(templateEngineAssembly));
			}

			references.IfNotNull(refs =>
			{
				refs.ForEach(reference =>
				{
					if (string.IsNullOrWhiteSpace(reference))
					{
						return;
					}

					string resolvedPath = null;

					if (File.Exists(reference))
					{
						resolvedPath = Path.GetFullPath(reference);
					}
					else
					{
						var candidatePath = Path.Combine(AppContext.BaseDirectory, reference);
						if (File.Exists(candidatePath))
						{
							resolvedPath = candidatePath;
						}
						else
						{
							resolvedPath = trustedPlatformAssemblies.FirstOrDefault(path =>
								Path.GetFileName(path).Equals(reference, StringComparison.OrdinalIgnoreCase) ||
								path.EndsWith(reference, StringComparison.OrdinalIgnoreCase));
						}
					}

					if (string.IsNullOrEmpty(resolvedPath))
					{
						return;
					}

					if (metadataReferences.Any(existingReference =>
							string.Equals(existingReference.Display, resolvedPath, StringComparison.OrdinalIgnoreCase)))
					{
						return;
					}

					metadataReferences.Add(MetadataReference.CreateFromFile(resolvedPath));
				});
			});

			return metadataReferences;
		}
	}
}

