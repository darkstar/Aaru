﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : PartClone.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Disk image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Manages partclone disk images.
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
// Copyright © 2011-2020 Natalia Portillo
// ****************************************************************************/

using System.Collections.Generic;
using System.IO;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Extents;
using Aaru.CommonTypes.Interfaces;
using Aaru.CommonTypes.Structs;

namespace Aaru.DiscImages
{
    public partial class PartClone : IMediaImage, IVerifiableImage
    {
        // The used block "bitmap" uses one byte per block
        // TODO: Convert on-image bytemap to on-memory bitmap
        byte[]                    byteMap;
        long                      dataOff;
        ExtentsULong              extents;
        Dictionary<ulong, ulong>  extentsOff;
        ImageInfo                 imageInfo;
        Stream                    imageStream;
        PartCloneHeader           pHdr;
        Dictionary<ulong, byte[]> sectorCache;

        public PartClone() => imageInfo = new ImageInfo
        {
            ReadableSectorTags    = new List<SectorTagType>(), ReadableMediaTags = new List<MediaTagType>(),
            HasPartitions         = false, HasSessions                           = false,
            Application           = "PartClone", ApplicationVersion              = null,
            Creator               = null, Comments                               = null, MediaManufacturer = null,
            MediaModel            = null,
            MediaSerialNumber     = null, MediaBarcode = null, MediaPartNumber = null,
            MediaSequence         = 0,
            LastMediaSequence     = 0, DriveManufacturer = null, DriveModel = null,
            DriveSerialNumber     = null,
            DriveFirmwareRevision = null
        };
    }
}