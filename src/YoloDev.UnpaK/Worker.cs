using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Compilation;

namespace YoloDev.UnpaK
{
    internal class Worker
    {
        public static ProjectInfo Process(
            string packagesDirectory,
            FrameworkName targetFramework,
            string configuration,
            string applicationBaseDirectory,
            IServiceProvider serviceProvider,
            IAssemblyLoaderContainer container)
        {
            var hostOptions = new DefaultHostOptions()
            {
                WatchFiles = false,
                PackageDirectory = packagesDirectory,
                TargetFramework = targetFramework,
                Configuration = configuration,
                ApplicationBaseDirectory = applicationBaseDirectory
            };

            var host = new DefaultHost(hostOptions, serviceProvider);
            if (host.Project == null)
                return null;

            if (string.IsNullOrEmpty(host.Project.Name))
            {
                hostOptions.ApplicationName = Path.GetFileName(hostOptions.ApplicationBaseDirectory);
            }
            else
            {
                hostOptions.ApplicationName = host.Project.Name;
            }

            var project = host.Project;

            using (host.AddLoaders(container))
            {
                host.Initialize();
                var libraryManager = (ILibraryManager)host.ServiceProvider.GetService(typeof(ILibraryManager));
                var sourceFiles = host.Project.Files.SourceFiles;
                var dependencies = libraryManager.GetLibraryInformation(hostOptions.ApplicationName).Dependencies.Select(d => new
                {
                    References = libraryManager.GetAllExports(d).MetadataReferences,
                    Sources = libraryManager.GetLibraryExport(d).SourceReferences,
                    Name = d
                });

                var sources = ImmutableList.CreateBuilder<SourceInfo>();
                var deps = ImmutableList.CreateBuilder<DependencyInfo>();
                var defines = ImmutableList.CreateBuilder<string>();
                var resources = ImmutableList.CreateBuilder<ResourceInfo>();

                var rootOptions = project.GetCompilerOptions();
                var configurationOptions = project.GetCompilerOptions(configuration);
                var targetFrameworkOptions = project.GetCompilerOptions(targetFramework);

                var resultOptions = CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);

                if (resultOptions.Defines != null)
                    defines.AddRange(resultOptions.Defines);

                foreach (var source in sourceFiles)
                {
                    var original = new FileInfo(source);
                    var relative = PathUtil.GetRelativePath(hostOptions.ApplicationBaseDirectory, original.FullName);

                    sources.Add(SourceInfo.FromSrc(relative));
                }

                foreach (var dep in dependencies)
                {
                    foreach (var source in dep.Sources)
                    {
                        sources.Add(SourceInfo.FromLib(dep.Name, source.Name));
                    }

                    foreach (var mref in dep.References)
                    {
                        if(mref is IMetadataEmbeddedReference)
                        {
                            resources.Add(ResourceInfo.AssemblyNeutralInterface(mref.Name, ((IMetadataEmbeddedReference)mref).Contents));
                        }

                        deps.Add(DependencyInfo.Reference(mref));
                    }
                }

                return new ProjectInfo(
                    project.Name,
                    project.Version,
                    project.ProjectDirectory,
                    configuration,
                    hostOptions.TargetFramework,
                    sources.ToImmutable(),
                    deps.ToImmutable(),
                    defines.ToImmutable(),
                    resources.ToImmutable());
            }
        }
    }
}