using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WinformsMVP.Services;

namespace WinformsMVP.Testing
{
    /// <summary>The kind of operation a <see cref="FileOperationCall"/> describes.</summary>
    public enum FileOperation
    {
        Read,
        Write,
        Append,
        Copy,
        Move,
        Delete,
        CreateDirectory,
        DeleteDirectory,
        Enumerate
    }

    /// <summary>An operation about to run, passed to <see cref="InMemoryFileService.Failure"/>.</summary>
    public sealed class FileOperationCall
    {
        internal FileOperationCall(FileOperation operation, string path, string destination)
        {
            Operation = operation;
            Path = path;
            Destination = destination;
        }

        /// <summary>The operation.</summary>
        public FileOperation Operation { get; }

        /// <summary>The full path (normalized) the operation reads, writes or enumerates; the source for copy/move.</summary>
        public string Path { get; }

        /// <summary>The full destination path for copy/move, otherwise null.</summary>
        public string Destination { get; }

        /// <inheritdoc />
        public override string ToString() => Destination == null ? $"{Operation} {Path}" : $"{Operation} {Path} -> {Destination}";
    }

    /// <summary>
    /// <see cref="IFileService"/> backed by memory, following Windows path rules on every host
    /// (case-insensitive, <c>\</c> and <c>/</c>, <c>.</c>/<c>..</c>; relative paths resolve against
    /// <see cref="BaseDirectory"/>, <c>C:\test</c> by default, which exists from the start).
    /// </summary>
    /// <remarks>
    /// <para>Behaves like <c>System.IO</c> where a presenter can tell: writes, copies and moves need an
    /// existing parent directory (<see cref="DirectoryNotFoundException"/>); only
    /// <see cref="CreateDirectory"/> and the setup helpers create parents; an existing destination
    /// without overwrite is an <see cref="IOException"/>; a missing file is a
    /// <see cref="FileNotFoundException"/>. Content is bytes: text is encoded as the given encoding
    /// (UTF-8 without BOM by default), a BOM is honored on read, lines end with <c>\r\n</c>.
    /// <see cref="GetFiles"/> / <see cref="GetDirectories"/> return full paths of immediate
    /// children (<c>*</c> and <c>?</c> patterns, no promised order).</para>
    /// <para>Out of scope: ACLs, locks and sharing, timestamps, links, device and long paths. Simulate
    /// a failure with <see cref="Failure"/> (e.g. a file locked by Excel).</para>
    /// <code>
    /// fakes.Files.SeedFile(@"C:\data\in.csv", "id,name\r\n1,Alice");
    /// fakes.Files.Failure = c => c.Operation == FileOperation.Write ? new IOException("locked") : null;
    /// Assert.Contains(@"C:\data\out.csv", fakes.Files.FilePaths);
    /// </code>
    /// </remarks>
    public class InMemoryFileService : IFileService
    {
        private const string NewLine = "\r\n";
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false);

        private readonly object _gate = new object();
        private readonly Dictionary<string, StoredFile> _files = new Dictionary<string, StoredFile>();
        private readonly Dictionary<string, string> _directories = new Dictionary<string, string>();

        /// <summary>Creates an empty file system whose base directory exists.</summary>
        /// <param name="baseDirectory">An absolute Windows path relative paths resolve against.</param>
        public InMemoryFileService(string baseDirectory = @"C:\test")
        {
            if (string.IsNullOrEmpty(baseDirectory)) throw new ArgumentNullException(nameof(baseDirectory));
            var normalized = baseDirectory.Replace('/', '\\');
            if (!(normalized.StartsWith(@"\\", StringComparison.Ordinal)
                  || (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '\\')))
                throw new ArgumentException("The base directory must be an absolute Windows path, e.g. C:\\test.", nameof(baseDirectory));
            BaseDirectory = WindowsPath.GetFullPath(normalized, null);
            SeedDirectory(BaseDirectory);
        }

        /// <summary>The directory relative paths resolve against.</summary>
        public string BaseDirectory { get; }

        /// <summary>
        /// Called with every operation before it changes or reads anything; return an exception to
        /// make the operation throw it, or null to let it run.
        /// </summary>
        public Func<FileOperationCall, Exception> Failure { get; set; }

        /// <summary>The full paths of all files (a snapshot, no promised order).</summary>
        public IReadOnlyCollection<string> FilePaths
        {
            get { lock (_gate) return _files.Values.Select(f => f.Path).ToArray(); }
        }

        /// <summary>The full paths of all directories created so far, drive roots excluded (a snapshot).</summary>
        public IReadOnlyCollection<string> DirectoryPaths
        {
            get { lock (_gate) return _directories.Values.ToArray(); }
        }

