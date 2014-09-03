using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            var optionANIs = app.Option("--ani", "Forces ANI assemblies to be listed in anis.txt and not in references.txt", CommandOptionType.NoValue);
            var optionFx = app.Option("--framework <FRAMEWORK>", "The framework to target (overrides the default of Asp.Net 5.0)", CommandOptionType.SingleValue);
            var optionOut = app.Option("-o|--out <OUT_DIR>", "Output directory", CommandOptionType.SingleValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            app.OnExecute(() =>
            {
                var hostOptions = new DefaultHostOptions();
                hostOptions.WatchFiles = false;
                hostOptions.PackageDirectory = optionPackages.Value();

                if (optionFx.HasValue())
                {
                    hostOptions.TargetFramework = Project.ParseFrameworkName(optionFx.Value());
                }
                else
                {
                    hostOptions.TargetFramework = _environment.TargetFramework;
                }

                hostOptions.Configuration = optionConfiguration.Value() ?? _environment.Configuration ?? "Debug";
                hostOptions.ApplicationBaseDirectory = _environment.ApplicationBasePath;

                var host = new DefaultHost(hostOptions, _serviceProvider);
                if (host.Project == null)
                    return -1;

                if (string.IsNullOrEmpty(host.Project.Name))
                {
                    hostOptions.ApplicationName = Path.GetFileName(hostOptions.ApplicationBaseDirectory);
                }
                else
                {
                    hostOptions.ApplicationName = host.Project.Name;
                }

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


                using (host.AddLoaders(_container))
                {
                    host.Initialize();
                    var libraryManager = (ILibraryManager)host.ServiceProvider.GetService(typeof(ILibraryManager));
                    var sources = host.Project.SourceFiles;
                    var deps = libraryManager.GetLibraryInformation(hostOptions.ApplicationName).Dependencies.Select(d => new {
                        References = libraryManager.GetAllExports(d).MetadataReferences,
                        Sources = libraryManager.GetLibraryExport(d).SourceReferences,
                        Name = d });

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

                    foreach (var source in sources)
                    {
                        var original = new FileInfo(source);
                        var relative = PathUtil.GetRelativePath(hostOptions.ApplicationBaseDirectory, original.FullName);

                        var newPath = Path.Combine(srcPath, relative);
                        var pathDir = Path.GetDirectoryName(newPath);
                        if (!Directory.Exists(pathDir))
                            Directory.CreateDirectory(pathDir);

                        var newFile = original.CopyTo(newPath);
                        srcPaths.Add(newFile.FullName);
                    }

                    foreach (var dep in deps)
                    {
                        foreach(var source in dep.Sources)
                        {
                            var file = Path.Combine(extSrcPath, dep.Name, Path.GetFileName(source.Name));
                            var dir = Path.GetDirectoryName(file);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            File.Copy(source.Name, file);
                            srcPaths.Add(file);
                        }

                        foreach (var mref in dep.References)
                        {
                            if (optionANIs.HasValue() && (mref is IMetadataEmbeddedReference))
                            {
                            	aniPaths.Add(Unpack(mref, libPath));
                            }
                            else
                            {
                                libPaths.Add(Unpack(mref, libPath));
                            }
                        }
                    }

                    File.WriteAllLines(Path.Combine(outDir, "sources.txt"), srcPaths);
                    File.WriteAllLines(Path.Combine(outDir, "references.txt"), libPaths.Distinct());
                    
                    if (optionANIs.HasValue())
                    {
                    	File.WriteAllLines(Path.Combine(outDir, "anis.txt"), aniPaths.Distinct());
                    }
                    
                    var fxId = string.Empty;
                    var fxVer = hostOptions.TargetFramework.Version;

                    if (hostOptions.TargetFramework.Identifier == ".NETFramework")
                    {
                        fxId = "NET";
		        	}
                    else if (hostOptions.TargetFramework.Identifier == "Asp.Net")
                    {
                        fxId = "ASPNET";
                    }
                    else if (hostOptions.TargetFramework.Identifier == "Asp.NetCore")
                    {
                        fxId = "ASPNETCORE";
                    }
                    else if (hostOptions.TargetFramework.Identifier == ".NETPortable")
                    {
                        fxId = "PORTABLE";
                    }
                    else if (hostOptions.TargetFramework.Identifier == ".NETCore")
                    {
                        fxId = "NETCORE";
                    }
                    else if (hostOptions.TargetFramework.Identifier == "WindowsPhone")
                    {
                        fxId = "WP";
                    }
                    else if (hostOptions.TargetFramework.Identifier == "MonoTouch")
                    {
                        fxId = "IOS";
                    }
                    else if (hostOptions.TargetFramework.Identifier == "MonoAndroid")
                    {
                        fxId = "ANDROID";
                    }
                    else if (hostOptions.TargetFramework.Identifier == "Silverlight")
                    {
                        fxId = "SL";
                    }
                    else
                    {
                        fxId = hostOptions.TargetFramework.Identifier;
                    }
	        	    
                    fxDefs.Add(string.Format("{0}{1}{2}", new object[] {fxId, fxVer.Major.ToString(), fxVer.Minor.ToString()}));
                    if (fxVer.Build > 0) {
                        fxDefs.Add(string.Format("{0}{1}{2}{3}", new object[] {fxId, fxVer.Major.ToString(), fxVer.Minor.ToString(), fxVer.Build.ToString()}));
                    }

                    File.WriteAllLines(Path.Combine(outDir, "fxdefines.txt"), fxDefs);
                    File.WriteAllLines(Path.Combine(outDir, "version.txt"), new[] { host.Project.Version.Version.ToString() });
                    File.WriteAllLines(Path.Combine(outDir, "name.txt"), new[] { host.Project.Name });
                    File.WriteAllLines(Path.Combine(outDir, "fxmoniker.txt"), new[] { hostOptions.TargetFramework.ToString() });
                }

                return 0;
            });
            return app.Execute(args);
        }

        private static string Unpack(IMetadataReference reference, string dir)
        {
            var newPath = Path.Combine(dir, reference.Name + ".dll");

            var fileRef = reference as IMetadataFileReference;
            if (fileRef != null)
            {
                if (Path.GetExtension(fileRef.Name).ToLowerInvariant() == ".exe")
                    newPath = Path.ChangeExtension(newPath, ".exe");

                if (!File.Exists(newPath))
                {
                    File.Copy(fileRef.Path, newPath);
                    var fileName = Path.GetFileNameWithoutExtension(fileRef.Path);
                    foreach(var file in Directory.EnumerateFiles(Path.GetDirectoryName(fileRef.Path), fileName + ".*"))
                    {
                        if(Path.GetFileNameWithoutExtension(file) == fileName)
                        {
                            var ext = Path.GetExtension(file);
                            var newFilePath = Path.Combine(dir, reference.Name + ext);
                            if (!File.Exists(newFilePath))
                                File.Copy(file, newFilePath);
                        }
                    }
                }
                return newPath;
            }

            if (File.Exists(newPath))
                return newPath;

            var eRef = reference as IMetadataEmbeddedReference;
            if (eRef != null)
            {
                File.WriteAllBytes(newPath, eRef.Contents);
                return newPath;
            }

            var projRef = reference as IMetadataProjectReference;
            if (projRef != null)
            {
                using (var fs = File.Create(newPath))
                {
                    projRef.EmitReferenceAssembly(fs);
                }
                return newPath;
            }

            throw new ArgumentException(string.Format("Invalid reference type {0}", reference.GetType()));
        }

        private static void ThrowEntryPointNotfoundException(
            DefaultHost host,
            string applicationName,
            Exception innerException)
        {

            var compilationException = innerException as CompilationException;

            if (compilationException != null)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, compilationException.Errors));
            }

            throw new InvalidOperationException(
                    string.Format("Unable to load application '{0}'.",
                    applicationName), innerException);
        }

        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}
