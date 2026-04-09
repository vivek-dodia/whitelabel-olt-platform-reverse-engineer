using System;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;

var dllPath = @"D:\fs-pon-manager\APP_OLT_Stick_V2.dll";
var outputDir = @"C:\Users\Vivek\Downloads\volt\fs\decompiled";
Directory.CreateDirectory(outputDir);

var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings());

// Decompile the entire assembly to C# source
var code = decompiler.DecompileWholeModuleAsString();
File.WriteAllText(Path.Combine(outputDir, "APP_OLT_Stick_V2.cs"), code);

Console.WriteLine($"Decompiled to {outputDir}/APP_OLT_Stick_V2.cs ({code.Length} chars)");
