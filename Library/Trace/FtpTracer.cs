using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Trace
{
    /// <summary>
    /// The class for tracing FTP commands and replies
    /// </summary>
    public class FtpTracer
    {
        private readonly ObservableCollection<IPEndPoint> connectedUsers = new ObservableCollection<IPEndPoint>();

        private readonly ReadOnlyObservableCollection<IPEndPoint> _connectedUsersView;

        /// <summary>
        /// The read-only collection of currently connected users. Lock <see cref="ConnectedUsersView"/>
        /// when accessing this.
        /// </summary>
        public ReadOnlyObservableCollection<IPEndPoint> ConnectedUsersView => _connectedUsersView;

        /// <summary>
        /// The sync root for <see cref="ConnectedUsersView"/>.
        /// </summary>
        public object ConnectedUsersSyncRoot { get; } = new object();

        internal FtpTracer()
        {
            _connectedUsersView = new ReadOnlyObservableCollection<IPEndPoint>(connectedUsers);
        }

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
            Task.Run(() =>
            {
                try
                {
                    CommandInvoked?.Invoke(command, remoteAddress);
                }
                catch { }
            });
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
            Task.Run(() =>
            {
                try
                {
                    ReplyInvoked?.Invoke(replyCode, remoteAddress);
                }
                catch { }
            });
        }

        /// <summary>
        /// The event handler to handle user changes
        /// </summary>
        /// <param name="remoteAddress"></param>
        public delegate void UserEventHandler(IPEndPoint remoteAddress);

        /// <summary>
        /// Fires when a user connects to the FTP server
        /// </summary>
        public event UserEventHandler UserConnected;

        /// <summary>
        /// Fires when a user disconnects from the FTP server
        /// </summary>
        public event UserEventHandler UserDisconnected;

        internal void TraceUserConnection(IPEndPoint remoteAddress)
        {
            Task.Run(() =>
            {
                lock (ConnectedUsersSyncRoot)
                    connectedUsers.Add(remoteAddress);
                try
                {
                    UserConnected?.Invoke(remoteAddress);
                }
                catch { }
            });
        }

        internal void TraceUserDisconnection(IPEndPoint remoteAddress)
        {
            Task.Run(() =>
            {
                lock (ConnectedUsersSyncRoot)
                    connectedUsers.Remove(remoteAddress);
                try
                {
                    UserDisconnected?.Invoke(remoteAddress);
                }
                catch { }
            });
        }
    }
}
