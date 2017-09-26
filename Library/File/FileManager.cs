// <copyright file="FileManager.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// Manager that provides <see cref="FileProvider"/> for each user
    /// </summary>
    public class FileManager
    {
        private string baseDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManager"/> class.
        /// Every user shares the same root directory.
        /// </summary>
        /// <param name="baseDirectory">The root directory of FTP</param>
        public FileManager(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory))
            {
                throw new IOException("Base directory doesn't exist");
            }
            this.baseDirectory = baseDirectory;
        }

        /// <summary>
        /// Gets provider for the specified user
        /// </summary>
        /// <param name="user">The name of the user</param>
        /// <returns>The <see cref="FileProvider"/> for that user</returns>
        public virtual FileProvider GetProvider(string user)
        {
            return new FileProvider(baseDirectory);
        }
    }
}
