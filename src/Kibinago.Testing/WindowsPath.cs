using System;
using System.Collections.Generic;

namespace Kibinago.Testing
{
    /// <summary>
    /// Windows path rules implemented without <c>System.IO.Path</c> or the process current
    /// directory, so <see cref="InMemoryFileService"/> behaves the same on every test host.
    /// </summary>
    internal static class WindowsPath
    {
        /// <summary>The normalized full path: separators unified, <c>.</c>/<c>..</c> resolved, relative paths combined with <paramref name="baseDirectory"/>.</summary>
        public static string GetFullPath(string path, string baseDirectory)
        {
            var p = path.Replace('/', '\\');
            string root;
            string rest;

            if (p.StartsWith(@"\\", StringComparison.Ordinal))
            {
                var parts = p.Substring(2).Split('\\');
                if (parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0)
                    throw new ArgumentException($"Invalid UNC path: {path}", nameof(path));
                root = @"\\" + parts[0] + @"\" + parts[1];
                rest = string.Join(@"\", parts, 2, parts.Length - 2);
            }
            else if (IsDriveRooted(p) && (p.Length == 2 || p[2] != '\\') && baseDirectory != null)
            {
                // Drive-relative ("C:report.txt", "C:"): the base directory on its own drive, else that drive's root.
                var driveRoot = char.ToUpperInvariant(p[0]) + @":\";
                var onBaseDrive = string.Equals(GetRoot(baseDirectory), driveRoot, StringComparison.OrdinalIgnoreCase);
                return GetFullPath((onBaseDrive ? baseDirectory : driveRoot) + @"\" + p.Substring(2), baseDirectory);
            }
            else if (IsDriveRooted(p))
            {
                root = char.ToUpperInvariant(p[0]) + @":\";
                rest = p.Substring(2);
            }
            else if (p.StartsWith(@"\", StringComparison.Ordinal))
            {
                root = GetRoot(baseDirectory);
                rest = p;
            }
            else
            {
                return GetFullPath(baseDirectory + @"\" + p, baseDirectory);
            }

            var segments = new List<string>();
            foreach (var segment in rest.Split('\\'))
            {
                if (segment.Length == 0 || segment == ".") continue;
                if (segment == "..")
                {
                    if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }

            if (segments.Count == 0) return root;
            return root.EndsWith(@"\", StringComparison.Ordinal)
                ? root + string.Join(@"\", segments)
                : root + @"\" + string.Join(@"\", segments);
        }

        /// <summary>The lookup key of a normalized full path (case-insensitive).</summary>
        public static string Key(string fullPath) => fullPath.ToUpperInvariant();

        /// <summary>The parent of a normalized full path, or null for a root.</summary>
        public static string GetParent(string fullPath)
        {
            if (IsRoot(fullPath)) return null;
            var index = fullPath.LastIndexOf('\\');
            var parent = fullPath.Substring(0, index);
            return IsDriveRooted(parent) && parent.Length == 2 ? parent + @"\" : parent;
        }

        /// <summary>True for <c>C:\</c> and <c>\\server\share</c>.</summary>
        public static bool IsRoot(string fullPath)
        {
            if (fullPath.Length == 3 && IsDriveRooted(fullPath)) return true;
            return fullPath.StartsWith(@"\\", StringComparison.Ordinal) && fullPath.IndexOf('\\', 2) == fullPath.LastIndexOf('\\');
        }

        public static string GetFileName(string path)
        {
            var index = LastSeparator(path);
            return index < 0 ? path : path.Substring(index + 1);
        }

        public static string GetExtension(string path)
        {
            var name = GetFileName(path);
            var dot = name.LastIndexOf('.');
            return dot < 0 || dot == name.Length - 1 ? string.Empty : name.Substring(dot);
        }

        public static string GetFileNameWithoutExtension(string path)
        {
            var name = GetFileName(path);
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(0, dot);
        }

        public static string GetDirectoryName(string path)
        {
            var p = path.Replace('/', '\\');
            if (IsRoot(p) || (p.Length == 2 && IsDriveRooted(p))) return null;
            var index = p.LastIndexOf('\\');
            if (index < 0) return IsDriveRooted(p) ? p.Substring(0, 2) : string.Empty;
            var directory = p.Substring(0, index);
            if (directory.Length == 2 && IsDriveRooted(directory)) return directory + @"\";
            if (directory.Length == 0) return @"\";
            return directory.StartsWith(@"\\", StringComparison.Ordinal) && directory.IndexOf('\\', 2) < 0 ? null : directory;
        }

        public static string Combine(string[] paths)
        {
            var result = string.Empty;
            foreach (var part in paths)
            {
                if (part.Length == 0) continue;
                if (IsRooted(part) || result.Length == 0) { result = part; continue; }
                var last = result[result.Length - 1];
                result += last == '\\' || last == '/' || last == ':' ? part : @"\" + part;
            }
            return result;
        }

        private static bool IsRooted(string path)
            => path.StartsWith(@"\", StringComparison.Ordinal) || path.StartsWith("/", StringComparison.Ordinal)
               || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':');

        private static bool IsDriveRooted(string p) => p.Length >= 2 && char.IsLetter(p[0]) && p[1] == ':';

        private static string GetRoot(string fullPath)
        {
            if (IsDriveRooted(fullPath)) return fullPath.Substring(0, 2).ToUpperInvariant() + @"\";
            var share = fullPath.IndexOf('\\', fullPath.IndexOf('\\', 2) + 1);
            return share < 0 ? fullPath : fullPath.Substring(0, share);
        }

        private static int LastSeparator(string path)
        {
            var index = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            if (index < 0 && IsDriveRooted(path)) index = 1;
            return index;
        }
    }
}
