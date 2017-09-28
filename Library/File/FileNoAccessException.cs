using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// Exception indicating that file action failed and will not
    /// success when retrying (e.g., file not found or unauthorized access).
    /// Causing FTP reply code 550
    /// </summary>
    class FileNoAccessException : Exception
    {
        /// <summary>
        /// Initiates a <see cref="FileNoAccessException"/>.
        /// </summary>
        public FileNoAccessException()
        { }

        /// <summary>
        /// Initiates a <see cref="FileNoAccessException"/> with given message.
        /// </summary>
        /// <param name="message">Description of exception</param>
        public FileNoAccessException(string message) :
            base(message)
        { }

        /// <summary>
        /// Initiates a <see cref="FileNoAccessException"/> with given message and inner exception.
        /// </summary>
        /// <param name="message">Description of exception</param>
        /// <param name="innerException">Inner exception</param>
        public FileNoAccessException(string message, Exception innerException) :
            base(message, innerException)
        { }
    }
}
