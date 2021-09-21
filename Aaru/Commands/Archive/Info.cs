// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Info.cs
// Author(s)      : Michael Drüing <michael@drueing.de>
//
// Component      : Commands.
//
// --[ Description ] ----------------------------------------------------------
//
//     Implements the 'info' command.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2021 Michael Drüing
// Copyright © 2011-2021 Natalia Portillo
// ****************************************************************************/

using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.Console;
using Aaru.Core;

namespace Aaru.Commands.Archive
{
    internal sealed class ArchiveInfoCommand : Command
    {
        public ArchiveInfoCommand() : base("info",
                                         "Identifies an archive file and shows information about it.")
        {
            AddArgument(new Argument<string>
            {
                Arity       = ArgumentArity.ExactlyOne,
                Description = "Archive file path",
                Name        = "archive-path"
            });

            Handler = CommandHandler.Create(GetType().GetMethod(nameof(Invoke)));
        }

        public static IArchive Detect(IFilter archiveFilter)
        {
            PluginBase plugins = GetPluginBase.Instance;

            foreach(IArchive archivePlugin in plugins.Archives.Values)
            {
                try
                {
                    AaruConsole.DebugWriteLine("Archive detection", "Trying plugin {0}", archivePlugin.Name);

                    using (Stream s = archiveFilter.GetDataForkStream())
                    {
                        if(archivePlugin.Identify(s))
                            return archivePlugin;
                    }
                }
                #pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
                catch
                {
                    // ignored
                }
            }

            return null;
        }
        public static int Invoke(bool debug, bool verbose, string archivePath)
        {
            MainClass.PrintCopyright();

            if(debug)
                AaruConsole.DebugWriteLineEvent += System.Console.Error.WriteLine;

            if(verbose)
                AaruConsole.VerboseWriteLineEvent += System.Console.WriteLine;

            Statistics.AddCommand("archive-info");

            AaruConsole.DebugWriteLine("Info command", "--debug={0}", debug);
            AaruConsole.DebugWriteLine("Info command", "--input={0}", archivePath);
            AaruConsole.DebugWriteLine("Info command", "--verbose={0}", verbose);

            FiltersList filtersList = new FiltersList();
            IFilter inputFilter = filtersList.GetFilter(archivePath);

            if(inputFilter == null)
            {
                AaruConsole.ErrorWriteLine("Cannot open specified file.");

                return (int)ErrorNumber.CannotOpenFile;
            }

            try
            {
                IArchive archive = Detect(inputFilter);

                if(archive == null)
                {
                    AaruConsole.WriteLine("Archive not identified.");

                    return (int)ErrorNumber.UnrecognizedFormat;
                }

                AaruConsole.WriteLine("Archive identified by {0} ({1}).", archive.Name, archive.Id);
                AaruConsole.WriteLine();

                try
                {
                    using (Stream s = inputFilter.GetDataForkStream())
                    {
                        archive.Open(s);
                        if (!archive.IsOpened())
                        {
                            AaruConsole.WriteLine("Unable to open archive");
                            AaruConsole.WriteLine("No error given");

                            return (int)ErrorNumber.CannotOpenFormat;
                        }

                        // TODO: Print archive info
                        
                        archive.Close();
                    }
                }
                catch(Exception ex)
                {
                    AaruConsole.ErrorWriteLine("Unable to open archive");
                    AaruConsole.ErrorWriteLine("Error: {0}", ex.Message);
                    AaruConsole.DebugWriteLine("Archive-info command", "Stack trace: {0}", ex.StackTrace);

                    return (int)ErrorNumber.CannotOpenFormat;
                }
            }
            catch(Exception ex)
            {
                AaruConsole.ErrorWriteLine($"Error reading file: {ex.Message}");
                AaruConsole.DebugWriteLine("Archive-info command", ex.StackTrace);

                return (int)ErrorNumber.UnexpectedException;
            }

            return (int)ErrorNumber.NoError;
        }
    }
}
