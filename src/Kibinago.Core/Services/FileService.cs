using System;
using System.IO;
using System.Text;

namespace Kibinago.Services
{
    /// <summary>
    /// Default implementation of IFileService that wraps System.IO operations.
    /// </summary>
    public class FileService : IFileService
    {
        #region File Existence and Info

        /// <inheritdoc />
        public bool Exists(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return File.Exists(path);
        }

        /// <inheritdoc />
        public long GetFileSize(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }

        #endregion

        #region File Operations

        /// <inheritdoc />
        public void Delete(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            File.Delete(path);
        }

        /// <inheritdoc />
        public void Copy(string source, string destination, bool overwrite = false)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(destination))
                throw new ArgumentNullException(nameof(destination));

            File.Copy(source, destination, overwrite);
        }

        /// <inheritdoc />
        public void Move(string source, string destination)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(destination))
                throw new ArgumentNullException(nameof(destination));

            File.Move(source, destination);
        }

        #endregion

        #region Read Operations

        /// <inheritdoc />
        public string ReadAllText(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return File.ReadAllText(path);
        }

        /// <inheritdoc />
        public string ReadAllText(string path, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return File.ReadAllText(path, encoding);
        }

        /// <inheritdoc />
        public byte[] ReadAllBytes(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return File.ReadAllBytes(path);
        }

        /// <inheritdoc />
        public string[] ReadAllLines(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return File.ReadAllLines(path);
        }

        /// <inheritdoc />
        public string[] ReadAllLines(string path, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return File.ReadAllLines(path, encoding);
        }

        #endregion

        #region Write Operations

        /// <inheritdoc />
        public void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            File.WriteAllText(path, contents ?? string.Empty);
        }

        /// <inheritdoc />
        public void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            File.WriteAllText(path, contents ?? string.Empty, encoding);
        }

        /// <inheritdoc />
        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            File.WriteAllBytes(path, bytes);
        }

        /// <inheritdoc />
        public void WriteAllLines(string path, string[] lines)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            File.WriteAllLines(path, lines);
        }

        /// <inheritdoc />
        public void WriteAllLines(string path, string[] lines, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            File.WriteAllLines(path, lines, encoding);
        }

        #endregion

        #region Append Operations

        /// <inheritdoc />
        public void AppendAllText(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            File.AppendAllText(path, contents ?? string.Empty);
        }

        /// <inheritdoc />
        public void AppendAllText(string path, string contents, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            File.AppendAllText(path, contents ?? string.Empty, encoding);
        }

        /// <inheritdoc />
        public void AppendAllLines(string path, string[] lines)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            File.AppendAllLines(path, lines);
        }

        #endregion

        #region Directory Operations

        /// <inheritdoc />
        public bool DirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Directory.Exists(path);
        }

        /// <inheritdoc />
        public void CreateDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            Directory.CreateDirectory(path);
        }

        /// <inheritdoc />
        public void DeleteDirectory(string path, bool recursive = false)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            Directory.Delete(path, recursive);
        }

        /// <inheritdoc />
        public string[] GetFiles(string path, string searchPattern = "*")
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Directory.GetFiles(path, searchPattern);
        }

        /// <inheritdoc />
        public string[] GetDirectories(string path, string searchPattern = "*")
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Directory.GetDirectories(path, searchPattern);
        }

        #endregion

        #region Path Operations

        /// <inheritdoc />
        public string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Path.GetFileName(path);
        }

        /// <inheritdoc />
        public string GetFileNameWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Path.GetFileNameWithoutExtension(path);
        }

        /// <inheritdoc />
        public string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Path.GetDirectoryName(path);
        }

        /// <inheritdoc />
        public string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Path.GetExtension(path);
        }

        /// <inheritdoc />
        public string Combine(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            return Path.Combine(paths);
        }

        /// <inheritdoc />
        public string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return Path.GetFullPath(path);
        }

        #endregion
    }
}
