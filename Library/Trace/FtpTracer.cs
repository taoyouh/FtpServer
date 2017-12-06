using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Zhaobang.FtpServer.Trace
{
    /// <summary>
    /// THe class for tracing FTP commands and replies
    /// </summary>
    public class FtpTracer
    {
        /// <summary>
        /// Event handler for tracing FTP commands
        /// </summary>
        /// <param name="command">The command received by the server</param>
        /// <param name="remoteAddress">The remote endpoint that sent the command</param>
        public delegate void FtpCommandInvokedHandler(string command, IPEndPoint remoteAddress);

        /// <summary>
        /// The event is fired when a command is received by the server
        /// </summary>
        public event FtpCommandInvokedHandler CommandInvoked;

        internal void TraceCommand(string command, IPEndPoint remoteAddress)
        {
            try
            {
                CommandInvoked?.Invoke(command, remoteAddress);
            }
            catch { }
        }

        /// <summary>
        /// Event handler for tracing FTP replies
        /// </summary>
        /// <param name="replyCode">The reply code sent by the server</param>
        /// <param name="remoteAddress">The remote endpoint that the reply is sent to</param>
        public delegate void FtpReplyInvokedHandler(string replyCode, IPEndPoint remoteAddress);

        /// <summary>
        /// The event is fired when a reply is sent from the server
        /// </summary>
        public event FtpReplyInvokedHandler ReplyInvoked;

        internal void TraceReply(string replyCode, IPEndPoint remoteAddress)
        {
            try
            {
                ReplyInvoked?.Invoke(replyCode, remoteAddress);
            }
            catch { }
        }
    }
}
