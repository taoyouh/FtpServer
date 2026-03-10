// <copyright file="MLstCommandHandler.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Handler for MLst and MLsD commands and corresponding OPTS and FEAT commands.
    /// </summary>
    internal class MLstCommandHandler
    {
        private readonly IMLstFileProvider fileProvider;
        private readonly HashSet<Fact> selectedFacts = new HashSet<Fact>
        {
            Fact.Size,
            Fact.Type,
            Fact.Perm,
            Fact.Modify,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="MLstCommandHandler"/> class.
        /// </summary>
        /// <param name="fileProvider">The file provider to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileProvider"/> is null.</exception>
        public MLstCommandHandler(IMLstFileProvider fileProvider)
        {
            this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        }

        private enum Fact
        {
            Size,
            Type,
            Perm,
            Modify,
        }

        /// <summary>
        /// Gets response for the FEAT command.
        /// </summary>
        /// <returns>The FEAT response.</returns>
        public string GetFeatLine()
        {
            var result = new StringBuilder();
            result.Append("MLST ");
            foreach (Fact availableFact in Enum.GetValues(typeof(Fact)))
            {
                result.Append(availableFact.ToString());
                if (this.selectedFacts.Contains(availableFact))
                {
                    result.Append("*");
                }
                result.Append(";");
            }
            return result.ToString();
        }

        /// <summary>
        /// Handles the OPTS command for MLST.
        /// </summary>
        /// <param name="parameter">The parameter string from the OPTS command.</param>
        /// <returns>True if the command was handled, false otherwise.</returns>
        public bool HandleOpts(string parameter)
        {
            if (parameter.Equals("MLST", StringComparison.OrdinalIgnoreCase))
            {
                selectedFacts.Clear();
                return true;
            }

            if (parameter.StartsWith("MLST ", StringComparison.OrdinalIgnoreCase))
            {
                selectedFacts.Clear();
                string[] facts = parameter.Substring(5).Split(';');
                foreach (string fact in facts)
                {
                    if (TryParseFact(fact, out Fact parsedFact))
                    {
                        selectedFacts.Add(parsedFact);
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a formatted item string for the MLsT command.
        /// </summary>
        /// <param name="path">The path of the item to format.</param>
        /// <returns>A task representing the asynchronous operation with the formatted item string.</returns>
        public async Task<string> GetFormattedItemAsync(string path)
        {
            FileSystemEntry entry = await this.fileProvider.GetItemAsync(path);

            return this.FormatEntry(entry);
        }

        /// <summary>
        /// Formats and writes child items to the output stream for the MLsD command.
        /// </summary>
        /// <param name="path">The path of the directory to list.</param>
        /// <param name="outputStream">The output stream to write the formatted items to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task FormatChildItemsAsync(string path, Stream outputStream)
        {
            IEnumerable<FileSystemEntry> entries = await this.fileProvider.GetChildItems(path);
            using (var writer = new StreamWriter(outputStream, new UTF8Encoding(false), -1, true))
            {
                foreach (FileSystemEntry entry in entries)
                {
                    await writer.WriteLineAsync(FormatEntry(entry));
                }
            }
        }

        private static bool TryParseFact(string factName, out Fact fact)
        {
            switch (factName.ToUpperInvariant())
            {
                case "SIZE":
                    fact = Fact.Size;
                    return true;
                case "TYPE":
                    fact = Fact.Type;
                    return true;
                case "PERM":
                    fact = Fact.Perm;
                    return true;
                case "MODIFY":
                    fact = Fact.Modify;
                    return true;
                default:
                    fact = default;
                    return false;
            }
        }

        private string FormatEntry(FileSystemEntry entry)
        {
            var result = new StringBuilder();

            if (selectedFacts.Contains(Fact.Size))
            {
                result.Append(string.Format(CultureInfo.InvariantCulture, "Size={0};", entry.Length));
            }

            if (selectedFacts.Contains(Fact.Type))
            {
                result.Append(string.Format(CultureInfo.InvariantCulture, "Type={0};", entry.IsDirectory ? "dir" : "file"));
            }

            if (selectedFacts.Contains(Fact.Perm))
            {
                string perm;
                if (entry.IsDirectory)
                {
                    perm = entry.IsReadOnly ? "defl" : "cdeflmp";
                }
                else
                {
                    perm = entry.IsReadOnly ? "dfr" : "adfrw";
                }
                result.Append(string.Format(CultureInfo.InvariantCulture, "Perm={0};", perm));
            }

            if (selectedFacts.Contains(Fact.Modify))
            {
                result.Append(string.Format(CultureInfo.InvariantCulture, "Modify={0:yyyyMMddHHmmss.fff};", entry.LastWriteTime.ToUniversalTime()));
            }

            result.Append(' ');
            result.Append(entry.Name);
            return result.ToString();
        }
    }
}