        #region Setup

        /// <summary>Creates or replaces a text file (UTF-8 without BOM unless <paramref name="encoding"/> is given), creating its parents.</summary>
        public void SeedFile(string path, string text, Encoding encoding = null)
            => SeedFile(path, Encode(text ?? string.Empty, encoding ?? DefaultEncoding));

        /// <summary>Creates or replaces a file with a copy of <paramref name="bytes"/>, creating its parents.</summary>
        public void SeedFile(string path, byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var full = Full(path, nameof(path));
            lock (_gate)
            {
                CreateDirectoryCore(WindowsPath.GetParent(full));
                PutFile(full, (byte[])bytes.Clone());
            }
        }

        /// <summary>Creates a directory and its parents.</summary>
        public void SeedDirectory(string path)
        {
            var full = Full(path, nameof(path));
            lock (_gate) CreateDirectoryCore(full);
        }

        #endregion

        #region IFileService — files

        /// <inheritdoc />
        public virtual bool Exists(string path)
        {
            var full = Full(path, nameof(path));
            lock (_gate) return _files.ContainsKey(WindowsPath.Key(full));
        }

        /// <inheritdoc />
        public virtual long GetFileSize(string path)
        {
            var full = Full(path, nameof(path));
            lock (_gate) return ExistingFile(full).Content.LongLength;
        }

