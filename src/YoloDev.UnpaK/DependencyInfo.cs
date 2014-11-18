using System;
using System.IO;
using System.IO.Path;
using Microsoft.Framework.Runtime;

namespace YoloDev.UnpaK
{
    internal class DependencyInfo
    {
        readonly IMetadataReference _ref;

        private DependencyInfo(IMetadataReference mref)
        {
            _ref = mref;
        }
        internal static DependencyInfo Reference(IMetadataReference mref) => new DependencyInfo(mref);

        public string Name => _ref.Name;

        public bool IsANI => _ref is IMetadataEmbeddedReference;

        public bool IsFile => _ref is IMetadataFileReference;

        public string Path
        {
            get
            {
                var fref = _ref as IMetadataFileReference;
                if (fref == null)
                    throw new InvalidOperationException();

                return fref.Path;
            }
        }

        public void CopyTo(string path)
        {
            var fileRef = _ref as IMetadataFileReference;
            if (fileRef != null)
            {
                if (GetExtension(fileRef.Name).ToLowerInvariant() == ".exe")
                    path = ChangeExtension(path, ".exe");

                if (!File.Exists(path))
                {
                    var dir = GetDirectoryName(path);
                    File.Copy(fileRef.Path, path);
                    var fileName = GetFileNameWithoutExtension(fileRef.Path);
                    foreach (var file in Directory.EnumerateFiles(GetDirectoryName(fileRef.Path), fileName + ".*"))
                    {
                        if (GetFileNameWithoutExtension(file) == fileName)
                        {
                            var ext = GetExtension(file);
                            var newFilePath = Combine(dir, _ref.Name + ext);
                            if (!File.Exists(newFilePath))
                                File.Copy(file, newFilePath);
                        }
                    }
                }
                return;
            }

            if (File.Exists(path))
                return;

            var eRef = _ref as IMetadataEmbeddedReference;
            if (eRef != null)
            {
                File.WriteAllBytes(path, eRef.Contents);
                return;
            }

            var projRef = _ref as IMetadataProjectReference;
            if (projRef != null)
            {
                using (var fs = File.Create(path))
                {
                    projRef.EmitReferenceAssembly(fs);
                }
                return;
            }

            throw new ArgumentException(string.Format("Invalid reference type {0}", _ref.GetType()));
        }
    }
}