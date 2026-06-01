using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IFileService for testing.
    /// Uses in-memory storage instead of actual file system.
    /// </summary>
    public class MockFileService : IFileService
    {
        // In-memory "file system"
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _directories = new HashSet<string>();

        #region File Existence and Info

        public bool Exists(string path)
        {
            return _files.ContainsKey(path);
        }

        public long GetFileSize(string path)
        {
            if (!_files.ContainsKey(path))
                throw new FileNotFoundException($"Mock file not found: {path}");
            return _files[path].Length;
        }

        #endregion

        #region File Operations

        public void Delete(string path)
        {
            _files.Remove(path);
        }

        public void Copy(string source, string destination, bool overwrite = false)
        {
            if (!_files.ContainsKey(source))
                throw new FileNotFoundException($"Source file not found: {source}");
            if (_files.ContainsKey(destination) && !overwrite)
                throw new IOException($"Destination file already exists: {destination}");

            _files[destination] = (byte[])_files[source].Clone();
        }

        public void Move(string source, string destination)
        {
            if (!_files.ContainsKey(source))
                throw new FileNotFoundException($"Source file not found: {source}");

            _files[destination] = _files[source];
            _files.Remove(source);
        }

        #endregion

        #region Read Operations

        public string ReadAllText(string path)
        {
            return ReadAllText(path, Encoding.UTF8);
        }

        public string ReadAllText(string path, Encoding encoding)
        {
            if (!_files.ContainsKey(path))
                throw new FileNotFoundException($"Mock file not found: {path}");
            return encoding.GetString(_files[path]);
        }

        public byte[] ReadAllBytes(string path)
        {
            if (!_files.ContainsKey(path))
                throw new FileNotFoundException($"Mock file not found: {path}");
            return (byte[])_files[path].Clone();
        }

        public string[] ReadAllLines(string path)
        {
            return ReadAllLines(path, Encoding.UTF8);
        }

        public string[] ReadAllLines(string path, Encoding encoding)
        {
            var text = ReadAllText(path, encoding);
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        #endregion

        #region Write Operations

        public void WriteAllText(string path, string contents)
        {
            WriteAllText(path, contents, Encoding.UTF8);
        }

        public void WriteAllText(string path, string contents, Encoding encoding)
        {
            _files[path] = encoding.GetBytes(contents ?? string.Empty);
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            _files[path] = (byte[])bytes.Clone();
        }

        public void WriteAllLines(string path, string[] lines)
        {
            WriteAllLines(path, lines, Encoding.UTF8);
        }

        public void WriteAllLines(string path, string[] lines, Encoding encoding)
        {
            var text = string.Join(Environment.NewLine, lines);
            WriteAllText(path, text, encoding);
        }

        #endregion

        #region Append Operations

        public void AppendAllText(string path, string contents)
        {
            AppendAllText(path, contents, Encoding.UTF8);
        }

        public void AppendAllText(string path, string contents, Encoding encoding)
        {
            var existing = Exists(path) ? ReadAllText(path, encoding) : string.Empty;
            WriteAllText(path, existing + (contents ?? string.Empty), encoding);
        }

        public void AppendAllLines(string path, string[] lines)
        {
            var text = string.Join(Environment.NewLine, lines);
            AppendAllText(path, Environment.NewLine + text);
        }

        #endregion

        #region Directory Operations

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(path);
        }

        public void CreateDirectory(string path)
        {
            _directories.Add(path);
        }

        public void DeleteDirectory(string path, bool recursive = false)
        {
            _directories.Remove(path);
            if (recursive)
            {
                // Remove all files in directory
                var filesToRemove = _files.Keys.Where(k => k.StartsWith(path + "\\")).ToList();
                foreach (var file in filesToRemove)
                {
                    _files.Remove(file);
                }
            }
        }

        public string[] GetFiles(string path, string searchPattern = "*")
        {
            var pattern = searchPattern.Replace("*", ".*").Replace("?", ".");
            var regex = new System.Text.RegularExpressions.Regex(pattern);

            return _files.Keys
                .Where(k => k.StartsWith(path) && regex.IsMatch(Path.GetFileName(k)))
                .ToArray();
        }

        public string[] GetDirectories(string path, string searchPattern = "*")
        {
            var pattern = searchPattern.Replace("*", ".*").Replace("?", ".");
            var regex = new System.Text.RegularExpressions.Regex(pattern);

            return _directories
                .Where(d => d.StartsWith(path) && regex.IsMatch(Path.GetFileName(d)))
                .ToArray();
        }

        #endregion

        #region Path Operations

        public string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }

        public string GetFileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path);
        }

        public string GetExtension(string path)
        {
            return Path.GetExtension(path);
        }

        public string Combine(params string[] paths)
        {
            return Path.Combine(paths);
        }

        public string GetFullPath(string path)
        {
            return Path.GetFullPath(path);
        }

        #endregion

        #region Test Helper Methods

        /// <summary>
        /// Helper method for test setup - adds a file to the mock file system
        /// </summary>
        public void AddFile(string path, string contents)
        {
            WriteAllText(path, contents);
        }

        /// <summary>
        /// Clears all files and directories from the mock file system
        /// </summary>
        public void Clear()
        {
            _files.Clear();
            _directories.Clear();
        }

        #endregion
    }
}
