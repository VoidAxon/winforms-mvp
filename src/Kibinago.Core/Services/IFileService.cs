using System.Text;

namespace Kibinago.Services
{
    /// <summary>
    /// Provides file system operations abstraction for testability.
    /// </summary>
    public interface IFileService
    {
        // File existence and info
        /// <summary>Whether the file exists.</summary>
        /// <param name="path">The file path.</param>
        bool Exists(string path);
        /// <summary>The size of the file, in bytes.</summary>
        /// <param name="path">The file path.</param>
        long GetFileSize(string path);

        // File operations
        /// <summary>Deletes the file.</summary>
        /// <param name="path">The file path.</param>
        void Delete(string path);
        /// <summary>Copies a file.</summary>
        /// <param name="source">The file to copy.</param>
        /// <param name="destination">The path of the copy.</param>
        /// <param name="overwrite"><c>true</c> to replace an existing destination file.</param>
        void Copy(string source, string destination, bool overwrite = false);
        /// <summary>Moves (or renames) a file.</summary>
        /// <param name="source">The file to move.</param>
        /// <param name="destination">The new path.</param>
        void Move(string source, string destination);

        // Read operations
        /// <summary>Reads the whole file as text, detecting the encoding from a byte order mark (UTF-8 otherwise).</summary>
        /// <param name="path">The file path.</param>
        string ReadAllText(string path);
        /// <summary>Reads the whole file as text in the given encoding.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="encoding">The text encoding.</param>
        string ReadAllText(string path, Encoding encoding);
        /// <summary>Reads the whole file as bytes.</summary>
        /// <param name="path">The file path.</param>
        byte[] ReadAllBytes(string path);
        /// <summary>Reads the file's lines, detecting the encoding as <see cref="ReadAllText(string)"/> does.</summary>
        /// <param name="path">The file path.</param>
        string[] ReadAllLines(string path);
        /// <summary>Reads the file's lines in the given encoding.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="encoding">The text encoding.</param>
        string[] ReadAllLines(string path, Encoding encoding);

        // Write operations
        /// <summary>Creates or overwrites the file with <paramref name="contents"/> (UTF-8).</summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The text to write; <c>null</c> writes nothing.</param>
        void WriteAllText(string path, string contents);
        /// <summary>Creates or overwrites the file with <paramref name="contents"/> in the given encoding.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The text to write; <c>null</c> writes nothing.</param>
        /// <param name="encoding">The text encoding.</param>
        void WriteAllText(string path, string contents, Encoding encoding);
        /// <summary>Creates or overwrites the file with <paramref name="bytes"/>.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="bytes">The bytes to write.</param>
        void WriteAllBytes(string path, byte[] bytes);
        /// <summary>Creates or overwrites the file with <paramref name="lines"/> (UTF-8), one per line.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="lines">The lines to write.</param>
        void WriteAllLines(string path, string[] lines);
        /// <summary>Creates or overwrites the file with <paramref name="lines"/> in the given encoding, one per line.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="lines">The lines to write.</param>
        /// <param name="encoding">The text encoding.</param>
        void WriteAllLines(string path, string[] lines, Encoding encoding);

        // Append operations
        /// <summary>Appends <paramref name="contents"/> to the file (UTF-8), creating it if needed.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The text to append; <c>null</c> appends nothing.</param>
        void AppendAllText(string path, string contents);
        /// <summary>Appends <paramref name="contents"/> to the file in the given encoding, creating it if needed.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The text to append; <c>null</c> appends nothing.</param>
        /// <param name="encoding">The text encoding.</param>
        void AppendAllText(string path, string contents, Encoding encoding);
        /// <summary>Appends <paramref name="lines"/> to the file (UTF-8), one per line, creating it if needed.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="lines">The lines to append.</param>
        void AppendAllLines(string path, string[] lines);

        // Directory operations
        /// <summary>Whether the directory exists.</summary>
        /// <param name="path">The directory path.</param>
        bool DirectoryExists(string path);
        /// <summary>Creates the directory and any missing parents; does nothing if it already exists.</summary>
        /// <param name="path">The directory path.</param>
        void CreateDirectory(string path);
        /// <summary>Deletes the directory.</summary>
        /// <param name="path">The directory path.</param>
        /// <param name="recursive"><c>true</c> to delete its contents too; <c>false</c> requires it to be empty.</param>
        void DeleteDirectory(string path, bool recursive = false);
        /// <summary>The full paths of the files directly in the directory that match <paramref name="searchPattern"/>.</summary>
        /// <param name="path">The directory path.</param>
        /// <param name="searchPattern">A wildcard pattern (<c>*</c>, <c>?</c>) matched against the names.</param>
        string[] GetFiles(string path, string searchPattern = "*");
        /// <summary>The full paths of the subdirectories directly in the directory that match <paramref name="searchPattern"/>.</summary>
        /// <param name="path">The directory path.</param>
        /// <param name="searchPattern">A wildcard pattern (<c>*</c>, <c>?</c>) matched against the names.</param>
        string[] GetDirectories(string path, string searchPattern = "*");

        // Path operations
        /// <summary>The file name and extension of <paramref name="path"/>.</summary>
        /// <param name="path">The path.</param>
        string GetFileName(string path);
        /// <summary>The file name of <paramref name="path"/> without its extension.</summary>
        /// <param name="path">The path.</param>
        string GetFileNameWithoutExtension(string path);
        /// <summary>The directory part of <paramref name="path"/>; <c>null</c> for a root directory.</summary>
        /// <param name="path">The path.</param>
        string GetDirectoryName(string path);
        /// <summary>The extension of <paramref name="path"/>, including the period; empty when there is none.</summary>
        /// <param name="path">The path.</param>
        string GetExtension(string path);
        /// <summary>Combines path segments into one path.</summary>
        /// <param name="paths">The segments; a rooted segment discards the ones before it.</param>
        string Combine(params string[] paths);
        /// <summary>The absolute path for <paramref name="path"/>, resolved against the current directory.</summary>
        /// <param name="path">The path.</param>
        string GetFullPath(string path);
    }
}
