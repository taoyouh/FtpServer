// <copyright file="SocketExtensions.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// The extension class to implement some Socket methods not available in old target.
    /// </summary>
    public static class SocketExtensions
    {
        private static readonly ConcurrentQueue<AcceptJob> IncomingJobs = new ConcurrentQueue<AcceptJob>();
        private static volatile int processingCount = 0;

        /// <summary>
        /// Accept an incoming connection on a given socket.
        /// </summary>
        /// <param name="listenSocket">The listening socket.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the accept progress.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static Task<Socket> AcceptAsync(this Socket listenSocket, CancellationToken cancellationToken)
        {
            var taskSource = new TaskCompletionSource<Socket>(TaskCreationOptions.RunContinuationsAsynchronously);
            IncomingJobs.Enqueue(new AcceptJob(listenSocket, cancellationToken, taskSource));
            if (Interlocked.Increment(ref processingCount) == 1)
            {
                Task.Factory.StartNew(AcceptLoop, TaskCreationOptions.LongRunning);
            }
            return taskSource.Task;
        }

        private static async Task AcceptLoop()
        {
            bool exit = false;
            List<Socket> readSockets = new List<Socket>();
            Dictionary<Socket, AcceptJob> processingJobs = new Dictionary<Socket, AcceptJob>();

            void RemoveJob(Socket socket)
            {
                if (Interlocked.Decrement(ref processingCount) == 0)
                {
                    exit = true;
                }
                processingJobs.Remove(socket);
            }

            while (!exit)
            {
                while (IncomingJobs.TryDequeue(out AcceptJob job))
                {
                    if (processingJobs.ContainsKey(job.ListenSocket))
                    {
                        job.ListenTaskSource.SetException(new InvalidOperationException("There is another in-progress accept task for this socket."));
                    }

                    processingJobs.Add(job.ListenSocket, job);
                }

                foreach (var job in processingJobs.Values.ToArray())
                {
                    if (job.CancellationToken.IsCancellationRequested)
                    {
                        RemoveJob(job.ListenSocket);
                        job.ListenTaskSource.SetCanceled();
                    }
                }

                readSockets.Clear();
                readSockets.AddRange(processingJobs.Keys);
                if (readSockets.Count == 0)
                {
                    continue;
                }

                try
                {
                    Socket.Select(readSockets, null, null, 1000);

                    foreach (Socket socket in readSockets)
                    {
                        AcceptJob job = processingJobs[socket];
                        RemoveJob(socket);

                        try
                        {
                            Socket clientSocket = socket.Accept();
                            job.ListenTaskSource.SetResult(clientSocket);
                        }
                        catch (Exception ex)
                        {
                            job.ListenTaskSource.SetException(ex);
                        }

                        await Task.Yield();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is SocketException || ex is ObjectDisposedException)
                    {
                        foreach (AcceptJob job in processingJobs.Values.ToArray())
                        {
                            try
                            {
                                job.ListenSocket.Poll(0, SelectMode.SelectRead);
                            }
                            catch (Exception ex2)
                            {
                                RemoveJob(job.ListenSocket);
                                job.ListenTaskSource.SetException(ex2);
                            }
                        }
                    }
                    else
                    {
                        foreach (AcceptJob job in processingJobs.Values.ToArray())
                        {
                            RemoveJob(job.ListenSocket);
                            job.ListenTaskSource.SetException(ex);
                        }
                    }
                }
            }
        }

        private readonly struct AcceptJob
        {
            public AcceptJob(Socket listenSocket, CancellationToken cancellationToken, TaskCompletionSource<Socket> listenTaskSource)
            {
                this.ListenSocket = listenSocket ?? throw new ArgumentNullException(nameof(listenSocket));
                this.CancellationToken = cancellationToken;
                this.ListenTaskSource = listenTaskSource ?? throw new ArgumentNullException(nameof(listenTaskSource));
            }

            public Socket ListenSocket { get; }

            public CancellationToken CancellationToken { get; }

            public TaskCompletionSource<Socket> ListenTaskSource { get; }
        }
    }
}