using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Versioning;
using NuGet;

namespace YoloDev.UnpaK
{
    internal class ProjectInfo
    {
        private readonly string _name;
        private readonly SemanticVersion _version;
        private readonly string _basePath;
        private readonly string _configuration;
        private readonly FrameworkName _framework;
        private readonly ImmutableList<SourceInfo> _sources;
        private readonly ImmutableList<DependencyInfo> _deps;
        private readonly ImmutableList<string> _defines;
        private readonly ImmutableList<ResourceInfo> _resources;

        public ProjectInfo(
            string name,
            SemanticVersion version,
            string basePath,
            string configuration,
            FrameworkName framework,
            ImmutableList<SourceInfo> sources,
            ImmutableList<DependencyInfo> deps,
            ImmutableList<string> defines,
            ImmutableList<ResourceInfo> resources)
        {
            _name = name;
            _version = version;
            _basePath = basePath;
            _configuration = configuration;
            _framework = framework;
            _sources = sources;
            _deps = deps;
            _defines = defines;
            _resources = resources;
        }

        public string Name => _name;

        public SemanticVersion Version => _version;

        public string Base => _basePath;

        public string Configuration => _configuration;

        public FrameworkName Framework => _framework;

        public IEnumerable<SourceInfo> Sources => _sources;

        public IEnumerable<DependencyInfo> Dependencies => _deps;

        public IEnumerable<string> Defines => _defines;

    }
}