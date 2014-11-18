using System;

namespace YoloDev.UnpaK
{
    internal class ResourceInfo
    {
        readonly string _name;
        readonly string _file;
        readonly byte[] _content;
        readonly ResourceKind _type;

        private ResourceInfo(string name, string file)
        {
            _name = name;
            _file = file;
            _type = ResourceKind.File;
        }

        private ResourceInfo(string name, byte[] content)
        {
            _name = name;
            _content = content;
            _type = ResourceKind.Content;
        }

        internal static ResourceInfo AssemblyNeutralInterface(string name, byte[] contents)
            => new ResourceInfo(name, contents);

        internal enum ResourceKind
        {
            File,
            Content
        }

    }
}