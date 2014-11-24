using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace YoloDev.UnpaK
{
    public class Program
    {
        private readonly IAssemblyLoaderContainer _container;
        private readonly IApplicationEnvironment _environment;
        private readonly IServiceProvider _serviceProvider;

        public Program(IAssemblyLoaderContainer container, IApplicationEnvironment environment, IServiceProvider serviceProvider)
        {
            _container = container;
            _environment = environment;
            _serviceProvider = serviceProvider;
        }

        public int Main(string[] args)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: true);
            app.Name = "YoloDev.UnpaK";
            var optionPackages = app.Option("--packages <PACKAGE_DIR>", "Directory containing packages", CommandOptionType.SingleValue);
            var optionConfiguration = app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue);
            var optionFx = app.Option("--framework <FRAMEWORK>", "The framework to target (overrides the default of Asp.Net 5.0)", CommandOptionType.SingleValue);
            var rootDir = app.Option("-r|--root <ROOT_DIR>", "Root directory (containing project.json, default to current working dir)", CommandOptionType.SingleValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            app.Command("props", pApp =>
            {
                var optionExt = pApp.Option("-e|--extension <PROJECT_EXTENSION>", "Project extension (default to 'props')", CommandOptionType.SingleValue);
                var optionImports = pApp.Option("-i|--import <IMPORT>", "Import targets to project", CommandOptionType.MultipleValue);

                pApp.OnExecute(() =>
                {
                    var packagesDirectory = optionPackages.Value();
                    var targetFramework = optionFx.HasValue() ? Project.ParseFrameworkName(optionFx.Value()) : _environment.RuntimeFramework;
                    var configuration = optionConfiguration.Value() ?? _environment.Configuration ?? "Debug";
                    var applicationBaseDirectory = rootDir.HasValue() ? rootDir.Value() : _environment.ApplicationBasePath;
                    var extension = optionExt.HasValue() ? optionExt.Value() : "props";

                    var info = Worker.Process(
                        packagesDirectory,
                        targetFramework,
                        configuration,
                        applicationBaseDirectory,
                        _serviceProvider,
                        _container);


                    var objDir = Path.Combine(info.Base, "obj", info.Configuration);
                    var objDllDir = Path.Combine(objDir, "dll");
                    if (!Directory.Exists(objDllDir))
                        Directory.CreateDirectory(objDllDir);

                    foreach (var ani in info.Dependencies.Where(d => d.IsANI))
                        ani.CopyTo(Path.Combine(objDllDir, ani.Name + ".dll"));

                    var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                    var propGroup = new XElement(("PropertyGroup"),
                        new XElement(("Configuration"), info.Configuration),
                        new XElement(("Platform"), "AnyCPU"),
                        new XElement(("SchemaVersion"), "2.0"),
                        new XElement(("ProjectGuid"), Guid.NewGuid().ToString()),
                        new XElement(("DebugSymbols"), "true"),
                        new XElement(("DebugType"), "full"),
                        new XElement(("Optimize"), "false"),
                        new XElement(("Tailcalls"), "false"),
                        new XElement(("ImplicitlyExpandDesignTimeFacades"), "false"),
                        new XElement(("OutputPath"), "bin/Debug"),
                        new XElement(("DefineConstants"), string.Join(";", info.Defines)),
                        new XElement(("OutputType"), "Library"),
                        new XElement(("Name"), info.Name),
                        new XElement(("RootNamespace"), info.Name),
                        new XElement(("AssemblyName"), info.Name),
                        new XElement(("TargetFrameworkVersion"), "v" + info.Framework.Version.ToString(2)),
                        new XElement(("WarningLevel"), "3"));
                    var imports = optionImports.Values.Select(i => new XElement(("Import"), new XAttribute(("Project"), i)));
                    var srcGroup = new XElement(("ItemGroup"),
                        info.Sources.Select(s =>
                        {
                            if (s.Kind == SourceInfo.SourceKind.Src)
                            {
                                return new XElement(("Compile"), new XAttribute("Include", s.Path));
                            }
                            else
                            {
                                return new XElement(("Compile"), new XAttribute("Include", s.Path));
                            }
                        }));
                    var refs = new XElement(("ItemGroup"),
                        info.Dependencies.Select(r => new XElement(("Reference"), new XAttribute(("Include"), r.Name),
                            new XElement(("HintPath"), !r.IsFile ? Path.Combine(objDllDir, r.Name + ".dll") : r.Path))));

                    var root = new XElement(("Project"),
                        new XAttribute(("ToolsVersion"), "4.0"),
                        new XAttribute(("DefaultTargets"), "Build"),
                        propGroup,
                        imports,
                        srcGroup,
                        refs);

                    root.SetDefaultXmlNamespace(ns);

                    var doc = new XDocument(root);

                    var fileName = Path.Combine(info.Base, info.Name + "." + extension);

                    if (File.Exists(fileName))
                        File.Delete(fileName);

                    using (var f = File.OpenWrite(fileName))
                        doc.Save(f);

                    return 0;
                });
            });

            app.Command("raw", rApp =>
            {
                var optionOut = rApp.Option("-o|--out <OUT_DIR>", "Output directory", CommandOptionType.SingleValue);

                rApp.OnExecute(() =>
                {
                    var packagesDirectory = optionPackages.Value();
                    var targetFramework = optionFx.HasValue() ? Project.ParseFrameworkName(optionFx.Value()) : _environment.RuntimeFramework;
                    var configuration = optionConfiguration.Value() ?? _environment.Configuration ?? "Debug";
                    var applicationBaseDirectory = rootDir.HasValue() ? rootDir.Value() : _environment.ApplicationBasePath;

                    var info = Worker.Process(
                        packagesDirectory,
                        targetFramework,
                        configuration,
                        applicationBaseDirectory,
                        _serviceProvider,
                        _container);

                    var outDir = optionOut.Value();
                    if (string.IsNullOrWhiteSpace(outDir))
                    {
                        Console.WriteLine("out parameter is required");
                        app.ShowHelp();
                        return -1;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(outDir);
                        if (fileInfo.Exists && (fileInfo.Attributes & FileAttributes.Directory) != FileAttributes.Directory)
                        {
                            Console.WriteLine("{0} exists and is a file", outDir);
                            return -1;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
                        {
                            Console.WriteLine("{0} is not a valid path", outDir);
                            return -1;
                        }

                        throw;
                    }

                    try
                    {
                        if (!Directory.Exists(outDir))
                            Directory.CreateDirectory(outDir);
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is DirectoryNotFoundException || ex is NotSupportedException)
                        {
                            Console.WriteLine("Could not create directory {0}", outDir);
                            return -1;
                        }

                        throw;
                    }

                    foreach (var fileEntry in Directory.EnumerateFileSystemEntries(outDir))
                    {
                        if (Directory.Exists(fileEntry))
                            Directory.Delete(fileEntry, true);
                        else
                            File.Delete(fileEntry);
                    }

                    var srcPath = Path.Combine(outDir, "src");
                    var libPath = Path.Combine(outDir, "lib");
                    var extSrcPath = Path.Combine(libPath, "src");
                    var srcPaths = new List<string>();
                    var libPaths = new List<string>();
                    var aniPaths = new List<string>();
                    var fxDefs = new List<string>();

                    Directory.CreateDirectory(srcPath);
                    Directory.CreateDirectory(libPath);

                    foreach (var source in info.Sources)
                    {
                        if (source.Kind == SourceInfo.SourceKind.Src)
                        {
                            var original = new FileInfo(Path.Combine(info.Base, source.Path));
                            var newPath = Path.Combine(srcPath, source.Path);
                            var pathDir = Path.GetDirectoryName(newPath);
                            if (!Directory.Exists(pathDir))
                                Directory.CreateDirectory(pathDir);

                            var newFile = original.CopyTo(newPath);
                            srcPaths.Add(newFile.FullName);
                        }
                        else
                        {
                            var file = Path.Combine(extSrcPath, source.Lib, Path.GetFileName(source.Path));
                            var dir = Path.GetDirectoryName(file);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            File.Copy(source.Path, file);
                            srcPaths.Add(file);
                        }
                    }

                    foreach (var dep in info.Dependencies)
                    {
                        var depPath = Path.Combine(libPath, dep.Name + ".dll");
                        dep.CopyTo(depPath);

                        if (dep.IsANI)
                        {
                            aniPaths.Add(depPath);
                        }
                        else
                        {
                            libPaths.Add(depPath);
                        }
                    }

                    fxDefs.AddRange(info.Defines);

                    File.WriteAllLines(Path.Combine(outDir, "sources.txt"), srcPaths);
                    File.WriteAllLines(Path.Combine(outDir, "references.txt"), libPaths.Distinct());
                    File.WriteAllLines(Path.Combine(outDir, "anis.txt"), aniPaths.Distinct());
                    File.WriteAllLines(Path.Combine(outDir, "defines.txt"), fxDefs);
                    File.WriteAllLines(Path.Combine(outDir, "version.txt"), new[] { info.Version.Version.ToString() });
                    File.WriteAllLines(Path.Combine(outDir, "full-version.txt"), new[] { info.Version.ToString() });
                    File.WriteAllLines(Path.Combine(outDir, "name.txt"), new[] { info.Name });
                    File.WriteAllLines(Path.Combine(outDir, "fxmoniker.txt"), new[] { info.Framework.ToString() });

                    return 0;
                });
            });
            
            return app.Execute(args);
        }

        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}
