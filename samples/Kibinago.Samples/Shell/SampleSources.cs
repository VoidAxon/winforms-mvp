using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// The sample's own .cs files, embedded at build time (Kibinago.Samples.csproj: LogicalName
    /// <c>Source/{path}</c>) so the コード tab shows the code that was compiled.
    /// </summary>
    public static class SampleSources
    {
        private const string Prefix = "Source/";

        // Normalized path ("ToDoDemo/ToDoDemoPresenter.cs") → manifest resource name (MSBuild's RecursiveDir
        // uses the build machine's separator).
        private static readonly Lazy<Dictionary<string, string>> Names = new Lazy<Dictionary<string, string>>(() =>
            typeof(SampleSources).Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(n => Normalize(n.Substring(Prefix.Length)), n => n, StringComparer.OrdinalIgnoreCase));

        /// <summary>Whether <paramref name="path"/> (relative to the sample project) was embedded.</summary>
        public static bool Exists(string path) => Names.Value.ContainsKey(Normalize(path));

        /// <summary>The text of an embedded source file, or <c>null</c> when it was not embedded.</summary>
        public static string Read(string path)
        {
            if (!Names.Value.TryGetValue(Normalize(path), out var name))
                return null;
            using (var stream = typeof(SampleSources).Assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
