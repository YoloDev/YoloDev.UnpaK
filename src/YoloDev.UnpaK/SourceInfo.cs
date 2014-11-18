using System;

namespace YoloDev.UnpaK
{
    internal class SourceInfo
    {
        readonly string _path;
        readonly string _lib;
        readonly SourceKind _kind;

        private SourceInfo(string relative)
        {
            _path = relative;
            _kind = SourceKind.Src;
        }

        private SourceInfo(string path, string lib)
        {
            _path = path;
            _lib = lib;
            _kind = SourceKind.Lib;
        }

        public string Path => _path;
        public SourceKind Kind => _kind;

        public string Lib
        {
            get
            {
                if (_kind == SourceKind.Src)
                    throw new InvalidOperationException();

                return _lib;
            }
        }

        internal static SourceInfo FromSrc(string relative) => new SourceInfo(relative);
        internal static SourceInfo FromLib(string lib, string path) => new SourceInfo(path, lib);

        internal enum SourceKind
        {
            Src,
            Lib
        }
    }
}