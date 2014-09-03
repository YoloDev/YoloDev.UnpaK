using System;

namespace YoloDev.UnpaK
{
    /// <summary>
    /// Summary description for PathUtil
    /// </summary>
    internal static class PathUtil
    {
        private static string NormalizeFilepath(string filepath)
        {
            string result = System.IO.Path.GetFullPath(filepath);

            result = result.TrimEnd(new[] { System.IO.Path.DirectorySeparatorChar });

            return result;
        }

        public static string GetRelativePath(string rootPath, string fullPath)
        {
            rootPath = NormalizeFilepath(rootPath);
            fullPath = NormalizeFilepath(fullPath);

            if (!fullPath.StartsWith(rootPath))
                return null;

            return "." + fullPath.Substring(rootPath.Length);
        }
    }
}
