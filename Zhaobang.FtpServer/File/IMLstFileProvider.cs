// <copyright file="IMLstFileProvider.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// Extended file provider interface to support actions needed by MLsT and MLsD commands.
    /// </summary>
    public interface IMLstFileProvider : IFileProvider
    {
        /// <summary>
        /// Gets a file system entry at the specified path.
        /// </summary>
        /// <param name="path">The path of the file system entry.</param>
        /// <returns>A task representing the asynchronous operation with the file system entry.</returns>
        Task<FileSystemEntry> GetItemAsync(string path);

        /// <summary>
        /// Gets the child items of the specified directory.
        /// </summary>
        /// <param name="path">The path of the directory.</param>
        /// <returns>A task representing the asynchronous operation with the child items.</returns>
        Task<IEnumerable<FileSystemEntry>> GetChildItems(string path);
    }
}
