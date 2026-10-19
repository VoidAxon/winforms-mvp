using System.Text;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Provides file system operations abstraction for testability.
    /// </summary>
    public interface IFileService
    {
        // File existence and info
        bool Exists(string path);
        long GetFileSize(string path);

        // File operations
        void Delete(string path);
        void Copy(string source, string destination, bool overwrite = false);
        void Move(string source, string destination);

        // Read operations
        string ReadAllText(string path);
        string ReadAllText(string path, Encoding encoding);
        byte[] ReadAllBytes(string path);
        string[] ReadAllLines(string path);
        string[] ReadAllLines(string path, Encoding encoding);

        // Write operations
        void WriteAllText(string path, string contents);
        void WriteAllText(string path, string contents, Encoding encoding);
        void WriteAllBytes(string path, byte[] bytes);
        void WriteAllLines(string path, string[] lines);
        void WriteAllLines(string path, string[] lines, Encoding encoding);

        // Append operations
        void AppendAllText(string path, string contents);
        void AppendAllText(string path, string contents, Encoding encoding);
        void AppendAllLines(string path, string[] lines);

        // Directory operations
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive = false);
        string[] GetFiles(string path, string searchPattern = "*");
        string[] GetDirectories(string path, string searchPattern = "*");

        // Path operations
        string GetFileName(string path);
        string GetFileNameWithoutExtension(string path);
        string GetDirectoryName(string path);
        string GetExtension(string path);
        string Combine(params string[] paths);
        string GetFullPath(string path);
    }
}