        /// <inheritdoc />
        public virtual void Delete(string path)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.Delete, full);
            lock (_gate)
            {
                RequireParent(full);
                if (IsDirectory(full)) throw new UnauthorizedAccessException($"Access to the path '{full}' is denied.");
                _files.Remove(WindowsPath.Key(full));
            }
        }

        /// <inheritdoc />
        public virtual void Copy(string source, string destination, bool overwrite = false)
        {
            var from = Full(source, nameof(source));
            var to = Full(destination, nameof(destination));
            Fail(FileOperation.Copy, from, to);
            lock (_gate)
            {
                var file = ExistingFile(from);
                RequireWritableTarget(to);
                if (WindowsPath.Key(from) == WindowsPath.Key(to))
                    throw new IOException($"The process cannot access the file '{to}' because it is being used by another process.");
                if (!overwrite && _files.ContainsKey(WindowsPath.Key(to)))
                    throw new IOException($"The file '{to}' already exists.");
                PutFile(to, (byte[])file.Content.Clone());
            }
        }

        /// <inheritdoc />
        public virtual void Move(string source, string destination)
        {
            var from = Full(source, nameof(source));
            var to = Full(destination, nameof(destination));
            Fail(FileOperation.Move, from, to);
            lock (_gate)
            {
                var file = ExistingFile(from);
                RequireWritableTarget(to);
                var sameFile = WindowsPath.Key(from) == WindowsPath.Key(to);
                if (!sameFile && _files.ContainsKey(WindowsPath.Key(to)))
                    throw new IOException($"Cannot create '{to}' because a file with that name already exists.");
                _files.Remove(WindowsPath.Key(from));
                PutFile(to, file.Content);
            }
        }

        #endregion

        #region IFileService — read

        /// <inheritdoc />
        public virtual string ReadAllText(string path) => Decode(Read(path), DefaultEncoding);

        /// <inheritdoc />
        public virtual string ReadAllText(string path, Encoding encoding)
        {
            RequireEncoding(encoding);
            return Decode(Read(path), encoding);
        }

        /// <inheritdoc />
        public virtual byte[] ReadAllBytes(string path) => (byte[])Read(path).Clone();

        /// <inheritdoc />
        public virtual string[] ReadAllLines(string path) => SplitLines(Read(path), DefaultEncoding);

        /// <inheritdoc />
        public virtual string[] ReadAllLines(string path, Encoding encoding)
        {
            RequireEncoding(encoding);
            return SplitLines(Read(path), encoding);
        }

        #endregion

        #region IFileService — write and append

        /// <inheritdoc />
        public virtual void WriteAllText(string path, string contents) => Write(path, Encode(contents ?? string.Empty, DefaultEncoding));

        /// <inheritdoc />
        public virtual void WriteAllText(string path, string contents, Encoding encoding)
        {
            RequirePath(path, nameof(path));
            RequireEncoding(encoding);
            Write(path, Encode(contents ?? string.Empty, encoding));
        }

        /// <inheritdoc />
        public virtual void WriteAllBytes(string path, byte[] bytes)
        {
            RequirePath(path, nameof(path));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            Write(path, (byte[])bytes.Clone());
        }

        /// <inheritdoc />
        public virtual void WriteAllLines(string path, string[] lines)
        {
            RequirePath(path, nameof(path));
            RequireLines(lines);
            Write(path, Encode(JoinLines(lines), DefaultEncoding));
        }

        /// <inheritdoc />
        public virtual void WriteAllLines(string path, string[] lines, Encoding encoding)
        {
            RequirePath(path, nameof(path));
            RequireLines(lines);
            RequireEncoding(encoding);
            Write(path, Encode(JoinLines(lines), encoding));
        }

        /// <inheritdoc />
        public virtual void AppendAllText(string path, string contents) => Append(path, contents ?? string.Empty, DefaultEncoding);

        /// <inheritdoc />
        public virtual void AppendAllText(string path, string contents, Encoding encoding)
        {
            RequirePath(path, nameof(path));
            RequireEncoding(encoding);
            Append(path, contents ?? string.Empty, encoding);
        }

        /// <inheritdoc />
        public virtual void AppendAllLines(string path, string[] lines)
        {
            RequirePath(path, nameof(path));
            RequireLines(lines);
            Append(path, JoinLines(lines), DefaultEncoding);
        }

        #endregion

        #region IFileService — directories

        /// <inheritdoc />
        public virtual bool DirectoryExists(string path)
        {
            var full = Full(path, nameof(path));
            lock (_gate) return IsDirectory(full);
        }

        /// <inheritdoc />
        public virtual void CreateDirectory(string path)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.CreateDirectory, full);
            lock (_gate) CreateDirectoryCore(full);
        }

        /// <inheritdoc />
        public virtual void DeleteDirectory(string path, bool recursive = false)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.DeleteDirectory, full);
            lock (_gate)
            {
                if (!_directories.ContainsKey(WindowsPath.Key(full)))
                    throw new DirectoryNotFoundException($"Could not find a part of the path '{full}'.");
                var prefix = WindowsPath.Key(full) + @"\";
                var fileKeys = _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                var dirKeys = _directories.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                if (!recursive && (fileKeys.Count > 0 || dirKeys.Count > 0))
                    throw new IOException($"The directory is not empty: '{full}'.");
                foreach (var key in fileKeys) _files.Remove(key);
                foreach (var key in dirKeys) _directories.Remove(key);
                _directories.Remove(WindowsPath.Key(full));
            }
        }

        /// <inheritdoc />
        public virtual string[] GetFiles(string path, string searchPattern = "*")
            => Enumerate(path, searchPattern, () => _files.Values.Select(f => f.Path));

        /// <inheritdoc />
        public virtual string[] GetDirectories(string path, string searchPattern = "*")
            => Enumerate(path, searchPattern, () => _directories.Values);

        #endregion

        #region IFileService — path helpers (Windows rules on every host)

        /// <inheritdoc />
        public virtual string GetFileName(string path) => WindowsPath.GetFileName(RequirePath(path, nameof(path)));

        /// <inheritdoc />
        public virtual string GetFileNameWithoutExtension(string path) => WindowsPath.GetFileNameWithoutExtension(RequirePath(path, nameof(path)));

        /// <inheritdoc />
        public virtual string GetDirectoryName(string path) => WindowsPath.GetDirectoryName(RequirePath(path, nameof(path)));

        /// <inheritdoc />
        public virtual string GetExtension(string path) => WindowsPath.GetExtension(RequirePath(path, nameof(path)));

        /// <inheritdoc />
        public virtual string Combine(params string[] paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (paths.Any(p => p == null)) throw new ArgumentNullException(nameof(paths));
            return WindowsPath.Combine(paths);
        }

        /// <inheritdoc />
        public virtual string GetFullPath(string path) => Full(path, nameof(path));

        #endregion

        #region Internals

        private byte[] Read(string path)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.Read, full);
            lock (_gate) return ExistingFile(full).Content;
        }

        private void Write(string path, byte[] content)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.Write, full);
            lock (_gate)
            {
                RequireWritableTarget(full);
                PutFile(full, content);
            }
        }

        private void Append(string path, string text, Encoding encoding)
        {
            var full = Full(path, nameof(path));
            Fail(FileOperation.Append, full);
            lock (_gate)
            {
                RequireWritableTarget(full);
                _files.TryGetValue(WindowsPath.Key(full), out var existing);
                // Like File.AppendAllText: the encoding's preamble only starts an empty file.
                var added = existing == null || existing.Content.Length == 0
                    ? Encode(text, encoding)
                    : encoding.GetBytes(text);
                var content = existing == null ? added : existing.Content.Concat(added).ToArray();
                PutFile(existing?.Path ?? full, content);
            }
        }

        private string[] Enumerate(string path, string searchPattern, Func<IEnumerable<string>> candidates)
        {
            var full = Full(path, nameof(path));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));
            Fail(FileOperation.Enumerate, full);
            var pattern = WildcardPattern(searchPattern);
            lock (_gate)
            {
                if (!IsDirectory(full))
                    throw new DirectoryNotFoundException($"Could not find a part of the path '{full}'.");
                var parentKey = WindowsPath.Key(full);
                return candidates()
                    .Where(p => WindowsPath.Key(WindowsPath.GetParent(p)) == parentKey)
                    .Where(p => pattern.IsMatch(WindowsPath.GetFileName(p)))
                    .ToArray();
            }
        }

        // Windows wildcards: * and ? as usual, and a trailing ".*" also matches a name without an
        // extension ("*.*" lists everything, as Directory.GetFiles does on Windows).
        private static Regex WildcardPattern(string searchPattern)
        {
            var anyExtension = searchPattern.EndsWith(".*", StringComparison.Ordinal);
            var stem = anyExtension ? searchPattern.Substring(0, searchPattern.Length - 2) : searchPattern;
            var body = Regex.Escape(stem).Replace(@"\*", ".*").Replace(@"\?", ".");
            return new Regex("^" + body + (anyExtension ? @"(\..*)?" : string.Empty) + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private void CreateDirectoryCore(string full)
        {
            if (full == null || WindowsPath.IsRoot(full)) return;
            if (_files.ContainsKey(WindowsPath.Key(full)))
                throw new IOException($"Cannot create '{full}' because a file with that name already exists.");
            CreateDirectoryCore(WindowsPath.GetParent(full));
            var key = WindowsPath.Key(full);
            if (!_directories.ContainsKey(key)) _directories[key] = full;
        }

        private void PutFile(string full, byte[] content)
        {
            var key = WindowsPath.Key(full);
            // Keep the casing the file was created with, as Windows does when it is overwritten.
            var display = _files.TryGetValue(key, out var existing) ? existing.Path : full;
            _files[key] = new StoredFile(display, content);
        }

        private StoredFile ExistingFile(string full)
        {
            if (_files.TryGetValue(WindowsPath.Key(full), out var file)) return file;
            throw new FileNotFoundException($"Could not find file '{full}'.", full);
        }

        private void RequireParent(string full)
        {
            var parent = WindowsPath.GetParent(full);
            if (parent != null && !IsDirectory(parent))
                throw new DirectoryNotFoundException($"Could not find a part of the path '{full}'.");
        }

        private void RequireWritableTarget(string full)
        {
            RequireParent(full);
            if (IsDirectory(full)) throw new UnauthorizedAccessException($"Access to the path '{full}' is denied.");
        }

        private bool IsDirectory(string full) => WindowsPath.IsRoot(full) || _directories.ContainsKey(WindowsPath.Key(full));

        private void Fail(FileOperation operation, string path, string destination = null)
        {
            var failure = Failure?.Invoke(new FileOperationCall(operation, path, destination));
            if (failure != null) throw failure;
        }

        private string Full(string path, string parameterName)
            => WindowsPath.GetFullPath(RequirePath(path, parameterName), BaseDirectory);

        private static string RequirePath(string path, string parameterName)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(parameterName);
            return path;
        }

        private static void RequireEncoding(Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
        }

        private static void RequireLines(string[] lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
        }

        private static string JoinLines(string[] lines)
        {
            var builder = new StringBuilder();
            foreach (var line in lines) builder.Append(line).Append(NewLine);
            return builder.ToString();
        }

        private static byte[] Encode(string text, Encoding encoding)
        {
            var preamble = encoding.GetPreamble();
            var body = encoding.GetBytes(text);
            if (preamble.Length == 0) return body;
            var bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            return bytes;
        }

        private static string Decode(byte[] content, Encoding encoding)
        {
            using (var reader = new StreamReader(new MemoryStream(content), encoding, detectEncodingFromByteOrderMarks: true))
                return reader.ReadToEnd();
        }

        private static string[] SplitLines(byte[] content, Encoding encoding)
        {
            var lines = new List<string>();
            using (var reader = new StreamReader(new MemoryStream(content), encoding, detectEncodingFromByteOrderMarks: true))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines.ToArray();
        }

        private sealed class StoredFile
        {
            public StoredFile(string path, byte[] content)
            {
                Path = path;
                Content = content;
            }

            public string Path { get; }
            public byte[] Content { get; }
        }

        #endregion
    }
}
