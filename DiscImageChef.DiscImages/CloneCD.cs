// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : CloneCD.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Disc image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Manages CloneCD disc images.
//
// --[ License ] --------------------------------------------------------------
//
//     This library is free software; you can redistribute it and/or modify
//     it under the terms of the GNU Lesser General Public License as
//     published by the Free Software Foundation; either version 2.1 of the
//     License, or (at your option) any later version.
//
//     This library is distributed in the hope that it will be useful, but
//     WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//     Lesser General Public License for more details.
//
//     You should have received a copy of the GNU Lesser General Public
//     License along with this library; if not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2018 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DiscImageChef.Checksums;
using DiscImageChef.CommonTypes;
using DiscImageChef.Console;
using DiscImageChef.Decoders.CD;
using DiscImageChef.Filters;

namespace DiscImageChef.DiscImages
{
    public class CloneCd : ImagePlugin
    {
        const string CCD_IDENTIFIER = @"^\s*\[CloneCD\]";
        const string DISC_IDENTIFIER = @"^\s*\[Disc\]";
        const string SESSION_IDENTIFIER = @"^\s*\[Session\s*(?<number>\d+)\]";
        const string ENTRY_IDENTIFIER = @"^\s*\[Entry\s*(?<number>\d+)\]";
        const string TRACK_IDENTIFIER = @"^\s*\[TRACK\s*(?<number>\d+)\]";
        const string CDTEXT_IDENTIFIER = @"^\s*\[CDText\]";
        const string CCD_VERSION = @"^\s*Version\s*=\s*(?<value>\d+)";
        const string DISC_ENTRIES = @"^\s*TocEntries\s*=\s*(?<value>\d+)";
        const string DISC_SESSIONS = @"^\s*Sessions\s*=\s*(?<value>\d+)";
        const string DISC_SCRAMBLED = @"^\s*DataTracksScrambled\s*=\s*(?<value>\d+)";
        const string CDTEXT_LENGTH = @"^\s*CDTextLength\s*=\s*(?<value>\d+)";
        const string DISC_CATALOG = @"^\s*CATALOG\s*=\s*(?<value>\w+)";
        const string SESSION_PREGAP = @"^\s*PreGapMode\s*=\s*(?<value>\d+)";
        const string SESSION_SUBCHANNEL = @"^\s*PreGapSubC\s*=\s*(?<value>\d+)";
        const string ENTRY_SESSION = @"^\s*Session\s*=\s*(?<value>\d+)";
        const string ENTRY_POINT = @"^\s*Point\s*=\s*(?<value>[\w+]+)";
        const string ENTRY_ADR = @"^\s*ADR\s*=\s*(?<value>\w+)";
        const string ENTRY_CONTROL = @"^\s*Control\s*=\s*(?<value>\w+)";
        const string ENTRY_TRACKNO = @"^\s*TrackNo\s*=\s*(?<value>\d+)";
        const string ENTRY_AMIN = @"^\s*AMin\s*=\s*(?<value>\d+)";
        const string ENTRY_ASEC = @"^\s*ASec\s*=\s*(?<value>\d+)";
        const string ENTRY_AFRAME = @"^\s*AFrame\s*=\s*(?<value>\d+)";
        const string ENTRY_ALBA = @"^\s*ALBA\s*=\s*(?<value>-?\d+)";
        const string ENTRY_ZERO = @"^\s*Zero\s*=\s*(?<value>\d+)";
        const string ENTRY_PMIN = @"^\s*PMin\s*=\s*(?<value>\d+)";
        const string ENTRY_PSEC = @"^\s*PSec\s*=\s*(?<value>\d+)";
        const string ENTRY_PFRAME = @"^\s*PFrame\s*=\s*(?<value>\d+)";
        const string ENTRY_PLBA = @"^\s*PLBA\s*=\s*(?<value>\d+)";
        const string CDTEXT_ENTRIES = @"^\s*Entries\s*=\s*(?<value>\d+)";
        const string CDTEXT_ENTRY = @"^\s*Entry\s*(?<number>\d+)\s*=\s*(?<value>([0-9a-fA-F]+\s*)+)";
        string catalog; // TODO: Use it

        Filter ccdFilter;
        byte[] cdtext;
        StreamReader cueStream;
        Filter dataFilter;
        Stream dataStream;
        byte[] fulltoc;
        Dictionary<uint, ulong> offsetmap;
        List<Partition> partitions;
        bool scrambled;
        List<Session> sessions;
        Filter subFilter;
        Stream subStream;
        List<Track> tracks;

        public CloneCd()
        {
            Name = "CloneCD";
            PluginUuid = new Guid("EE9C2975-2E79-427A-8EE9-F86F19165784");
            ImageInfo = new ImageInfo
            {
                ReadableSectorTags = new List<SectorTagType>(),
                ReadableMediaTags = new List<MediaTagType>(),
                ImageHasPartitions = true,
                ImageHasSessions = true,
                ImageVersion = null,
                ImageApplicationVersion = null,
                ImageName = null,
                ImageCreator = null,
                MediaManufacturer = null,
                MediaModel = null,
                MediaPartNumber = null,
                MediaSequence = 0,
                LastMediaSequence = 0,
                DriveManufacturer = null,
                DriveModel = null,
                DriveSerialNumber = null,
                DriveFirmwareRevision = null
            };
        }

        public override bool IdentifyImage(Filter imageFilter)
        {
            ccdFilter = imageFilter;

            try
            {
                imageFilter.GetDataForkStream().Seek(0, SeekOrigin.Begin);
                byte[] testArray = new byte[512];
                imageFilter.GetDataForkStream().Read(testArray, 0, 512);
                imageFilter.GetDataForkStream().Seek(0, SeekOrigin.Begin);
                // Check for unexpected control characters that shouldn't be present in a text file and can crash this plugin
                bool twoConsecutiveNulls = false;
                for(int i = 0; i < 512; i++)
                {
                    if(i >= imageFilter.GetDataForkStream().Length) break;

                    if(testArray[i] == 0)
                    {
                        if(twoConsecutiveNulls) return false;

                        twoConsecutiveNulls = true;
                    }
                    else twoConsecutiveNulls = false;

                    if(testArray[i] < 0x20 && testArray[i] != 0x0A && testArray[i] != 0x0D && testArray[i] != 0x00)
                        return false;
                }

                cueStream = new StreamReader(ccdFilter.GetDataForkStream());

                string line = cueStream.ReadLine();

                Regex hdr = new Regex(CCD_IDENTIFIER);

                Match hdm = hdr.Match(line ?? throw new InvalidOperationException());

                return hdm.Success;
            }
            catch(Exception ex)
            {
                DicConsole.ErrorWriteLine("Exception trying to identify image file {0}", ccdFilter);
                DicConsole.ErrorWriteLine("Exception: {0}", ex.Message);
                DicConsole.ErrorWriteLine("Stack trace: {0}", ex.StackTrace);
                return false;
            }
        }

