﻿// <copyright file="ControlConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.Exceptions;
using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Used to maintain the FTP control connection
    /// </summary>
    internal class ControlConnection : IDisposable
    {
        private const int ReadByteBufferLength = 12;
        private const int ReadCharBufferLength = 12;

        private readonly FtpServer server;
        private readonly TcpClient tcpClient;
        private readonly NetworkStream stream;

        private readonly IPEndPoint remoteEndPoint;
        private readonly IPEndPoint localEndPoint;

        /// <summary>
        /// This should be available all time, but needs to check
        /// <see cref="DataConnection.IsOpen"/> before usage.
        /// </summary>
        private readonly DataConnection dataConnection;

        private Encoding encoding = Encoding.UTF8;
        private string userName = string.Empty;
        private bool authenticated;

        private byte[] readByteBuffer = new byte[ReadByteBufferLength];
        private char[] readCharBuffer = new char[ReadCharBufferLength];
        private int readOffset = 0;

        private DataConnectionMode dataConnectionMode = DataConnectionMode.Active;
        private IPAddress userActiveIP;
        private int userActiveDataPort = 20;

        /// <summary>
        /// This is relevant to user, and should be available if
        /// and only if <see cref="authenticated"/> is true.
        /// </summary>
        private FileProvider fileProvider;

        /// <summary>
        /// Only stream mode is supported
        /// </summary>
        private TransmissionMode transmissionMode = TransmissionMode.Stream;

        /// <summary>
        /// This is ignored
        /// </summary>
        private DataType dataType = DataType.ASCII;

        private ListFormat listFormat = ListFormat.Unix;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlConnection"/> class.
        /// Used by <see cref="FtpServer"/> to create a control connection.
        /// </summary>
        /// <param name="server">The <see cref="FtpServer"/> that creates the connection</param>
        /// <param name="tcpClient">The TCP client of the connection</param>
        internal ControlConnection(FtpServer server, TcpClient tcpClient)
        {
            this.server = server;
            this.tcpClient = tcpClient;

            var remoteUri = new Uri("ftp://" + this.tcpClient.Client.RemoteEndPoint.ToString());
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteUri.Host), remoteUri.Port);
            userActiveDataPort = remoteEndPoint.Port;
            userActiveIP = remoteEndPoint.Address;

            var localUri = new Uri("ftp://" + this.tcpClient.Client.LocalEndPoint.ToString());
            localEndPoint = new IPEndPoint(IPAddress.Parse(localUri.Host), localUri.Port);

            dataConnection = server.DataConnector.GetDataConnection(localEndPoint.Address);

            stream = this.tcpClient.GetStream();
        }

        private enum ListFormat
        {
            Unix,
            MsDos
        }

        private enum TransmissionMode
        {
            Stream
        }

        private enum DataType
        {
            ASCII,
            IMAGE
        }

        private enum DataConnectionMode
        {
            Passive,
            Active
        }

        /// <summary>
        /// Defined in page 40 in RFC 959
        /// </summary>
        private enum FtpReplyCode
        {
            CommandOkay = 200,
            SystemStatus = 211,
            CommandUnrecognized = 500,
            SyntaxErrorInParametersOrArguments = 501,
            NotImplemented = 502,
            ParameterNotImplemented = 504,
            BadSequence = 503,
            ServiceReady = 220,
            UserLoggedIn = 230,
            NotLoggedIn = 530,
            NeedPassword = 331,
            LocalError = 451,
            PathCreated = 257,
            TransferStarting = 125,
            SuccessClosingDataConnection = 226,
            FileActionOk = 250,
            FileUnavailable = 550,
            EnteringPassiveMode = 227,
            AboutToOpenDataConnection = 150,
            NameSystemType = 215,
            FileActionPendingInfo = 350,
        }

        /// <summary>
        /// Dispose of all the connections
        /// </summary>
        public void Dispose()
        {
            tcpClient.Dispose();
            dataConnection.Dispose();
        }

        /// <summary>
        /// Starts the control connection.
        /// </summary>
        /// <remarks>Can only be used once</remarks>
        /// <param name="cancellationToken">Token to terminate the control connection</param>
        /// <returns>The task that finishes when control connection is closed</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ReplyAsync(FtpReplyCode.ServiceReady, "FtpServer by Taoyou is now ready");

                while (true)
                {
                    var command = await ReadLineAsync();
                    try
                    {
                        await ProcessCommandAsync(command);
                    }
                    catch (QuitRequestedException) { return; }
                    catch (Exception ex)
                    {
                        await ReplyAsync(
                            FtpReplyCode.LocalError,
                            string.Format("Exception thrown, message: {0}", ex.Message).Replace('\r', ' ').Replace('\n', ' '));
                    }
                    if (cancellationToken.IsCancellationRequested)
                        return;
                }
            }
            finally
            {
                Dispose();
            }
        }

        private async Task ProcessCommandAsync(string message)
        {
            var messageSegs = message.Split(new[] { ' ' }, 2);
            var command = messageSegs[0];
            var parameter = messageSegs.Length < 2 ? string.Empty : messageSegs[1];

            System.Diagnostics.Debug.WriteLine(message);
            switch (command.ToUpper())
            {
                case "RNFR":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await CommandRnfrAsync(parameter);
                    return;
                case "RNTO":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await ReplyAsync(FtpReplyCode.BadSequence, "Should use RNFR first");
                    return;
                case "DELE":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await fileProvider.DeleteAsync(parameter);
                    await ReplyAsync(FtpReplyCode.FileActionOk, "Delete succeeded");
                    return;
                case "RMD":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await fileProvider.DeleteDirectoryAsync(parameter);
                    await ReplyAsync(FtpReplyCode.FileActionOk, "Directory deleted");
                    return;
                case "MKD":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await fileProvider.CreateDirectoryAsync(parameter);
                    await ReplyAsync(
                        FtpReplyCode.PathCreated,
                        string.Format(
                            "\"{0}\"",
                            fileProvider.GetWorkingDirectory().Replace("\"", "\"\"")));
                    return;
                case "PWD":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    await ReplyAsync(
                        FtpReplyCode.PathCreated,
                        string.Format(
                            "\"{0}\"",
                            fileProvider.GetWorkingDirectory().Replace("\"", "\"\"")));
                    return;
                case "SYST":
                    await ReplyAsync(FtpReplyCode.NameSystemType, "UNIX simulated by .NET Core");
                    return;
                case "FEAT":
                    await ReplyMultilineAsync(FtpReplyCode.SystemStatus, "Supports:\nUTF8");
                    return;
                case "OPTS":
                    if (parameter.ToUpper() == "UTF8 ON")
                    {
                        encoding = Encoding.UTF8;
                        await ReplyAsync(FtpReplyCode.CommandOkay, "UTF-8 is on");
                        return;
                    }
                    else if (parameter.ToUpper() == "UTF8 OFF")
                    {
                        encoding = Encoding.ASCII;
                        await ReplyAsync(FtpReplyCode.CommandOkay, "UTF-8 is off");
                        return;
                    }
                    break;
                case "USER":
                    userName = parameter;
                    authenticated = false;
                    fileProvider = null;
                    await ReplyAsync(FtpReplyCode.NeedPassword, "Please input password");
                    return;
                case "PASS":
                    if (authenticated = server.Authenticator.Authenticate(userName, parameter))
                    {
                        await ReplyAsync(FtpReplyCode.UserLoggedIn, "Logged in");
                        fileProvider = server.FileManager.GetProvider(userName);
                    }
                    else
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "Failed to log in");
                        fileProvider = null;
                    }
                    return;
                case "PORT":
                    await CommandPortAsync(parameter);
                    return;
                case "PASV":
                    await CommandPasvAsync();
                    return;
                case "TYPE":
                    switch (parameter)
                    {
                        case "A":
                            dataType = DataType.ASCII;
                            await ReplyAsync(FtpReplyCode.CommandOkay, "In ASCII type");
                            return;
                        case "I":
                            dataType = DataType.IMAGE;
                            await ReplyAsync(FtpReplyCode.CommandOkay, "In IMAGE type");
                            return;
                        default:
                            await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Unknown type");
                            return;
                    }
                case "MODE":
                    switch (parameter)
                    {
                        case "S":
                            transmissionMode = TransmissionMode.Stream;
                            await ReplyAsync(FtpReplyCode.CommandOkay, "In stream mode");
                            return;
                        default:
                            await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Unknown mode");
                            return;
                    }
                case "QUIT":
                    throw new QuitRequestedException();
                case "RETR":
                    await CommandRetrAsync(parameter);
                    return;
                case "STOR":
                    await CommandStorAsync(parameter);
                    return;
                case "CWD":
                    if (!authenticated)
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                        return;
                    }
                    if (fileProvider.SetWorkingDirectory(parameter))
                    {
                        await ReplyAsync(FtpReplyCode.FileActionOk, fileProvider.GetWorkingDirectory());
                    }
                    else
                    {
                        await ReplyAsync(FtpReplyCode.FileUnavailable, "Path doesn't exist");
                    }
                    return;
                case "NLST":
                    await CommandNlstAsync(parameter);
                    return;
                case "LIST":
                    await CommandListAsync(parameter);
                    return;
                case "NOOP":
                    await ReplyAsync(FtpReplyCode.CommandOkay, "OK");
                    return;
            }
            await ReplyAsync(FtpReplyCode.CommandUnrecognized, "Can't recognize this command.");
        }

        private async Task CommandRnfrAsync(string parameter)
        {
            var fromPath = parameter;
            await ReplyAsync(FtpReplyCode.FileActionPendingInfo, "Waiting for RNTO");
            var nextCommand = await ReadLineAsync();
            if (!nextCommand.ToUpper().StartsWith("RNTO "))
            {
                await ReplyAsync(FtpReplyCode.BadSequence, "Wrong sequence, renaming aborted");
                return;
            }
            var toPath = nextCommand.Substring(5);
            await fileProvider.RenameAsync(fromPath, toPath);
            await ReplyAsync(FtpReplyCode.FileActionOk, "Rename succeeded");
        }

        private async Task CommandListAsync(string parameter)
        {
            if (!authenticated)
            {
                await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                return;
            }
            var listing = await fileProvider.GetListingAsync(parameter);
            MemoryStream stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            writer.NewLine = "\r\n";
            await writer.WriteLineAsync();
            foreach (var item in listing)
            {
                if (listFormat == ListFormat.Unix)
                {
                    if (item is FileInfo file)
                    {
                        await writer.WriteLineAsync(
                            string.Format(
                                "-{0}{0}{0}   1 owner   group {1,15} {2} {3}",
                                file.IsReadOnly ? "r-x" : "rwx",
                                file.Length,
                                item.LastWriteTime.ToString(
                                item.LastWriteTime.Year == DateTime.Now.Year ?
                                    "MMM dd HH:mm" : "MMM dd  yyyy", CultureInfo.InvariantCulture),
                                item.Name));
                    }
                    else
                    {
                        await writer.WriteLineAsync(
                            string.Format(
                                "drwxrwxrwx   1 owner   group               0 {0} {1}",
                                item.LastWriteTime.ToString(
                                item.LastWriteTime.Year == DateTime.Now.Year ?
                                    "MMM dd HH:mm" : "MMM dd  yyyy", CultureInfo.InvariantCulture),
                                item.Name));
                    }
                }
                else if (listFormat == ListFormat.MsDos)
                {
                    if (item is FileInfo file)
                    {
                        await writer.WriteLineAsync(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0:MM-dd-yy  hh:mmtt} {1,20} {2}",
                                item.LastWriteTime,
                                file.Length,
                                item.Name));
                    }
                    else
                    {
                        await writer.WriteLineAsync(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0:MM-dd-yy  hh:mmtt}       {1,-14} {2}",
                                item.LastWriteTime,
                                "<DIR>",
                                item.Name));
                    }
                }
                else
                {
                    throw new NotSupportedException("Can't only use Unix or MS-DOS listing format.");
                }
            }
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            await OpenDataConnectionAsync();
            await dataConnection.SendAsync(stream);
            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "Listing has been sent");
            return;
        }

        private async Task CommandNlstAsync(string parameter)
        {
            if (!authenticated)
            {
                await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                return;
            }
            var nameListing = await fileProvider.GetNameListingAsync(parameter);
            MemoryStream stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            writer.NewLine = "\r\n";
            foreach (var item in nameListing)
            {
                await writer.WriteLineAsync(item);
            }
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            await OpenDataConnectionAsync();
            await dataConnection.SendAsync(stream);
            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "Listing has been sent");
            return;
        }

        private async Task CommandStorAsync(string parameter)
        {
            if (!authenticated)
            {
                await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                return;
            }
            if (string.IsNullOrEmpty(parameter))
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, path is missing");
                return;
            }
            if (transmissionMode != TransmissionMode.Stream)
            {
                await ReplyAsync(FtpReplyCode.NotImplemented, "Only supports stream mode");
                return;
            }

            using (Stream fileStream = await fileProvider.OpenFileForWriteAsync(parameter))
            {
                await OpenDataConnectionAsync();
                await dataConnection.RecieveAsync(fileStream);
                await fileStream.FlushAsync();
            }
            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "File has been recieved");
            return;
        }

        private async Task CommandRetrAsync(string parameter)
        {
            if (!authenticated)
            {
                await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                return;
            }
            if (string.IsNullOrEmpty(parameter))
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, path is missing");
                return;
            }
            if (transmissionMode != TransmissionMode.Stream)
            {
                await ReplyAsync(FtpReplyCode.NotImplemented, "Only supports stream mode");
                return;
            }

            using (Stream fileStream = await fileProvider.OpenFileForReadAsync(parameter))
            {
                await OpenDataConnectionAsync();
                await dataConnection.SendAsync(fileStream);
            }
            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "File has been sent");
            return;
        }

        private async Task CommandPasvAsync()
        {
            var localEP = dataConnection.Listen();
            var ipBytes = localEP.Address.GetAddressBytes();
            if (ipBytes.Length != 4) throw new Exception();
            var passiveEPString =
                string.Format(
                    "{0},{1},{2},{3},{4},{5}",
                    ipBytes[0],
                    ipBytes[1],
                    ipBytes[2],
                    ipBytes[3],
                    (byte)(localEP.Port / 256),
                    (byte)(localEP.Port % 256));
            dataConnectionMode = DataConnectionMode.Passive;
            await ReplyAsync(FtpReplyCode.EnteringPassiveMode, "Enter Passive Mode (" + passiveEPString + ")");
        }

        private async Task CommandPortAsync(string parameter)
        {
            var paramSegs = parameter.Split(',');
            if (paramSegs.Length != 6)
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, count of comma incorrect");
                return;
            }
            try
            {
                var bytes = paramSegs.Select(x => byte.Parse(x)).ToArray();
                IPAddress remoteIP = new IPAddress(new ArraySegment<byte>(bytes, 0, 4).ToArray());
                int remotePort = (bytes[4] << 8) | bytes[5];
                userActiveDataPort = remotePort;
                userActiveIP = remoteIP;
                dataConnectionMode = DataConnectionMode.Active;
                await dataConnection.ConnectActiveAsync(remoteIP, remotePort);
                await ReplyAsync(FtpReplyCode.CommandOkay, "Data connection established");
                return;
            }
            catch
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, number format incorrect");
                return;
            }
        }

        private async Task OpenDataConnectionAsync()
        {
            if (dataConnection != null && dataConnection.IsOpen)
            {
                await ReplyAsync(FtpReplyCode.TransferStarting, "Transfer is starting");
            }
            else
            {
                await ReplyAsync(FtpReplyCode.AboutToOpenDataConnection, "File is Ok, about to open connection.");
                if (dataConnectionMode == DataConnectionMode.Active)
                {
                    await dataConnection.ConnectActiveAsync(userActiveIP, userActiveDataPort);
                }
                else if (dataConnectionMode == DataConnectionMode.Passive)
                {
                    await dataConnection.AcceptAsync();
                }
            }
        }

        /// <summary>
        /// Reads a line from network stream partitioned by CRLF
        /// </summary>
        /// <returns>The line read with CRLF trimmed</returns>
        private async Task<string> ReadLineAsync()
        {
            var decoder = encoding.GetDecoder();
            StringBuilder messageBuilder = new StringBuilder();

            bool lastByteIsCr = false;
            while (true)
            {
                var byteCount = await stream.ReadAsync(readByteBuffer, readOffset, readByteBuffer.Length - readOffset);
                if (byteCount == 0)
                {
                    throw new EndOfStreamException();
                }
                for (int i = readOffset; i < readOffset + byteCount; i++)
                {
                    // If meets CRLF, stop
                    if (lastByteIsCr && readByteBuffer[i] == '\n')
                    {
                        var byteCountToRead = i + 1 - readOffset;
                        while (byteCountToRead > 0)
                        {
                            decoder.Convert(
                                readByteBuffer,
                                readOffset,
                                byteCountToRead,
                                readCharBuffer,
                                0,
                                readCharBuffer.Length,
                                true,
                                out int bytesUsed,
                                out int charsUsed,
                                out bool completed);
                            messageBuilder.Append(readCharBuffer, 0, charsUsed);
                            byteCountToRead -= bytesUsed;
                        }

                        messageBuilder.Remove(messageBuilder.Length - 2, 2);
                        return messageBuilder.ToString();
                    }
                    else
                    {
                        lastByteIsCr = readByteBuffer[i] == '\r';
                    }
                }

                while (byteCount > 0)
                {
                    decoder.Convert(
                        readByteBuffer,
                        readOffset,
                        byteCount,
                        readCharBuffer,
                        0,
                        readCharBuffer.Length,
                        false,
                        out int bytesUsed,
                        out int charsUsed,
                        out bool completed);
                    byteCount -= bytesUsed;
                    messageBuilder.Append(readCharBuffer, 0, charsUsed);
                }

                readOffset = 0;
            }
        }

        private async Task ReplyAsync(FtpReplyCode code, string message)
        {
            StringBuilder stringBuilder = new StringBuilder(6 + message.Length);
            stringBuilder.Append((int)code);
            stringBuilder.Append(' ');
            stringBuilder.Append(message);
            stringBuilder.Append("\r\n");
            System.Diagnostics.Debug.WriteLine(stringBuilder.ToString());
            var bytesToSend = encoding.GetBytes(stringBuilder.ToString());
            await stream.WriteAsync(bytesToSend, 0, bytesToSend.Length);
        }

        private async Task ReplyMultilineAsync(FtpReplyCode code, string message)
        {
            message = message.Replace("\r", string.Empty);
            message = message.Replace("\n", "\r\n ");
            var stringToSend = string.Format("{0}-{1}\r\n{2} End\r\n", (int)code, message, (int)code);
            var bytesToSend = encoding.GetBytes(stringToSend);
            await stream.WriteAsync(bytesToSend, 0, bytesToSend.Length);
        }

        private string EncodePathName(string path)
        {
            return path.Replace("\r", "\r\0");
        }

        private string DecodePathName(string path)
        {
            return path.Replace("\r\0", "\r\0");
        }
    }
}