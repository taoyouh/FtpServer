// <copyright file="FileProvider.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// The provider of files for a single user
    /// </summary>
    public class FileProvider
    {
        /// <summary>
        /// The root directory for ftp
        /// </summary>
        private string baseDirectory;

        /// <summary>
        /// The remote working directory path, without '/' at start.
        /// Use <see cref="GetLocalPath(string)"/> to get local
        /// working directory
        /// </summary>
        private string workingDirectory = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileProvider"/> class.
        /// </summary>
        /// <param name="baseDirectory">The root directory of the user</param>
        public FileProvider(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory))
            {
                throw new IOException("Base directory doesn't exist");
            }
            this.baseDirectory = baseDirectory;
        }

        /// <summary>
        /// Gets the FTP working directory
        /// </summary>
        /// <returns>The FTP working directory absolute path</returns>
        public virtual string GetWorkingDirectory()
        {
            return "/" + workingDirectory;
        }

        /// <summary>
        /// Sets the FTP working directory
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of working directory</param>
        /// <returns>Whether the setting succeeded or not</returns>
        public virtual bool SetWorkingDirectory(string path)
        {
            var localPath = GetLocalPath(path);
            if (Directory.Exists(localPath))
            {
                workingDirectory = GetFtpPath(localPath);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a directory
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the directory</param>
        /// <returns>The task to await</returns>
#pragma warning disable CS1998
        public virtual async Task CreateDirectoryAsync(string path)
#pragma warning restore CS1998
        {
            Directory.CreateDirectory(GetLocalPath(path));
        }

        /// <summary>
        /// Deletes a directory
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the directory</param>
        /// <returns>The task to await</returns>
#pragma warning disable CS1998
        public virtual async Task DeleteDirectoryAsync(string path)
#pragma warning restore CS1998
        {
            var localPath = GetLocalPath(path);
            if (localPath == GetLocalPath(baseDirectory))
            {
                throw new UnauthorizedAccessException("User tried to delete base directory");
            }
            Directory.Delete(localPath, true);
        }

        /// <summary>
        /// Deletes a file
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <returns>The task to await</returns>
#pragma warning disable CS1998
        public virtual async Task DeleteAsync(string path)
#pragma warning restore CS1998
        {
            System.IO.File.Delete(GetLocalPath(path));
        }

        /// <summary>
        /// Renames or moves a file or directory
        /// </summary>
        /// <param name="fromPath">Absolute or relative FTP path of source file or directory</param>
        /// <param name="toPath">Absolute or relative FTP path of target file or directory</param>
        /// <returns>The task to await</returns>
#pragma warning disable CS1998
        public virtual async Task RenameAsync(string fromPath, string toPath)
#pragma warning restore CS1998
        {
            var fromLocalPath = GetLocalPath(fromPath);
            var toLocalPath = GetLocalPath(toPath);
            Directory.Move(fromLocalPath, toLocalPath);
        }

        /// <summary>
        /// Opens a file
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <param name="mode">Open mode</param>
        /// <param name="access">Access types</param>
        /// <returns>The file stream</returns>
#pragma warning disable CS1998
        public virtual async Task<Stream> OpenFileAsync(string path, FileMode mode, FileAccess access)
#pragma warning restore CS1998
        {
            string localPath = GetLocalPath(path);
            return System.IO.File.Open(localPath, mode, access);
        }

        /// <summary>
        /// Opens a file for reading
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <returns>The file stream</returns>
#pragma warning disable CS1998
        public virtual async Task<Stream> OpenFileForReadAsync(string path)
#pragma warning restore CS1998
        {
            string localPath = GetLocalPath(path);
            return System.IO.File.OpenRead(localPath);
        }

        /// <summary>
        /// Opens a file for writing
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <returns>The file stream</returns>
#pragma warning disable CS1998
        public virtual async Task<Stream> OpenFileForWriteAsync(string path)
#pragma warning restore CS1998
        {
            string localPath = GetLocalPath(path);
            return System.IO.File.OpenWrite(localPath);
        }

        /// <summary>
        /// Gets the names of files and directories
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <returns>The names of items</returns>
#pragma warning disable CS1998
        public virtual async Task<IEnumerable<string>> GetNameListingAsync(string path)
#pragma warning restore CS1998
        {
            string localPath = GetLocalPath(path);
            return Directory.EnumerateFileSystemEntries(localPath);
        }

        /// <summary>
        /// Gets the info of files and directories
        /// </summary>
        /// <param name="path">Absolute or relative FTP path of the file</param>
        /// <returns>The info of items in <see cref="FileInfo"/> or <see cref="DirectoryInfo"/></returns>
#pragma warning disable CS1998
        public virtual async Task<IEnumerable<FileSystemInfo>> GetListingAsync(string path)
#pragma warning restore CS1998
        {
            string localPath = GetLocalPath(path);
            var directories = Directory.GetDirectories(localPath).Select(x => new DirectoryInfo(x));
            var files = Directory.GetFiles(localPath).Select(x => new FileInfo(x));
            return directories.Concat<FileSystemInfo>(files);
        }

        private string GetLocalPath(string path)
        {
            string fullPath = Path.Combine(workingDirectory, path).TrimStart('/', '\\');
            string localPath = Path.GetFullPath(Path.Combine(baseDirectory, fullPath));
            if (!Path.GetFullPath(localPath).Contains(Path.GetFullPath(baseDirectory)))
                throw new UnauthorizedAccessException("User tried to access out of base directory");
            return localPath;
        }

        private string GetFtpPath(string localPath)
        {
            string localFullPath = Path.GetFullPath(localPath);
            string baseFullPath = Path.GetFullPath(baseDirectory);
            if (!localFullPath.Contains(baseFullPath))
            {
                throw new UnauthorizedAccessException("User tried to access out of base directory");
            }
            return localFullPath.Replace(baseFullPath, string.Empty).TrimStart('/', '\\').Replace('\\', '/');
        }
    }
}