        public override bool OpenImage(Filter imageFilter)
        {
            if(imageFilter == null) return false;

            ccdFilter = imageFilter;

            try
            {
                imageFilter.GetDataForkStream().Seek(0, SeekOrigin.Begin);
                cueStream = new StreamReader(imageFilter.GetDataForkStream());
                int lineNumber = 0;

                Regex ccdIdRegex = new Regex(CCD_IDENTIFIER);
                Regex discIdRegex = new Regex(DISC_IDENTIFIER);
                Regex sessIdRegex = new Regex(SESSION_IDENTIFIER);
                Regex entryIdRegex = new Regex(ENTRY_IDENTIFIER);
                Regex trackIdRegex = new Regex(TRACK_IDENTIFIER);
                Regex cdtIdRegex = new Regex(CDTEXT_IDENTIFIER);
                Regex ccdVerRegex = new Regex(CCD_VERSION);
                Regex discEntRegex = new Regex(DISC_ENTRIES);
                Regex discSessRegex = new Regex(DISC_SESSIONS);
                Regex discScrRegex = new Regex(DISC_SCRAMBLED);
                Regex cdtLenRegex = new Regex(CDTEXT_LENGTH);
                Regex discCatRegex = new Regex(DISC_CATALOG);
                Regex sessPregRegex = new Regex(SESSION_PREGAP);
                Regex sessSubcRegex = new Regex(SESSION_SUBCHANNEL);
                Regex entSessRegex = new Regex(ENTRY_SESSION);
                Regex entPointRegex = new Regex(ENTRY_POINT);
                Regex entAdrRegex = new Regex(ENTRY_ADR);
                Regex entCtrlRegex = new Regex(ENTRY_CONTROL);
                Regex entTnoRegex = new Regex(ENTRY_TRACKNO);
                Regex entAMinRegex = new Regex(ENTRY_AMIN);
                Regex entASecRegex = new Regex(ENTRY_ASEC);
                Regex entAFrameRegex = new Regex(ENTRY_AFRAME);
                Regex entAlbaRegex = new Regex(ENTRY_ALBA);
                Regex entZeroRegex = new Regex(ENTRY_ZERO);
                Regex entPMinRegex = new Regex(ENTRY_PMIN);
                Regex entPSecRegex = new Regex(ENTRY_PSEC);
                Regex entPFrameRegex = new Regex(ENTRY_PFRAME);
                Regex entPlbaRegex = new Regex(ENTRY_PLBA);
                Regex cdtEntsRegex = new Regex(CDTEXT_ENTRIES);
                Regex cdtEntRegex = new Regex(CDTEXT_ENTRY);

                bool inCcd = false;
                bool inDisk = false;
                bool inSession = false;
                bool inEntry = false;
                bool inTrack = false;
                bool inCdText = false;
                MemoryStream cdtMs = new MemoryStream();
                int minSession = int.MaxValue;
                int maxSession = int.MinValue;
                FullTOC.TrackDataDescriptor currentEntry = new FullTOC.TrackDataDescriptor();
                List<FullTOC.TrackDataDescriptor> entries = new List<FullTOC.TrackDataDescriptor>();
                scrambled = false;
                catalog = null;

                while(cueStream.Peek() >= 0)
                {
                    lineNumber++;
                    string line = cueStream.ReadLine();

                    Match ccdIdMatch = ccdIdRegex.Match(line);
                    Match discIdMatch = discIdRegex.Match(line);
                    Match sessIdMatch = sessIdRegex.Match(line);
                    Match entryIdMatch = entryIdRegex.Match(line);
                    Match trackIdMatch = trackIdRegex.Match(line);
                    Match cdtIdMatch = cdtIdRegex.Match(line);

                    // [CloneCD]
                    if(ccdIdMatch.Success)
                    {
                        if(inDisk || inSession || inEntry || inTrack || inCdText)
                            throw new
                                FeatureUnsupportedImageException($"Found [CloneCD] out of order in line {lineNumber}");

                        inCcd = true;
                        inDisk = false;
                        inSession = false;
                        inEntry = false;
                        inTrack = false;
                        inCdText = false;
                    }
                    else if(discIdMatch.Success || sessIdMatch.Success || entryIdMatch.Success ||
                            trackIdMatch.Success || cdtIdMatch.Success)
                    {
                        if(inEntry)
                        {
                            entries.Add(currentEntry);
                            currentEntry = new FullTOC.TrackDataDescriptor();
                        }

                        inCcd = false;
                        inDisk = discIdMatch.Success;
                        inSession = sessIdMatch.Success;
                        inEntry = entryIdMatch.Success;
                        inTrack = trackIdMatch.Success;
                        inCdText = cdtIdMatch.Success;
                    }
                    else
                    {
                        if(inCcd)
                        {
                            Match ccdVerMatch = ccdVerRegex.Match(line);

                            if(!ccdVerMatch.Success) continue;

                            DicConsole.DebugWriteLine("CloneCD plugin", "Found Version at line {0}", lineNumber);

                            ImageInfo.ImageVersion = ccdVerMatch.Groups["value"].Value;
                            if(ImageInfo.ImageVersion != "2" && ImageInfo.ImageVersion != "3")
                                DicConsole
                                    .ErrorWriteLine("(CloneCD plugin): Warning! Unknown CCD image version {0}, may not work!",
                                                    ImageInfo.ImageVersion);
                        }
                        else if(inDisk)
                        {
                            Match discEntMatch = discEntRegex.Match(line);
                            Match discSessMatch = discSessRegex.Match(line);
                            Match discScrMatch = discScrRegex.Match(line);
                            Match cdtLenMatch = cdtLenRegex.Match(line);
                            Match discCatMatch = discCatRegex.Match(line);

                            if(discEntMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found TocEntries at line {0}", lineNumber);
                            else if(discSessMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Sessions at line {0}", lineNumber);
                            else if(discScrMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found DataTracksScrambled at line {0}",
                                                          lineNumber);
                                scrambled |= discScrMatch.Groups["value"].Value == "1";
                            }
                            else if(cdtLenMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found CDTextLength at line {0}",
                                                          lineNumber);
                            else if(discCatMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Catalog at line {0}", lineNumber);
                                catalog = discCatMatch.Groups["value"].Value;
                            }
                        }
                        // TODO: Do not suppose here entries come sorted
                        else if(inCdText)
                        {
                            Match cdtEntsMatch = cdtEntsRegex.Match(line);
                            Match cdtEntMatch = cdtEntRegex.Match(line);

                            if(cdtEntsMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found CD-Text Entries at line {0}",
                                                          lineNumber);
                            else if(cdtEntMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found CD-Text Entry at line {0}",
                                                          lineNumber);
                                string[] bytes = cdtEntMatch.Groups["value"].Value.Split(new[] {' '},
                                                                                         StringSplitOptions
                                                                                             .RemoveEmptyEntries);
                                foreach(string byt in bytes) cdtMs.WriteByte(Convert.ToByte(byt, 16));
                            }
                        }
                        // Is this useful?
                        else if(inSession)
                        {
                            Match sessPregMatch = sessPregRegex.Match(line);
                            Match sessSubcMatch = sessSubcRegex.Match(line);

                            if(sessPregMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PreGapMode at line {0}", lineNumber);
                            else if(sessSubcMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PreGapSubC at line {0}", lineNumber);
                        }
                        else if(inEntry)
                        {
                            Match entSessMatch = entSessRegex.Match(line);
                            Match entPointMatch = entPointRegex.Match(line);
                            Match entAdrMatch = entAdrRegex.Match(line);
                            Match entCtrlMatch = entCtrlRegex.Match(line);
                            Match entTnoMatch = entTnoRegex.Match(line);
                            Match entAMinMatch = entAMinRegex.Match(line);
                            Match entASecMatch = entASecRegex.Match(line);
                            Match entAFrameMatch = entAFrameRegex.Match(line);
                            Match entAlbaMatch = entAlbaRegex.Match(line);
                            Match entZeroMatch = entZeroRegex.Match(line);
                            Match entPMinMatch = entPMinRegex.Match(line);
                            Match entPSecMatch = entPSecRegex.Match(line);
                            Match entPFrameMatch = entPFrameRegex.Match(line);
                            Match entPlbaMatch = entPlbaRegex.Match(line);

                            if(entSessMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Session at line {0}", lineNumber);
                                currentEntry.SessionNumber = Convert.ToByte(entSessMatch.Groups["value"].Value, 10);
                                if(currentEntry.SessionNumber < minSession) minSession = currentEntry.SessionNumber;
                                if(currentEntry.SessionNumber > maxSession) maxSession = currentEntry.SessionNumber;
                            }
                            else if(entPointMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Point at line {0}", lineNumber);
                                currentEntry.POINT = Convert.ToByte(entPointMatch.Groups["value"].Value, 16);
                            }
                            else if(entAdrMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found ADR at line {0}", lineNumber);
                                currentEntry.ADR = Convert.ToByte(entAdrMatch.Groups["value"].Value, 16);
                            }
                            else if(entCtrlMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Control at line {0}", lineNumber);
                                currentEntry.CONTROL = Convert.ToByte(entCtrlMatch.Groups["value"].Value, 16);
                            }
                            else if(entTnoMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found TrackNo at line {0}", lineNumber);
                                currentEntry.TNO = Convert.ToByte(entTnoMatch.Groups["value"].Value, 10);
                            }
                            else if(entAMinMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found AMin at line {0}", lineNumber);
                                currentEntry.Min = Convert.ToByte(entAMinMatch.Groups["value"].Value, 10);
                            }
                            else if(entASecMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found ASec at line {0}", lineNumber);
                                currentEntry.Sec = Convert.ToByte(entASecMatch.Groups["value"].Value, 10);
                            }
                            else if(entAFrameMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found AFrame at line {0}", lineNumber);
                                currentEntry.Frame = Convert.ToByte(entAFrameMatch.Groups["value"].Value, 10);
                            }
                            else if(entAlbaMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found ALBA at line {0}", lineNumber);
                            else if(entZeroMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found Zero at line {0}", lineNumber);
                                currentEntry.Zero = Convert.ToByte(entZeroMatch.Groups["value"].Value, 10);
                                currentEntry.HOUR = (byte)((currentEntry.Zero & 0xF0) >> 4);
                                currentEntry.PHOUR = (byte)(currentEntry.Zero & 0x0F);
                            }
                            else if(entPMinMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PMin at line {0}", lineNumber);
                                currentEntry.PMIN = Convert.ToByte(entPMinMatch.Groups["value"].Value, 10);
                            }
                            else if(entPSecMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PSec at line {0}", lineNumber);
                                currentEntry.PSEC = Convert.ToByte(entPSecMatch.Groups["value"].Value, 10);
                            }
                            else if(entPFrameMatch.Success)
                            {
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PFrame at line {0}", lineNumber);
                                currentEntry.PFRAME = Convert.ToByte(entPFrameMatch.Groups["value"].Value, 10);
                            }
                            else if(entPlbaMatch.Success)
                                DicConsole.DebugWriteLine("CloneCD plugin", "Found PLBA at line {0}", lineNumber);
                        }
                    }
                }

                if(inEntry) entries.Add(currentEntry);

                if(entries.Count == 0) throw new FeatureUnsupportedImageException("Did not find any track.");

                FullTOC.CDFullTOC toc;
                toc.TrackDescriptors = entries.ToArray();
                toc.LastCompleteSession = (byte)maxSession;
                toc.FirstCompleteSession = (byte)minSession;
                toc.DataLength = (ushort)(entries.Count * 11 + 2);
                MemoryStream tocMs = new MemoryStream();
                tocMs.Write(BigEndianBitConverter.GetBytes(toc.DataLength), 0, 2);
                tocMs.WriteByte(toc.FirstCompleteSession);
                tocMs.WriteByte(toc.LastCompleteSession);
                foreach(FullTOC.TrackDataDescriptor descriptor in toc.TrackDescriptors)
                {
                    tocMs.WriteByte(descriptor.SessionNumber);
                    tocMs.WriteByte((byte)((descriptor.ADR << 4) + descriptor.CONTROL));
                    tocMs.WriteByte(descriptor.TNO);
                    tocMs.WriteByte(descriptor.POINT);
                    tocMs.WriteByte(descriptor.Min);
                    tocMs.WriteByte(descriptor.Sec);
                    tocMs.WriteByte(descriptor.Frame);
                    tocMs.WriteByte(descriptor.Zero);
                    tocMs.WriteByte(descriptor.PMIN);
                    tocMs.WriteByte(descriptor.PSEC);
                    tocMs.WriteByte(descriptor.PFRAME);
                }

                fulltoc = tocMs.ToArray();
                ImageInfo.ReadableMediaTags.Add(MediaTagType.CD_FullTOC);

                DicConsole.DebugWriteLine("CloneCD plugin", "{0}", FullTOC.Prettify(toc));

                string dataFile = Path.GetFileNameWithoutExtension(imageFilter.GetBasePath()) + ".img";
                string subFile = Path.GetFileNameWithoutExtension(imageFilter.GetBasePath()) + ".sub";

                FiltersList filtersList = new FiltersList();
                dataFilter = filtersList.GetFilter(dataFile);

                if(dataFilter == null) throw new Exception("Cannot open data file");

                filtersList = new FiltersList();
                subFilter = filtersList.GetFilter(subFile);

                int curSessionNo = 0;
                Track currentTrack = new Track();
                bool firstTrackInSession = true;
                tracks = new List<Track>();
                ulong leadOutStart = 0;

                dataStream = dataFilter.GetDataForkStream();
                if(subFilter != null) subStream = subFilter.GetDataForkStream();

                foreach(FullTOC.TrackDataDescriptor descriptor in entries)
                {
                    if(descriptor.SessionNumber > curSessionNo)
                    {
                        curSessionNo = descriptor.SessionNumber;
                        if(!firstTrackInSession)
                        {
                            currentTrack.TrackEndSector = leadOutStart - 1;
                            tracks.Add(currentTrack);
                        }
                        firstTrackInSession = true;
                    }

                    switch(descriptor.ADR)
                    {
                        case 1:
                        case 4:
                            switch(descriptor.POINT)
                            {
                                case 0xA0:
                                    byte discType = descriptor.PSEC;
                                    DicConsole.DebugWriteLine("CloneCD plugin", "Disc Type: {0}", discType);
                                    break;
                                case 0xA2:
                                    leadOutStart = GetLba(descriptor.PHOUR, descriptor.PMIN, descriptor.PSEC,
                                                          descriptor.PFRAME);
                                    break;
                                default:
                                    if(descriptor.POINT >= 0x01 && descriptor.POINT <= 0x63)
                                    {
                                        if(!firstTrackInSession)
                                        {
                                            currentTrack.TrackEndSector =
                                                GetLba(descriptor.PHOUR, descriptor.PMIN, descriptor.PSEC,
                                                       descriptor.PFRAME) - 1;
                                            tracks.Add(currentTrack);
                                        }
                                        else firstTrackInSession = false;

                                        currentTrack = new Track
                                        {
                                            TrackBytesPerSector = 2352,
                                            TrackFile = dataFilter.GetFilename(),
                                            TrackFileType = scrambled ? "SCRAMBLED" : "BINARY",
                                            TrackFilter = dataFilter,
                                            TrackRawBytesPerSector = 2352,
                                            TrackSequence = descriptor.POINT,
                                            TrackStartSector =
                                                GetLba(descriptor.PHOUR, descriptor.PMIN, descriptor.PSEC,
                                                       descriptor.PFRAME),
                                            TrackSession = descriptor.SessionNumber
                                        };
                                        currentTrack.TrackFileOffset = currentTrack.TrackStartSector * 2352;

                                        // Need to check exact data type later
                                        if((TocControl)(descriptor.CONTROL & 0x0D) == TocControl.DataTrack ||
                                           (TocControl)(descriptor.CONTROL & 0x0D) == TocControl.DataTrackIncremental)
                                            currentTrack.TrackType = TrackType.Data;
                                        else currentTrack.TrackType = TrackType.Audio;

                                        if(subFilter != null)
                                        {
                                            currentTrack.TrackSubchannelFile = subFilter.GetFilename();
                                            currentTrack.TrackSubchannelFilter = subFilter;
                                            currentTrack.TrackSubchannelOffset = currentTrack.TrackStartSector * 96;
                                            currentTrack.TrackSubchannelType = TrackSubchannelType.Raw;
                                        }
                                        else currentTrack.TrackSubchannelType = TrackSubchannelType.None;

                                        if(currentTrack.TrackType == TrackType.Data)
                                        {
                                            byte[] syncTest = new byte[12];
                                            byte[] sectTest = new byte[2352];
                                            dataStream.Seek((long)currentTrack.TrackFileOffset, SeekOrigin.Begin);
                                            dataStream.Read(sectTest, 0, 2352);
                                            Array.Copy(sectTest, 0, syncTest, 0, 12);

                                            if(Sector.SyncMark.SequenceEqual(syncTest))
                                            {
                                                if(scrambled) sectTest = Sector.Scramble(sectTest);

                                                if(sectTest[15] == 1)
                                                {
                                                    currentTrack.TrackBytesPerSector = 2048;
                                                    currentTrack.TrackType = TrackType.CdMode1;
                                                    if(!ImageInfo.ReadableSectorTags
                                                                 .Contains(SectorTagType.CdSectorSync))
                                                        ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorSync);
                                                    if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                  .CdSectorHeader))
                                                        ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorHeader);
                                                    if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType.CdSectorEcc)
                                                    ) ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorEcc);
                                                    if(!ImageInfo.ReadableSectorTags
                                                                 .Contains(SectorTagType.CdSectorEccP))
                                                        ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorEccP);
                                                    if(!ImageInfo.ReadableSectorTags
                                                                 .Contains(SectorTagType.CdSectorEccQ))
                                                        ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorEccQ);
                                                    if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType.CdSectorEdc)
                                                    ) ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorEdc);
                                                    if(ImageInfo.SectorSize < 2048) ImageInfo.SectorSize = 2048;
                                                }
                                                else if(sectTest[15] == 2)
                                                {
                                                    byte[] subHdr1 = new byte[4];
                                                    byte[] subHdr2 = new byte[4];
                                                    byte[] empHdr = new byte[4];

                                                    Array.Copy(sectTest, 16, subHdr1, 0, 4);
                                                    Array.Copy(sectTest, 20, subHdr2, 0, 4);

                                                    if(subHdr1.SequenceEqual(subHdr2) && !empHdr.SequenceEqual(subHdr1))
                                                        if((subHdr1[2] & 0x20) == 0x20)
                                                        {
                                                            currentTrack.TrackBytesPerSector = 2324;
                                                            currentTrack.TrackType = TrackType.CdMode2Form2;
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorSync)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorSync);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorHeader)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorHeader);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorSubHeader)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorSubHeader);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorEdc))
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorEdc);
                                                            if(ImageInfo.SectorSize < 2324) ImageInfo.SectorSize = 2324;
                                                        }
                                                        else
                                                        {
                                                            currentTrack.TrackBytesPerSector = 2048;
                                                            currentTrack.TrackType = TrackType.CdMode2Form1;
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorSync)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorSync);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorHeader)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorHeader);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorSubHeader)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorSubHeader);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorEcc))
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorEcc);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorEccP)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorEccP);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorEccQ)
                                                            )
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorEccQ);
                                                            if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                          .CdSectorEdc))
                                                                ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                     .CdSectorEdc);
                                                            if(ImageInfo.SectorSize < 2048) ImageInfo.SectorSize = 2048;
                                                        }
                                                    else
                                                    {
                                                        currentTrack.TrackBytesPerSector = 2336;
                                                        currentTrack.TrackType = TrackType.CdMode2Formless;
                                                        if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                      .CdSectorSync))
                                                            ImageInfo.ReadableSectorTags
                                                                     .Add(SectorTagType.CdSectorSync);
                                                        if(!ImageInfo.ReadableSectorTags.Contains(SectorTagType
                                                                                                      .CdSectorHeader))
                                                            ImageInfo.ReadableSectorTags.Add(SectorTagType
                                                                                                 .CdSectorHeader);
                                                        if(ImageInfo.SectorSize < 2336) ImageInfo.SectorSize = 2336;
                                                    }
                                                }
                                            }
                                        }
                                        else { if(ImageInfo.SectorSize < 2352) ImageInfo.SectorSize = 2352; }
                                    }
                                    break;
                            }

                            break;
                        case 5:
                            switch(descriptor.POINT)
                            {
                                case 0xC0:
                                    if(descriptor.PMIN == 97)
                                    {
                                        int type = descriptor.PFRAME % 10;
                                        int frm = descriptor.PFRAME - type;

                                        ImageInfo.MediaManufacturer = ATIP.ManufacturerFromATIP(descriptor.PSEC, frm);

                                        if(ImageInfo.MediaManufacturer != "")
                                            DicConsole.DebugWriteLine("CloneCD plugin", "Disc manufactured by: {0}",
                                                                      ImageInfo.MediaManufacturer);
                                    }
                                    break;
                            }

                            break;
                        case 6:
                        {
                            uint id = (uint)((descriptor.Min << 16) + (descriptor.Sec << 8) + descriptor.Frame);
                            DicConsole.DebugWriteLine("CloneCD plugin", "Disc ID: {0:X6}", id & 0x00FFFFFF);
                            ImageInfo.MediaSerialNumber = $"{id & 0x00FFFFFF:X6}";
                            break;
                        }
                    }
                }

                if(!firstTrackInSession)
                {
                    currentTrack.TrackEndSector = leadOutStart - 1;
                    tracks.Add(currentTrack);
                }

                if(subFilter != null && !ImageInfo.ReadableSectorTags.Contains(SectorTagType.CdSectorSubchannel))
                    ImageInfo.ReadableSectorTags.Add(SectorTagType.CdSectorSubchannel);

                sessions = new List<Session>();
                Session currentSession = new Session
                {
                    EndTrack = uint.MinValue,
                    StartTrack = uint.MaxValue,
                    SessionSequence = 1
                };
                partitions = new List<Partition>();
                offsetmap = new Dictionary<uint, ulong>();

                foreach(Track track in tracks)
                {
                    if(track.TrackSession == currentSession.SessionSequence)
                    {
                        if(track.TrackSequence > currentSession.EndTrack)
                        {
                            currentSession.EndSector = track.TrackEndSector;
                            currentSession.EndTrack = track.TrackSequence;
                        }

                        if(track.TrackSequence < currentSession.StartTrack)
                        {
                            currentSession.StartSector = track.TrackStartSector;
                            currentSession.StartTrack = track.TrackSequence;
                        }
                    }
                    else
                    {
                        sessions.Add(currentSession);
                        currentSession = new Session
                        {
                            EndTrack = uint.MinValue,
                            StartTrack = uint.MaxValue,
                            SessionSequence = track.TrackSession
                        };
                    }

                    Partition partition = new Partition
                    {
                        Description = track.TrackDescription,
                        Size =
                            (track.TrackEndSector - track.TrackStartSector + 1) * (ulong)track.TrackRawBytesPerSector,
                        Length = track.TrackEndSector - track.TrackStartSector + 1,
                        Sequence = track.TrackSequence,
                        Offset = track.TrackFileOffset,
                        Start = track.TrackStartSector,
                        Type = track.TrackType.ToString()
                    };
                    ImageInfo.Sectors += partition.Length;
                    partitions.Add(partition);
                    offsetmap.Add(track.TrackSequence, track.TrackStartSector);
                }

                bool data = false;
                bool mode2 = false;
                bool firstaudio = false;
                bool firstdata = false;
                bool audio = false;

                for(int i = 0; i < tracks.Count; i++)
                {
                    // First track is audio
                    firstaudio |= i == 0 && tracks[i].TrackType == TrackType.Audio;

                    // First track is data
                    firstdata |= i == 0 && tracks[i].TrackType != TrackType.Audio;

                    // Any non first track is data
                    data |= i != 0 && tracks[i].TrackType != TrackType.Audio;

                    // Any non first track is audio
                    audio |= i != 0 && tracks[i].TrackType == TrackType.Audio;

                    switch(tracks[i].TrackType)
                    {
                        case TrackType.CdMode2Form1:
                        case TrackType.CdMode2Form2:
                        case TrackType.CdMode2Formless:
                            mode2 = true;
                            break;
                    }
                }

                // TODO: Check format
                cdtext = cdtMs.ToArray();

                if(!data && !firstdata) ImageInfo.MediaType = MediaType.CDDA;
                else if(firstaudio && data && sessions.Count > 1 && mode2) ImageInfo.MediaType = MediaType.CDPLUS;
                else if(firstdata && audio || mode2) ImageInfo.MediaType = MediaType.CDROMXA;
                else if(!audio) ImageInfo.MediaType = MediaType.CDROM;
                else ImageInfo.MediaType = MediaType.CD;

                ImageInfo.ImageApplication = "CloneCD";
                ImageInfo.ImageSize = (ulong)imageFilter.GetDataForkLength();
                ImageInfo.ImageCreationTime = imageFilter.GetCreationTime();
                ImageInfo.ImageLastModificationTime = imageFilter.GetLastWriteTime();
                ImageInfo.XmlMediaType = XmlMediaType.OpticalDisc;

                return true;
            }
            catch(Exception ex)
            {
                DicConsole.ErrorWriteLine("Exception trying to identify image file {0}", imageFilter.GetFilename());
                DicConsole.ErrorWriteLine("Exception: {0}", ex.Message);
                DicConsole.ErrorWriteLine("Stack trace: {0}", ex.StackTrace);
                return false;
            }
        }

        static ulong GetLba(int hour, int minute, int second, int frame)
        {
            return (ulong)(hour * 60 * 60 * 75 + minute * 60 * 75 + second * 75 + frame - 150);
        }

        public override bool ImageHasPartitions()
        {
            return ImageInfo.ImageHasPartitions;
        }

        public override ulong GetImageSize()
        {
            return ImageInfo.ImageSize;
        }

        public override ulong GetSectors()
        {
            return ImageInfo.Sectors;
        }

        public override uint GetSectorSize()
        {
            return ImageInfo.SectorSize;
        }

        public override byte[] ReadDiskTag(MediaTagType tag)
        {
            switch(tag)
            {
                case MediaTagType.CD_FullTOC:
                {
                    return fulltoc;
                }
                case MediaTagType.CD_TEXT:
                {
                    if(cdtext != null && cdtext.Length > 0) return cdtext;

                    throw new FeatureNotPresentImageException("Image does not contain CD-TEXT information.");
                }
                default:
                    throw new FeatureSupportedButNotImplementedImageException("Feature not supported by image format");
            }
        }

        public override byte[] ReadSector(ulong sectorAddress)
        {
            return ReadSectors(sectorAddress, 1);
        }

        public override byte[] ReadSectorTag(ulong sectorAddress, SectorTagType tag)
        {
            return ReadSectorsTag(sectorAddress, 1, tag);
        }

        public override byte[] ReadSector(ulong sectorAddress, uint track)
        {
            return ReadSectors(sectorAddress, 1, track);
        }

        public override byte[] ReadSectorTag(ulong sectorAddress, uint track, SectorTagType tag)
        {
            return ReadSectorsTag(sectorAddress, 1, track, tag);
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length)
        {
            foreach(KeyValuePair<uint, ulong> kvp in from kvp in offsetmap
                                                     where sectorAddress >= kvp.Value
                                                     from track in tracks
                                                     where track.TrackSequence == kvp.Key
                                                     where sectorAddress <= track.TrackEndSector
                                                     select kvp)
                return ReadSectors(sectorAddress - kvp.Value, length, kvp.Key);

            throw new ArgumentOutOfRangeException(nameof(sectorAddress), $"Sector address {sectorAddress} not found");
        }

        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, SectorTagType tag)
        {
            foreach(KeyValuePair<uint, ulong> kvp in from kvp in offsetmap
                                                     where sectorAddress >= kvp.Value
                                                     from track in tracks
                                                     where track.TrackSequence == kvp.Key
                                                     where sectorAddress <= track.TrackEndSector
                                                     select kvp)
                return ReadSectorsTag(sectorAddress - kvp.Value, length, kvp.Key, tag);

            throw new ArgumentOutOfRangeException(nameof(sectorAddress), $"Sector address {sectorAddress} not found");
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length, uint track)
        {
            Track dicTrack = new Track {TrackSequence = 0};

            foreach(Track linqTrack in tracks.Where(linqTrack => linqTrack.TrackSequence == track))
            {
                dicTrack = linqTrack;
                break;
            }

            if(dicTrack.TrackSequence == 0)
                throw new ArgumentOutOfRangeException(nameof(track), "Track does not exist in disc image");

            if(length + sectorAddress - 1 > dicTrack.TrackEndSector)
                throw new ArgumentOutOfRangeException(nameof(length),
                                                      string
                                                          .Format("Requested more sectors ({0} {2}) than present in track ({1}), won't cross tracks",
                                                                  length + sectorAddress, dicTrack.TrackEndSector,
                                                                  sectorAddress));

            uint sectorOffset;
            uint sectorSize;
            uint sectorSkip;

            switch(dicTrack.TrackType)
            {
                case TrackType.Audio:
                {
                    sectorOffset = 0;
                    sectorSize = 2352;
                    sectorSkip = 0;
                    break;
                }
                case TrackType.CdMode1:
                {
                    sectorOffset = 16;
                    sectorSize = 2048;
                    sectorSkip = 288;
                    break;
                }
                case TrackType.CdMode2Formless:
                {
                    sectorOffset = 16;
                    sectorSize = 2336;
                    sectorSkip = 0;
                    break;
                }
                case TrackType.CdMode2Form1:
                {
                    sectorOffset = 24;
                    sectorSize = 2048;
                    sectorSkip = 280;
                    break;
                }
                case TrackType.CdMode2Form2:
                {
                    sectorOffset = 24;
                    sectorSize = 2324;
                    sectorSkip = 4;
                    break;
                }
                default: throw new FeatureSupportedButNotImplementedImageException("Unsupported track type");
            }

            byte[] buffer = new byte[sectorSize * length];

            dataStream.Seek((long)(dicTrack.TrackFileOffset + sectorAddress * 2352), SeekOrigin.Begin);
            if(sectorOffset == 0 && sectorSkip == 0) dataStream.Read(buffer, 0, buffer.Length);
            else
                for(int i = 0; i < length; i++)
                {
                    byte[] sector = new byte[sectorSize];
                    dataStream.Seek(sectorOffset, SeekOrigin.Current);
                    dataStream.Read(sector, 0, sector.Length);
                    dataStream.Seek(sectorSkip, SeekOrigin.Current);
                    Array.Copy(sector, 0, buffer, i * sectorSize, sectorSize);
                }

            return buffer;
        }

        // TODO: Flags
        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, uint track, SectorTagType tag)
        {
            Track dicTrack = new Track {TrackSequence = 0};

            foreach(Track linqTrack in tracks.Where(linqTrack => linqTrack.TrackSequence == track))
            {
                dicTrack = linqTrack;
                break;
            }

            if(dicTrack.TrackSequence == 0)
                throw new ArgumentOutOfRangeException(nameof(track), "Track does not exist in disc image");

            if(length + sectorAddress - 1 > dicTrack.TrackEndSector)
                throw new ArgumentOutOfRangeException(nameof(length),
                                                      $"Requested more sectors ({length + sectorAddress}) than present in track ({dicTrack.TrackEndSector}), won't cross tracks");

            if(dicTrack.TrackType == TrackType.Data)
                throw new ArgumentException("Unsupported tag requested", nameof(tag));

            byte[] buffer;

            switch(tag)
            {
                case SectorTagType.CdSectorEcc:
                case SectorTagType.CdSectorEccP:
                case SectorTagType.CdSectorEccQ:
                case SectorTagType.CdSectorEdc:
                case SectorTagType.CdSectorHeader:
                case SectorTagType.CdSectorSubHeader:
                case SectorTagType.CdSectorSync: break;
                case SectorTagType.CdSectorSubchannel:
                    buffer = new byte[96 * length];
                    subStream.Seek((long)(dicTrack.TrackSubchannelOffset + sectorAddress * 96), SeekOrigin.Begin);
                    subStream.Read(buffer, 0, buffer.Length);
                    return buffer;
                default: throw new ArgumentException("Unsupported tag requested", nameof(tag));
            }

            uint sectorOffset;
            uint sectorSize;
            uint sectorSkip;

            switch(dicTrack.TrackType)
            {
                case TrackType.CdMode1:
                    switch(tag)
                    {
                        case SectorTagType.CdSectorSync:
                        {
                            sectorOffset = 0;
                            sectorSize = 12;
                            sectorSkip = 2340;
                            break;
                        }
                        case SectorTagType.CdSectorHeader:
                        {
                            sectorOffset = 12;
                            sectorSize = 4;
                            sectorSkip = 2336;
                            break;
                        }
                        case SectorTagType.CdSectorSubHeader:
                            throw new ArgumentException("Unsupported tag requested for this track", nameof(tag));
                        case SectorTagType.CdSectorEcc:
                        {
                            sectorOffset = 2076;
                            sectorSize = 276;
                            sectorSkip = 0;
                            break;
                        }
                        case SectorTagType.CdSectorEccP:
                        {
                            sectorOffset = 2076;
                            sectorSize = 172;
                            sectorSkip = 104;
                            break;
                        }
                        case SectorTagType.CdSectorEccQ:
                        {
                            sectorOffset = 2248;
                            sectorSize = 104;
                            sectorSkip = 0;
                            break;
                        }
                        case SectorTagType.CdSectorEdc:
                        {
                            sectorOffset = 2064;
                            sectorSize = 4;
                            sectorSkip = 284;
                            break;
                        }
                        default: throw new ArgumentException("Unsupported tag requested", nameof(tag));
                    }

                    break;
                case TrackType.CdMode2Formless:
                {
                    switch(tag)
                    {
                        case SectorTagType.CdSectorSync:
                        case SectorTagType.CdSectorHeader:
                        case SectorTagType.CdSectorEcc:
                        case SectorTagType.CdSectorEccP:
                        case SectorTagType.CdSectorEccQ:
                            throw new ArgumentException("Unsupported tag requested for this track", nameof(tag));
                        case SectorTagType.CdSectorSubHeader:
                        {
                            sectorOffset = 0;
                            sectorSize = 8;
                            sectorSkip = 2328;
                            break;
                        }
                        case SectorTagType.CdSectorEdc:
                        {
                            sectorOffset = 2332;
                            sectorSize = 4;
                            sectorSkip = 0;
                            break;
                        }
                        default: throw new ArgumentException("Unsupported tag requested", nameof(tag));
                    }

                    break;
                }
                case TrackType.CdMode2Form1:
                    switch(tag)
                    {
                        case SectorTagType.CdSectorSync:
                        {
                            sectorOffset = 0;
                            sectorSize = 12;
                            sectorSkip = 2340;
                            break;
                        }
                        case SectorTagType.CdSectorHeader:
                        {
                            sectorOffset = 12;
                            sectorSize = 4;
                            sectorSkip = 2336;
                            break;
                        }
                        case SectorTagType.CdSectorSubHeader:
                        {
                            sectorOffset = 16;
                            sectorSize = 8;
                            sectorSkip = 2328;
                            break;
                        }
                        case SectorTagType.CdSectorEcc:
                        {
                            sectorOffset = 2076;
                            sectorSize = 276;
                            sectorSkip = 0;
                            break;
                        }
                        case SectorTagType.CdSectorEccP:
                        {
                            sectorOffset = 2076;
                            sectorSize = 172;
                            sectorSkip = 104;
                            break;
                        }
                        case SectorTagType.CdSectorEccQ:
                        {
                            sectorOffset = 2248;
                            sectorSize = 104;
                            sectorSkip = 0;
                            break;
                        }
                        case SectorTagType.CdSectorEdc:
                        {
                            sectorOffset = 2072;
                            sectorSize = 4;
                            sectorSkip = 276;
                            break;
                        }
                        default: throw new ArgumentException("Unsupported tag requested", nameof(tag));
                    }

                    break;
                case TrackType.CdMode2Form2:
                    switch(tag)
                    {
                        case SectorTagType.CdSectorSync:
                        {
                            sectorOffset = 0;
                            sectorSize = 12;
                            sectorSkip = 2340;
                            break;
                        }
                        case SectorTagType.CdSectorHeader:
                        {
                            sectorOffset = 12;
                            sectorSize = 4;
                            sectorSkip = 2336;
                            break;
                        }
                        case SectorTagType.CdSectorSubHeader:
                        {
                            sectorOffset = 16;
                            sectorSize = 8;
                            sectorSkip = 2328;
                            break;
                        }
                        case SectorTagType.CdSectorEdc:
                        {
                            sectorOffset = 2348;
                            sectorSize = 4;
                            sectorSkip = 0;
                            break;
                        }
                        default: throw new ArgumentException("Unsupported tag requested", nameof(tag));
                    }

                    break;
                case TrackType.Audio:
                {
                    throw new ArgumentException("Unsupported tag requested", nameof(tag));
                }
                default: throw new FeatureSupportedButNotImplementedImageException("Unsupported track type");
            }

            buffer = new byte[sectorSize * length];

            dataStream.Seek((long)(dicTrack.TrackFileOffset + sectorAddress * 2352), SeekOrigin.Begin);
            if(sectorOffset == 0 && sectorSkip == 0) dataStream.Read(buffer, 0, buffer.Length);
            else
                for(int i = 0; i < length; i++)
                {
                    byte[] sector = new byte[sectorSize];
                    dataStream.Seek(sectorOffset, SeekOrigin.Current);
                    dataStream.Read(sector, 0, sector.Length);
                    dataStream.Seek(sectorSkip, SeekOrigin.Current);
                    Array.Copy(sector, 0, buffer, i * sectorSize, sectorSize);
                }

            return buffer;
        }

        public override byte[] ReadSectorLong(ulong sectorAddress)
        {
            return ReadSectorsLong(sectorAddress, 1);
        }

        public override byte[] ReadSectorLong(ulong sectorAddress, uint track)
        {
            return ReadSectorsLong(sectorAddress, 1, track);
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length)
        {
            foreach(KeyValuePair<uint, ulong> kvp in from kvp in offsetmap
                                                     where sectorAddress >= kvp.Value
                                                     from track in tracks
                                                     where track.TrackSequence == kvp.Key
                                                     where sectorAddress - kvp.Value <
                                                           track.TrackEndSector - track.TrackStartSector + 1
                                                     select kvp)
                return ReadSectorsLong(sectorAddress - kvp.Value, length, kvp.Key);

            throw new ArgumentOutOfRangeException(nameof(sectorAddress), $"Sector address {sectorAddress} not found");
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length, uint track)
        {
            Track dicTrack = new Track {TrackSequence = 0};

            foreach(Track linqTrack in tracks.Where(linqTrack => linqTrack.TrackSequence == track))
            {
                dicTrack = linqTrack;
                break;
            }

            if(dicTrack.TrackSequence == 0)
                throw new ArgumentOutOfRangeException(nameof(track), "Track does not exist in disc image");

            if(length + sectorAddress - 1 > dicTrack.TrackEndSector)
                throw new ArgumentOutOfRangeException(nameof(length),
                                                      $"Requested more sectors ({length + sectorAddress}) than present in track ({dicTrack.TrackEndSector}), won't cross tracks");

            byte[] buffer = new byte[2352 * length];

            dataStream.Seek((long)(dicTrack.TrackFileOffset + sectorAddress * 2352), SeekOrigin.Begin);
            dataStream.Read(buffer, 0, buffer.Length);

            return buffer;
        }

        public override string GetImageFormat()
        {
            return "CloneCD";
        }

        public override string GetImageVersion()
        {
            return ImageInfo.ImageVersion;
        }

        public override string GetImageApplication()
        {
            return ImageInfo.ImageApplication;
        }

        public override string GetImageApplicationVersion()
        {
            return ImageInfo.ImageApplicationVersion;
        }

        public override string GetImageCreator()
        {
            return ImageInfo.ImageCreator;
        }

        public override DateTime GetImageCreationTime()
        {
            return ImageInfo.ImageCreationTime;
        }

        public override DateTime GetImageLastModificationTime()
        {
            return ImageInfo.ImageLastModificationTime;
        }

        public override string GetImageName()
        {
            return ImageInfo.ImageName;
        }

        public override string GetImageComments()
        {
            return ImageInfo.ImageComments;
        }

        public override string GetMediaManufacturer()
        {
            return ImageInfo.MediaManufacturer;
        }

        public override string GetMediaModel()
        {
            return ImageInfo.MediaModel;
        }

        public override string GetMediaSerialNumber()
        {
            return ImageInfo.DriveSerialNumber;
        }

        public override string GetMediaBarcode()
        {
            return ImageInfo.MediaBarcode;
        }

        public override string GetMediaPartNumber()
        {
            return ImageInfo.MediaPartNumber;
        }

        public override MediaType GetMediaType()
        {
            return ImageInfo.MediaType;
        }

        public override int GetMediaSequence()
        {
            return ImageInfo.MediaSequence;
        }

        public override int GetLastDiskSequence()
        {
            return ImageInfo.LastMediaSequence;
        }

        public override string GetDriveManufacturer()
        {
            return ImageInfo.DriveManufacturer;
        }

        public override string GetDriveModel()
        {
            return ImageInfo.DriveModel;
        }

        public override string GetDriveSerialNumber()
        {
            return ImageInfo.DriveSerialNumber;
        }

        public override List<Partition> GetPartitions()
        {
            return partitions;
        }

        public override List<Track> GetTracks()
        {
            return tracks;
        }

        public override List<Track> GetSessionTracks(Session session)
        {
            if(sessions.Contains(session)) return GetSessionTracks(session.SessionSequence);

            throw new ImageNotSupportedException("Session does not exist in disc image");
        }

        public override List<Track> GetSessionTracks(ushort session)
        {
            return tracks.Where(track => track.TrackSession == session).ToList();
        }

        public override List<Session> GetSessions()
        {
            return sessions;
        }

        public override bool? VerifySector(ulong sectorAddress)
        {
            byte[] buffer = ReadSectorLong(sectorAddress);
            return CdChecksums.CheckCdSector(buffer);
        }

        public override bool? VerifySector(ulong sectorAddress, uint track)
        {
            byte[] buffer = ReadSectorLong(sectorAddress, track);
            return CdChecksums.CheckCdSector(buffer);
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            byte[] buffer = ReadSectorsLong(sectorAddress, length);
            int bps = (int)(buffer.Length / length);
            byte[] sector = new byte[bps];
            failingLbas = new List<ulong>();
            unknownLbas = new List<ulong>();

            for(int i = 0; i < length; i++)
            {
                Array.Copy(buffer, i * bps, sector, 0, bps);
                bool? sectorStatus = CdChecksums.CheckCdSector(sector);

                switch(sectorStatus)
                {
                    case null:
                        unknownLbas.Add((ulong)i + sectorAddress);
                        break;
                    case false:
                        failingLbas.Add((ulong)i + sectorAddress);
                        break;
                }
            }

            if(unknownLbas.Count > 0) return null;
            return failingLbas.Count <= 0;
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, uint track, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            byte[] buffer = ReadSectorsLong(sectorAddress, length, track);
            int bps = (int)(buffer.Length / length);
            byte[] sector = new byte[bps];
            failingLbas = new List<ulong>();
            unknownLbas = new List<ulong>();

            for(int i = 0; i < length; i++)
            {
                Array.Copy(buffer, i * bps, sector, 0, bps);
                bool? sectorStatus = CdChecksums.CheckCdSector(sector);

                switch(sectorStatus)
                {
                    case null:
                        unknownLbas.Add((ulong)i + sectorAddress);
                        break;
                    case false:
                        failingLbas.Add((ulong)i + sectorAddress);
                        break;
                }
            }

            if(unknownLbas.Count > 0) return null;
            return failingLbas.Count <= 0;
        }

        public override bool? VerifyMediaImage()
        {
            return null;
        }
    }
}