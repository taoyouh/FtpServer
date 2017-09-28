using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// Error when writing to file that will occur when retrying
    /// </summary>
    public class FileSpaceInsufficientException : Exception
    {
        /// <summary>
        /// Initiates a <see cref="FileSpaceInsufficientException"/>.
        /// </summary>
        public FileSpaceInsufficientException()
        { }

        /// <summary>
        /// Initiates a <see cref="FileSpaceInsufficientException"/> with given message.
        /// </summary>
        /// <param name="message">Description of exception</param>
        public FileSpaceInsufficientException(string message) :
            base(message)
        { }

        /// <summary>
        /// Initiates a <see cref="FileSpaceInsufficientException"/> with given message and inner exception.
        /// </summary>
        /// <param name="message">Description of exception</param>
        /// <param name="innerException">Inner exception</param>
        public FileSpaceInsufficientException(string message, Exception innerException) :
            base(message, innerException)
        { }
    }
}
