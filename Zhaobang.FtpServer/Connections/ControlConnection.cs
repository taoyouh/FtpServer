// <copyright file="ControlConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Exceptions;
using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Used to maintain the FTP control connection.
    /// </summary>
    internal class ControlConnection : IDisposable
    {
        private const int ReadByteBufferLength = 12;
        private const int ReadCharBufferLength = 12;

        private readonly IControlConnectionHost host;

        private readonly IPEndPoint remoteEndPoint;
        private readonly IPEndPoint localEndPoint;

        private readonly Encoding utf8Encoding = new UTF8Encoding(false);

        /// <summary>
        /// This should be available all time, but needs to check
        /// <see cref="LocalDataConnection.IsOpen"/> before usage.
        /// </summary>
        private readonly IDataConnection dataConnection;

        private Stream stream;

        private Encoding encoding;
        private string userName = string.Empty;
        private bool authenticated;

        private byte[] readByteBuffer = new byte[ReadByteBufferLength];
        private char[] readCharBuffer = new char[ReadCharBufferLength];
        private int readOffset = 0;

        private DataConnectionMode dataConnectionMode = DataConnectionMode.Active;
        private int userActiveProtocal = 1;
        private IPAddress userActiveIP;
        private int userActiveDataPort = 20;

        private IFileProvider fileProvider;

        /// <summary>
        /// Only stream mode is supported.
        /// </summary>
        private TransmissionMode transmissionMode = TransmissionMode.Stream;

        /// <summary>
        /// This is ignored.
        /// </summary>
        private DataType dataType = DataType.ASCII;

        private ListFormat listFormat = ListFormat.Unix;

        private bool useSecureDataConnection = false;

        private MLstCommandHandler mLstCommandHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlConnection"/> class to handle the FTP control connection.
        /// </summary>
        /// <param name="host">The <see cref="IControlConnectionHost"/> that creates the connection.</param>
        /// <param name="stream">The TCP stream of the connection.</param>
        /// <param name="remoteEndPoint">The IP end point of the client.</param>
        /// <param name="localEndPoint">The IP end point of the server.</param>
        /// <exception cref="ArgumentNullException">The argument passed in is null.</exception>
        internal ControlConnection(IControlConnectionHost host, Stream stream, IPEndPoint remoteEndPoint, IPEndPoint localEndPoint)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));

            this.remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this.localEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));

            dataConnection = host.DataConnector.GetDataConnection(localEndPoint.Address);

            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

            this.encoding = this.utf8Encoding;
        }

        private enum ListFormat
        {
            Unix,
            MsDos,
        }

        private enum TransmissionMode
        {
            Stream,
        }

        private enum DataType
        {
            ASCII,
            IMAGE,
        }

        private enum DataConnectionMode
        {
            Passive,
            Active,
            ExtendedPassive,
            ExtendedActive,
        }

        /// <summary>
        /// Defined in page 40 in RFC 959.
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
            FileBusy = 450,
            FileNoAccess = 550,
            FileSpaceInsufficient = 452,
            EnteringPassiveMode = 227,
            EnteringEpsvMode = 229,
            AboutToOpenDataConnection = 150,
            NameSystemType = 215,
            FileActionPendingInfo = 350,
            NotSupportedProtocal = 522,
            ProceedWithNegotiation = 234,
            UnsupportedProtectionLevel = 536,
        }

        /// <summary>
        /// Gets or sets the file provider instance for the current authenticated user. The value should be available if and only if <see cref="authenticated"/> is true.
        /// </summary>
        public IFileProvider FileProvider
        {
            get => this.fileProvider;
            set
            {
                if (this.fileProvider != value)
                {
                    this.fileProvider = value;
                    if (this.fileProvider is IMLstFileProvider fileProvider)
                    {
                        this.mLstCommandHandler = new MLstCommandHandler(fileProvider);
                    }
                    else
                    {
                        this.mLstCommandHandler = null;
                    }
                }
            }
        }

        /// <summary>
        /// Dispose of all the connections.
        /// </summary>
        public void Dispose()
        {
            stream.Dispose();
            dataConnection.Close();
        }

        /// <summary>
        /// Starts the control connection.
        /// </summary>
        /// <remarks>Can only be used once.</remarks>
        /// <param name="cancellationToken">Token to terminate the control connection.</param>
        /// <returns>The task that finishes when control connection is closed.</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            host.Tracer.TraceUserConnection(remoteEndPoint);
            try
            {
                await ReplyAsync(FtpReplyCode.ServiceReady, "FtpServer by Taoyou is now ready");

                while (true)
                {
                    var command = await ReadLineAsync();
                    if (command == null)
                    {
                        // Reaches end of stream
                        break;
                    }

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
                host.Tracer.TraceUserDisconnection(remoteEndPoint);
            }
        }

        private async Task ProcessCommandAsync(string message)
        {
            var messageSegs = message.Split(new[] { ' ' }, 2);
            var command = messageSegs[0];
            var parameter = messageSegs.Length < 2 ? string.Empty : messageSegs[1];

            host.Tracer.TraceCommand(command, remoteEndPoint);
            switch (command.ToUpper())
            {
                case "RNFR":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await CommandRnfrAsync(parameter);
                    return;
                case "RNTO":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await ReplyAsync(FtpReplyCode.BadSequence, "Should use RNFR first");
                    return;
                case "DELE":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await FileProvider.DeleteAsync(parameter);
                    await ReplyAsync(FtpReplyCode.FileActionOk, "Delete succeeded");
                    return;
                case "RMD":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await FileProvider.DeleteDirectoryAsync(parameter);
                    await ReplyAsync(FtpReplyCode.FileActionOk, "Directory deleted");
                    return;
                case "MKD":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await FileProvider.CreateDirectoryAsync(parameter);
                    await ReplyAsync(
                        FtpReplyCode.PathCreated,
                        string.Format(
                            "\"{0}\"",
                            FileProvider.GetWorkingDirectory().Replace("\"", "\"\"")));
                    return;
                case "PWD":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    await ReplyAsync(
                        FtpReplyCode.PathCreated,
                        string.Format(
                            "\"{0}\"",
                            FileProvider.GetWorkingDirectory().Replace("\"", "\"\"")));
                    return;
                case "SYST":
                    await ReplyAsync(FtpReplyCode.NameSystemType, "UNIX simulated by .NET Core");
                    return;
                case "FEAT":
                    var features = new List<string>
                    {
                        "UTF8",
                    };
                    if (this.mLstCommandHandler != null)
                    {
                        string featLine = this.mLstCommandHandler.GetFeatLine();
                        features.Add(featLine);
                    }
                    if (this.host.ControlConnectionSslFactory != null)
                    {
                        features.Add("AUTH TLS");
                        features.Add("PBSZ");
                        features.Add("PROT");
                    }
                    await ReplyMultilineAsync(FtpReplyCode.SystemStatus, $"Supports:\n{string.Join("\n", features)}");
                    return;
                case "OPTS":
                    if (parameter.ToUpperInvariant() == "UTF8 ON")
                    {
                        this.encoding = this.utf8Encoding;
                        await ReplyAsync(FtpReplyCode.CommandOkay, "UTF-8 is on");
                        return;
                    }
                    else if (parameter.ToUpperInvariant() == "UTF8 OFF")
                    {
                        encoding = Encoding.ASCII;
                        await ReplyAsync(FtpReplyCode.CommandOkay, "UTF-8 is off");
                        return;
                    }
                    else if (parameter.StartsWith("MLST", StringComparison.OrdinalIgnoreCase))
                    {
                        if (mLstCommandHandler != null)
                        {
                            if (mLstCommandHandler.HandleOpts(parameter))
                            {
                                await ReplyAsync(FtpReplyCode.CommandOkay, "MLST options set");
                                return;
                            }
                            else
                            {
                                await ReplyAsync(FtpReplyCode.SyntaxErrorInParametersOrArguments, "Syntax error");
                                return;
                            }
                        }
                        else
                        {
                            await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "MLST not supported");
                            return;
                        }
                    }
                    break;
                case "USER":
                    userName = parameter;
                    authenticated = false;
                    FileProvider = null;
                    await ReplyAsync(FtpReplyCode.NeedPassword, "Please input password");
                    return;
                case "PASS":
                    if (authenticated = host.Authenticator.Authenticate(userName, parameter))
                    {
                        await ReplyAsync(FtpReplyCode.UserLoggedIn, "Logged in");
                        FileProvider = host.FileManager.GetProvider(userName);
                    }
                    else
                    {
                        await ReplyAsync(FtpReplyCode.NotLoggedIn, "Failed to log in");
                        FileProvider = null;
                    }
                    return;
                case "PORT":
                    await CommandPortAsync(parameter);
                    return;
                case "EPRT":
                    await CommandEprtAsync(parameter);
                    return;
                case "PASV":
                    await CommandPasvAsync();
                    return;
                case "EPSV":
                    await CommandEpsvAsync(parameter);
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
                    if (parameter == "S")
                    {
                        transmissionMode = TransmissionMode.Stream;
                        await ReplyAsync(FtpReplyCode.CommandOkay, "In stream mode");
                    }
                    else
                    {
                        await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Unknown mode");
                    }

                    return;
                case "QUIT":
                    if (host.ControlConnectionSslFactory != null)
                    {
                        await host.ControlConnectionSslFactory.DisconnectAsync(stream);
                    }
                    throw new QuitRequestedException();
                case "RETR":
                    await CommandRetrAsync(parameter);
                    return;
                case "STOR":
                    await CommandStorAsync(parameter);
                    return;
                case "CWD":
                    if (!await EnsureAuthenticatedAsync())
                    {
                        return;
                    }
                    if (FileProvider.SetWorkingDirectory(parameter))
                    {
                        await ReplyAsync(FtpReplyCode.FileActionOk, FileProvider.GetWorkingDirectory());
                    }
                    else
                    {
                        await ReplyAsync(FtpReplyCode.FileNoAccess, "Path doesn't exist");
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
                case "AUTH":
                    await CommandAuthAsync(parameter);
                    return;
                case "PBSZ":
                    await this.CommandPbszAsync(parameter);
                    return;
                case "PROT":
                    await CommandProtAsync(parameter);
                    return;
                case "MLSD":
                    await CommandMLsDAsync(parameter);
                    return;
                case "MLST":
                    await CommandMLstAsync(parameter);
                    return;
            }
            await ReplyAsync(FtpReplyCode.CommandUnrecognized, "Can't recognize this command.");
        }

        private async Task CommandAuthAsync(string parameter)
        {
            if (this.host.ControlConnectionSslFactory == null)
            {
                await this.ReplyAsync(FtpReplyCode.NotImplemented, "Security extensions are not implemented.");
                return;
            }
            if (parameter != "TLS" && parameter != "SSL")
            {
                await this.ReplyAsync(FtpReplyCode.ParameterNotImplemented, "The requested security mechanism is not supported.");
                return;
            }
            await ReplyAsync(FtpReplyCode.ProceedWithNegotiation, "Authenticating");
            stream = await host.ControlConnectionSslFactory.UpgradeAsync(stream);
        }

        private async Task CommandPbszAsync(string parameter)
        {
            if (this.host.ControlConnectionSslFactory != null)
            {
                if (parameter == "0")
                {
                    await this.ReplyAsync(FtpReplyCode.CommandOkay, "PBSZ set to 0.");
                }
                else
                {
                    await this.ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Only PBSZ 0 is supported.");
                }
            }
            else
            {
                await this.ReplyAsync(FtpReplyCode.NotImplemented, "Security extensions are not implemented.");
            }
        }

        private async Task CommandProtAsync(string parameter)
        {
            switch (parameter)
            {
                case "C":
                    useSecureDataConnection = false;
                    break;
                case "S":
                case "E":
                    await ReplyAsync(FtpReplyCode.UnsupportedProtectionLevel, "Protection level not supported");
                    return;
                case "P":
                    if (!(dataConnection is ISslDataConnection))
                    {
                        await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Parameter not implemented");
                        return;
                    }
                    useSecureDataConnection = true;
                    break;
                default:
                    await ReplyAsync(FtpReplyCode.ParameterNotImplemented, "Parameter not implemented");
                    return;
            }
            await ReplyAsync(FtpReplyCode.CommandOkay, "Secure level set");
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
            await FileProvider.RenameAsync(fromPath, toPath);
            await ReplyAsync(FtpReplyCode.FileActionOk, "Rename succeeded");
        }

        private async Task CommandListAsync(string parameter)
        {
            if (!await EnsureAuthenticatedAsync())
            {
                return;
            }
            MemoryStream stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            writer.NewLine = "\r\n";

            // Windows Explorer changes working directory and lists the current directory, and the parameter is always empty.
            // Some FTP clients (such as Material Files) adds a "-a" flag before the path, but does not escape spaces in the path like we do in a shell.
            if (parameter.StartsWith("-a "))
            {
                parameter = parameter.Substring(3);
            }
            try
            {
                var listing = await fileProvider.GetListingAsync(parameter);
                await writer.WriteLineAsync($"Total {listing.Count()}");
                foreach (var item in listing)
                {
                    DateTime lastWriteTime = item.LastWriteTime.ToLocalTime();
                    if (listFormat == ListFormat.Unix)
                    {
                        await writer.WriteLineAsync(
                            string.Format(
                                "{0}{1}{1}{1}   1 owner   group {2,15} {3} {4}",
                                item.IsDirectory ? 'd' : '-',
                                item.IsReadOnly ? "r-x" : "rwx",
                                item.Length,
                                lastWriteTime.ToString(
                                    lastWriteTime.Year == DateTime.Now.Year ?
                                    "MMM dd HH:mm" : "MMM dd  yyyy", CultureInfo.InvariantCulture),
                                item.Name));
                    }
                    else if (listFormat == ListFormat.MsDos)
                    {
                        if (item.IsDirectory)
                        {
                            await writer.WriteLineAsync(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0:MM-dd-yy  hh:mmtt} {1,20} {2}",
                                    lastWriteTime.ToLocalTime(),
                                    item.Length,
                                    item.Name));
                        }
                        else
                        {
                            await writer.WriteLineAsync(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0:MM-dd-yy  hh:mmtt}       {1,-14} {2}",
                                    lastWriteTime.ToLocalTime(),
                                    "<DIR>",
                                    item.Name));
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Can't only use Unix or MS-DOS listing format.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (await this.HandleExceptionAsync(ex))
                {
                    return;
                }
                else
                {
                    throw;
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
            if (!await EnsureAuthenticatedAsync())
            {
                return;
            }
            MemoryStream stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            writer.NewLine = "\r\n";
            try
            {
                var nameListing = await FileProvider.GetNameListingAsync(parameter);
                foreach (var item in nameListing)
                {
                    await writer.WriteLineAsync(item);
                }
            }
            catch (Exception ex)
            {
                if (await this.HandleExceptionAsync(ex))
                {
                    return;
                }
                else
                {
                    throw;
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

        private async Task CommandStorAsync(string parameter)
        {
            if (!await EnsureAuthenticatedAsync())
            {
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

            try
            {
                using (Stream fileStream = await FileProvider.CreateFileForWriteAsync(parameter))
                {
                    await OpenDataConnectionAsync();
                    await dataConnection.RecieveAsync(fileStream);
                    await fileStream.FlushAsync();
                }
            }
            catch (FileBusyException ex)
            {
                await ReplyAsync(FtpReplyCode.FileBusy, string.Format("Temporarily unavailable: {0}", ex.Message));
                return;
            }
            catch (FileSpaceInsufficientException ex)
            {
                await ReplyAsync(FtpReplyCode.FileSpaceInsufficient, string.Format("Writing file denied: {0}", ex.Message));
                return;
            }

            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "File has been recieved");
            return;
        }

        private async Task CommandRetrAsync(string parameter)
        {
            if (!await EnsureAuthenticatedAsync())
            {
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

            try
            {
                using (Stream fileStream = await FileProvider.OpenFileForReadAsync(parameter))
                {
                    await OpenDataConnectionAsync();
                    await dataConnection.SendAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                if (await this.HandleExceptionAsync(ex))
                {
                    return;
                }
                else
                {
                    throw;
                }
            }
            await dataConnection.DisconnectAsync();
            await ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "File has been sent");
            return;
        }

        private async Task CommandPasvAsync()
        {
            var localEP = dataConnection.Listen();
            IPAddress ipv4Address;
            if (localEP.Address.IsIPv4MappedToIPv6)
            {
                ipv4Address = localEP.Address.MapToIPv4();
            }
            else
            {
                ipv4Address = localEP.Address;
            }

            byte[] ipBytes = ipv4Address.GetAddressBytes();
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
                userActiveProtocal = 1;
                dataConnectionMode = DataConnectionMode.Active;
                await dataConnection.ConnectActiveAsync(userActiveIP, userActiveDataPort, userActiveProtocal);
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

        private async Task CommandEprtAsync(string parameter)
        {
            if (string.IsNullOrEmpty(parameter))
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, parameter is empty");
                return;
            }

            var seperator = parameter[0];
            var paramSegs = parameter.Split(seperator);

            if (paramSegs.Length != 5)
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Syntax error, count of segments incorrect");
                return;
            }

            int remoteProtocal;
            if (!int.TryParse(paramSegs[1], out remoteProtocal))
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "Protocal ID incorrect");
                return;
            }

            IPAddress remoteIP;
            int remotePort;
            try
            {
                remoteIP = IPAddress.Parse(paramSegs[2]);
                remotePort = int.Parse(paramSegs[3]);
            }
            catch (Exception)
            {
                await ReplyAsync(
                    FtpReplyCode.SyntaxErrorInParametersOrArguments,
                    "IP address or port number incorrect.");
                return;
            }
            userActiveDataPort = remotePort;
            userActiveIP = remoteIP;
            userActiveProtocal = remoteProtocal;

            dataConnectionMode = DataConnectionMode.ExtendedPassive;
            try
            {
                await dataConnection.ConnectActiveAsync(userActiveIP, userActiveDataPort, userActiveProtocal);
            }
            catch (NotSupportedException)
            {
                var supportedProtocalString =
                    string.Join(",", dataConnection.SupportedActiveProtocal.Select(x => x.ToString()));
                await ReplyAsync(FtpReplyCode.NotSupportedProtocal, $"Protocal not supported, use({supportedProtocalString})");
                return;
            }

            await ReplyAsync(FtpReplyCode.CommandOkay, "Data connection established");
        }

        private async Task CommandEpsvAsync(string parameter)
        {
            int port;
            try
            {
                if (string.IsNullOrEmpty(parameter))
                {
                    port = dataConnection.Listen().Port;
                }
                else
                {
                    var protocal = int.Parse(parameter);
                    port = dataConnection.ExtendedListen(protocal);
                }
            }
            catch (FormatException)
            {
                await ReplyAsync(FtpReplyCode.SyntaxErrorInParametersOrArguments, "Protocal ID incorrect.");
                return;
            }
            catch (NotSupportedException)
            {
                var supportedProtocalString =
                    string.Join(",", dataConnection.SupportedPassiveProtocal.Select(x => x.ToString()));
                await ReplyAsync(FtpReplyCode.NotSupportedProtocal, $"Protocal not supported, use({supportedProtocalString})");
                return;
            }
            dataConnectionMode = DataConnectionMode.ExtendedPassive;
            await ReplyAsync(
                FtpReplyCode.EnteringEpsvMode,
                string.Format("Entering extended passive mode. (|||{0}|)", port));
        }

        private async Task CommandMLsDAsync(string parameter)
        {
            if (!await this.EnsureAuthenticatedAsync())
            {
                return;
            }
            else if (this.mLstCommandHandler == null)
            {
                await ReplyAsync(FtpReplyCode.CommandUnrecognized, "Can't recognize this command.");
                return;
            }

            var stream = new MemoryStream();
            try
            {
                await this.mLstCommandHandler.FormatChildItemsAsync(parameter, stream);
            }
            catch (Exception ex)
            {
                if (await this.HandleExceptionAsync(ex))
                {
                    return;
                }
                else
                {
                    throw;
                }
            }

            stream.Seek(0, SeekOrigin.Begin);

            await this.OpenDataConnectionAsync();
            await this.dataConnection.SendAsync(stream);
            await this.dataConnection.DisconnectAsync();
            await this.ReplyAsync(FtpReplyCode.SuccessClosingDataConnection, "Listing has been sent");
        }

        private async Task CommandMLstAsync(string parameter)
        {
            if (!await EnsureAuthenticatedAsync())
            {
                return;
            }
            else if (this.mLstCommandHandler == null)
            {
                await ReplyAsync(FtpReplyCode.CommandUnrecognized, "Can't recognize this command.");
            }

            try
            {
                string formattedItem = await this.mLstCommandHandler.GetFormattedItemAsync(parameter);
                await this.ReplyMultilineAsync(FtpReplyCode.FileActionOk, string.Format(
                    CultureInfo.InvariantCulture, "Listing item\r\n{0}", formattedItem));
            }
            catch (Exception ex)
            {
                if (await this.HandleExceptionAsync(ex))
                {
                    return;
                }
                else
                {
                    throw;
                }
            }
            return;
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
                switch (dataConnectionMode)
                {
                    case DataConnectionMode.Active:
                    case DataConnectionMode.ExtendedActive:
                        await dataConnection.ConnectActiveAsync(userActiveIP, userActiveDataPort, userActiveProtocal);
                        break;
                    case DataConnectionMode.Passive:
                    case DataConnectionMode.ExtendedPassive:
                        await dataConnection.AcceptAsync();
                        break;
                }
            }
            if (useSecureDataConnection)
            {
                await (dataConnection as ISslDataConnection).UpgradeToSslAsync();
            }
        }

        /// <summary>
        /// Reads a line from network stream partitioned by CRLF.
        /// </summary>
        /// <returns>The line read with CRLF trimmed.</returns>
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
                    return null;
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
            host.Tracer.TraceReply(((int)code).ToString(), remoteEndPoint);
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
            host.Tracer.TraceReply(((int)code).ToString(), remoteEndPoint);
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

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (!authenticated)
            {
                await ReplyAsync(FtpReplyCode.NotLoggedIn, "You need to log in first");
                return false;
            }

            return true;
        }

        private async Task<bool> HandleExceptionAsync(Exception ex)
        {
            if (ex is FileBusyException ex2)
            {
                await ReplyAsync(FtpReplyCode.FileBusy, string.Format("File temporarily unavailable: {0}", ex2.Message));
                return true;
            }
            else if (ex is FileNoAccessException ex3)
            {
                await ReplyAsync(FtpReplyCode.FileNoAccess, string.Format("File access denied: {0}", ex3.Message));
                return true;
            }
            else if (ex is ArgumentException ex4)
            {
                // RFC 3659 7.2.1: Giving a pathname that exists but is not a directory
                // as the argument to a MLSD command generates a 501 reply.
                await ReplyAsync(FtpReplyCode.SyntaxErrorInParametersOrArguments, string.Format("Syntax error: {0}", ex4.Message));
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
