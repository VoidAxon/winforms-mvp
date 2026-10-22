using System;
using System.IO;
using System.Linq;
using System.Text;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Testing
{
    public class InMemoryFileServiceTests
    {
        [Fact]
        public void PathsAreCaseAndSeparatorInsensitive_OnAnyHost()
        {
            var files = new InMemoryFileService();
            files.SeedFile(@"C:\a\B.txt", "x");

            Assert.True(files.Exists(@"c:/A/b.txt"));
            Assert.True(files.Exists(@"C:\a\.\sub\..\b.TXT"));
            Assert.True(files.DirectoryExists("c:/a/"));
            Assert.Equal(@"C:\a\B.txt", Assert.Single(files.FilePaths));
        }

        [Fact]
        public void RelativePaths_ResolveAgainstTheBaseDirectory_WhichExists()
        {
            var files = new InMemoryFileService();
            Assert.True(files.DirectoryExists(@"C:\test"));

            files.WriteAllText("out.csv", "1");

            Assert.True(files.Exists(@"C:\test\out.csv"));
            Assert.Equal(@"C:\test\out.csv", files.GetFullPath("out.csv"));
            Assert.Equal(@"D:\work\x", new InMemoryFileService(@"D:\work").GetFullPath(@"sub\..\x"));
        }

        [Fact]
        public void Write_WithoutParentDirectory_ThrowsDirectoryNotFound()
        {
            var files = new InMemoryFileService();
            Assert.Throws<DirectoryNotFoundException>(() => files.WriteAllText(@"C:\missing\a.txt", "x"));
            Assert.Throws<DirectoryNotFoundException>(() => files.AppendAllText(@"C:\missing\a.txt", "x"));
            Assert.Empty(files.FilePaths);
        }

        [Fact]
        public void SeedFile_And_CreateDirectory_CreateParents()
        {
            var files = new InMemoryFileService();
            files.SeedFile(@"C:\a\b\c.txt", new byte[] { 1 });
            files.CreateDirectory(@"D:\x\y");

            Assert.True(files.DirectoryExists(@"C:\a\b"));
            Assert.True(files.DirectoryExists(@"D:\x"));
            Assert.Contains(@"D:\x\y", files.DirectoryPaths);
        }

        [Fact]
        public void ReadAllText_RoundTripsWithEncoding_AndHonorsBom()
        {
            var files = new InMemoryFileService();
            var sjisLike = Encoding.Unicode;   // a BOM-writing encoding available on every runtime

            files.WriteAllText("u.txt", "あいう", sjisLike);
            files.WriteAllText("plain.txt", "あいう");

            Assert.Equal(new byte[] { 0xFF, 0xFE }, files.ReadAllBytes("u.txt").Take(2).ToArray());
            Assert.Equal("あいう", files.ReadAllText("u.txt"));            // BOM detected
            Assert.Equal("あいう", files.ReadAllText("u.txt", Encoding.UTF8));
            Assert.Equal(Encoding.UTF8.GetBytes("あいう"), files.ReadAllBytes("plain.txt"));   // no BOM by default
            Assert.Equal(3 * 2 + 2, files.GetFileSize("u.txt"));
        }

        [Fact]
        public void AppendAllText_AppendsBytes_AndCreatesTheFile()
        {
            var files = new InMemoryFileService();
            files.AppendAllText("log.txt", "a");
            files.AppendAllText("log.txt", "b");
            files.AppendAllLines("log.txt", new[] { "c" });

            Assert.Equal("abc\r\n", files.ReadAllText("log.txt"));
        }

        [Fact]
        public void ReadAllLines_SplitsLikeFile()
        {
            var files = new InMemoryFileService();
            files.WriteAllText("l.txt", "a\r\nb\nc\r\n");
            files.WriteAllLines("w.txt", new[] { "x", "y" });

            Assert.Equal(new[] { "a", "b", "c" }, files.ReadAllLines("l.txt"));
            Assert.Equal("x\r\ny\r\n", files.ReadAllText("w.txt"));
        }

        [Fact]
        public void BytesAreCopiedInAndOut()
        {
            var files = new InMemoryFileService();
            var bytes = new byte[] { 1, 2 };
            files.WriteAllBytes("b.bin", bytes);
            bytes[0] = 9;
            files.ReadAllBytes("b.bin")[1] = 9;

            Assert.Equal(new byte[] { 1, 2 }, files.ReadAllBytes("b.bin"));
        }

        [Fact]
        public void Copy_And_Move_FollowSystemIoRules()
        {
            var files = new InMemoryFileService();
            files.SeedFile("a.txt", "A");
            files.SeedFile("b.txt", "B");

            Assert.Throws<IOException>(() => files.Copy("a.txt", "b.txt"));
            files.Copy("a.txt", "b.txt", overwrite: true);
            Assert.Equal("A", files.ReadAllText("b.txt"));

            Assert.Throws<FileNotFoundException>(() => files.Move("missing.txt", "c.txt"));
            Assert.Throws<IOException>(() => files.Move("a.txt", "b.txt"));
            Assert.Throws<DirectoryNotFoundException>(() => files.Copy("a.txt", @"C:\nowhere\a.txt"));

            files.Move("a.txt", "c.txt");
            Assert.False(files.Exists("a.txt"));
            Assert.Equal("A", files.ReadAllText("c.txt"));
        }

        [Fact]
        public void MissingFiles_AndFileDirectoryConflicts()
        {
            var files = new InMemoryFileService();
            files.CreateDirectory("dir");

            Assert.Throws<FileNotFoundException>(() => files.ReadAllText("none.txt"));
            Assert.Throws<FileNotFoundException>(() => files.GetFileSize("none.txt"));
            files.Delete("none.txt");   // like File.Delete: no error for a missing file
            Assert.Throws<UnauthorizedAccessException>(() => files.WriteAllText("dir", "x"));

            files.SeedFile("f.txt", "x");
            Assert.Throws<IOException>(() => files.CreateDirectory(@"f.txt\sub"));
        }

        [Fact]
        public void GetFiles_ReturnsImmediateChildren_MatchingThePattern()
        {
            var files = new InMemoryFileService();
            files.SeedFile(@"C:\d\a.csv", "");
            files.SeedFile(@"C:\d\B.CSV", "");
            files.SeedFile(@"C:\d\c.txt", "");
            files.SeedFile(@"C:\d\sub\d.csv", "");

            var csv = files.GetFiles(@"C:\d", "*.csv").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

            Assert.Equal(new[] { @"C:\d\a.csv", @"C:\d\B.CSV" }, csv);
            Assert.Equal(3, files.GetFiles(@"C:\d").Length);
            Assert.Equal(new[] { @"C:\d\c.txt" }, files.GetFiles(@"C:\d", "?.txt"));
            Assert.Equal(new[] { @"C:\d\sub" }, files.GetDirectories(@"C:\d"));
            Assert.Throws<DirectoryNotFoundException>(() => files.GetFiles(@"C:\none"));
        }

        [Fact]
        public void DeleteDirectory_NonEmpty_WithoutRecursive_Throws()
        {
            var files = new InMemoryFileService();
            files.SeedFile(@"C:\d\sub\a.txt", "");

            Assert.Throws<IOException>(() => files.DeleteDirectory(@"C:\d"));
            files.DeleteDirectory(@"C:\d", recursive: true);

            Assert.False(files.DirectoryExists(@"C:\d"));
            Assert.False(files.Exists(@"C:\d\sub\a.txt"));
            Assert.Throws<DirectoryNotFoundException>(() => files.DeleteDirectory(@"C:\d"));
        }

        [Fact]
        public void NullOrEmptyPath_ThrowsArgumentNull()
        {
            var files = new InMemoryFileService();
            Assert.Throws<ArgumentNullException>(() => files.Exists(null));
            Assert.Throws<ArgumentNullException>(() => files.WriteAllText("", "x"));
            Assert.Throws<ArgumentNullException>(() => files.Combine(null));
            Assert.Throws<ArgumentNullException>(() => files.WriteAllText("a.txt", "x", null));
        }

        [Fact]
        public void Failure_IsThrownBeforeAnyChange()
        {
            var files = new InMemoryFileService();
            files.SeedFile("a.txt", "A");
            FileOperationCall seen = null;
            files.Failure = c => { seen = c; return c.Operation == FileOperation.Copy ? new IOException("locked by Excel") : null; };

            Assert.Equal("locked by Excel", Assert.Throws<IOException>(() => files.Copy("a.txt", "b.txt")).Message);

            Assert.Equal(@"C:\test\a.txt", seen.Path);
            Assert.Equal(@"C:\test\b.txt", seen.Destination);
            Assert.False(files.Exists("b.txt"));
            files.WriteAllText("c.txt", "ok");   // null from the hook: no failure
            Assert.Equal(FileOperation.Write, seen.Operation);
        }

        [Fact]
        public void Copy_OntoItself_Throws_EvenWithOverwrite()
        {
            var files = new InMemoryFileService();
            files.SeedFile("a.txt", "A");
            Assert.Throws<IOException>(() => files.Copy("a.txt", @"C:/TEST/A.TXT", overwrite: true));
            Assert.Equal("A", files.ReadAllText("a.txt"));
        }

        [Fact]
        public void StarDotStar_MatchesNamesWithoutAnExtension_LikeWindows()
        {
            var files = new InMemoryFileService();
            files.SeedFile(@"C:\d\README", "");
            files.SeedFile(@"C:\d\a.txt", "");
            files.SeedDirectory(@"C:\d\sub");

            Assert.Equal(2, files.GetFiles(@"C:\d", "*.*").Length);
            Assert.Single(files.GetDirectories(@"C:\d", "*.*"));
            Assert.Equal(new[] { @"C:\d\README" }, files.GetFiles(@"C:\d", "READ*.*"));
        }

        [Fact]
        public void DriveRelativePaths_ResolveAgainstTheBaseDirectoryOnItsDrive()
        {
            var files = new InMemoryFileService();
            Assert.Equal(@"C:\test\report.txt", files.GetFullPath("C:report.txt"));
            Assert.Equal(@"D:\report.txt", files.GetFullPath("d:report.txt"));
            Assert.Equal(@"C:\test", files.GetFullPath("C:"));
            Assert.Equal(@"D:\", files.GetFullPath("D:"));
        }

        [Fact]
        public void Read_MissingParent_OrADirectory_ThrowsLikeSystemIo()
        {
            var files = new InMemoryFileService();
            files.CreateDirectory("dir");

            Assert.Throws<DirectoryNotFoundException>(() => files.ReadAllText(@"C:\missing\file.txt"));
            Assert.Throws<UnauthorizedAccessException>(() => files.ReadAllText("dir"));
            Assert.Throws<DirectoryNotFoundException>(() => files.Copy(@"C:\missing\a.txt", "b.txt"));
        }

        [Fact]
        public void SeedFile_OntoADirectory_Throws()
        {
            var files = new InMemoryFileService();
            Assert.Throws<UnauthorizedAccessException>(() => files.SeedFile(@"C:\test", "x"));
            Assert.False(files.Exists(@"C:\test"));
        }

        [Fact]
        public void FileOperations_RejectDirectoryShapedPaths()
        {
            var files = new InMemoryFileService();
            files.SeedFile("report.txt", "data");

            Assert.False(files.Exists("report.txt/"));
            Assert.Throws<UnauthorizedAccessException>(() => files.Delete(@"report.txt\"));
            Assert.Throws<UnauthorizedAccessException>(() => files.WriteAllText("new.txt/", "x"));
            Assert.True(files.Exists("report.txt"));
            Assert.False(files.Exists("new.txt"));
        }

        [Fact]
        public void PathHelpers_FollowWindowsRules()
        {
            var files = new InMemoryFileService();

            Assert.Equal(@"C:\a", files.GetDirectoryName(@"C:\a\b.txt"));
            Assert.Equal(@"C:\", files.GetDirectoryName(@"C:\b.txt"));
            Assert.Null(files.GetDirectoryName(@"C:\"));
            Assert.Equal("b.txt", files.GetFileName("C:/a/b.txt"));
            Assert.Equal("b", files.GetFileNameWithoutExtension(@"C:\a\b.txt"));
            Assert.Equal(".CSV", files.GetExtension("x.CSV"));
            Assert.Equal("", files.GetExtension(@"C:\a.d\file"));
            Assert.Equal(@"C:\a\b.txt", files.Combine(@"C:\a", "b.txt"));
            Assert.Equal(@"D:\x", files.Combine(@"C:\a", @"D:\x"));
            Assert.Equal(@"a\b", files.Combine("a", "", "b"));
        }
    }
}
