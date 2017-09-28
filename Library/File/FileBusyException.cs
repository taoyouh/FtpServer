using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer.File
{
    /// <summary>
    /// Exception indicating that file action failed and may work
    /// when retrying. Causing FTP reply code 450
    /// </summary>
    public class FileBusyException : Exception
    {
        /// <summary>
        /// Initiates a <see cref="FileBusyException"/>.
        /// </summary>
        public FileBusyException()
        { }

        /// <summary>
        /// Initiates a <see cref="FileBusyException"/> with given message.
        /// </summary>
        /// <param name="message">Description of exception</param>
        public FileBusyException(string message) :
            base(message)
        { }

        /// <summary>
        /// Initiates a <see cref="FileBusyException"/> with given message and inner exception.
        /// </summary>
        /// <param name="message">Description of exception</param>
        /// <param name="innerException">Inner exception</param>
        public FileBusyException(string message, Exception innerException) :
            base(message, innerException)
        { }
    }
}
