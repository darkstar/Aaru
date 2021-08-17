﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Write.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Disk image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Writes Anex86 disk images.
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
// Copyright © 2011-2021 Natalia Portillo
// ****************************************************************************/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Structs;
using Schemas;
using Marshal = Aaru.Helpers.Marshal;

namespace Aaru.DiscImages
{
    public sealed partial class Anex86
    {
        /// <inheritdoc />
        public bool Create(string path, MediaType mediaType, Dictionary<string, string> options, ulong sectors,
                           uint sectorSize)
        {
            if(sectorSize == 0)
            {
                ErrorMessage = "Unsupported sector size";

                return false;
            }

            if(sectors * sectorSize > int.MaxValue ||
               sectors              > (long)int.MaxValue * 8 * 33)
            {
                ErrorMessage = "Too many sectors";

                return false;
            }

            if(!SupportedMediaTypes.Contains(mediaType))
            {
                ErrorMessage = $"Unsupported media format {mediaType}";

                return false;
            }

            _imageInfo = new ImageInfo
            {
                MediaType  = mediaType,
                SectorSize = sectorSize,
                Sectors    = sectors
            };

            try
            {
                _writingStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch(IOException e)
            {
                ErrorMessage = $"Could not create new image file, exception {e.Message}";

                return false;
            }

            _header = new Header
            {
                hdrSize = 4096,
                dskSize = (int)(sectors * sectorSize),
                bps     = (int)sectorSize
            };

            IsWriting    = true;
            ErrorMessage = null;

            return true;
        }

        /// <inheritdoc />
        public bool WriteMediaTag(byte[] data, MediaTagType tag)
        {
            ErrorMessage = "Writing media tags is not supported.";

            return false;
        }

        /// <inheritdoc />
        public bool WriteSector(byte[] data, ulong sectorAddress)
        {
            if(!IsWriting)
            {
                ErrorMessage = "Tried to write on a non-writable image";

                return false;
            }

            if(data.Length != _imageInfo.SectorSize)
            {
                ErrorMessage = "Incorrect data size";

                return false;
            }

            if(sectorAddress >= _imageInfo.Sectors)
            {
                ErrorMessage = "Tried to write past image size";

                return false;
            }

            _writingStream.Seek((long)(4096 + (sectorAddress * _imageInfo.SectorSize)), SeekOrigin.Begin);
            _writingStream.Write(data, 0, data.Length);

            ErrorMessage = "";

            return true;
        }

        /// <inheritdoc />
        public bool WriteSectors(byte[] data, ulong sectorAddress, uint length)
        {
            if(!IsWriting)
            {
                ErrorMessage = "Tried to write on a non-writable image";

                return false;
            }

            if(data.Length % _imageInfo.SectorSize != 0)
            {
                ErrorMessage = "Incorrect data size";

                return false;
            }

            if(sectorAddress + length > _imageInfo.Sectors)
            {
                ErrorMessage = "Tried to write past image size";

                return false;
            }

            _writingStream.Seek((long)(4096 + (sectorAddress * _imageInfo.SectorSize)), SeekOrigin.Begin);
            _writingStream.Write(data, 0, data.Length);

            ErrorMessage = "";

            return true;
        }

        /// <inheritdoc />
        public bool WriteSectorLong(byte[] data, ulong sectorAddress)
        {
            ErrorMessage = "Writing sectors with tags is not supported.";

            return false;
        }

        /// <inheritdoc />
        public bool WriteSectorsLong(byte[] data, ulong sectorAddress, uint length)
        {
            ErrorMessage = "Writing sectors with tags is not supported.";

            return false;
        }

        /// <inheritdoc />
        public bool Close()
        {
            if(!IsWriting)
            {
                ErrorMessage = "Image is not opened for writing";

                return false;
            }

            if((_imageInfo.MediaType == MediaType.Unknown || _imageInfo.MediaType == MediaType.GENERIC_HDD ||
                _imageInfo.MediaType == MediaType.FlashDrive || _imageInfo.MediaType == MediaType.CompactFlash ||
                _imageInfo.MediaType == MediaType.CompactFlashType2 || _imageInfo.MediaType == MediaType.PCCardTypeI ||
                _imageInfo.MediaType == MediaType.PCCardTypeII || _imageInfo.MediaType == MediaType.PCCardTypeIII ||
                _imageInfo.MediaType == MediaType.PCCardTypeIV) &&
               _header.cylinders == 0)
            {
                _header.cylinders = (int)(_imageInfo.Sectors / 8 / 33);
                _header.heads     = 8;
                _header.spt       = 33;

                while(_header.cylinders == 0)
                {
                    _header.heads--;

                    if(_header.heads == 0)
                    {
                        _header.spt--;
                        _header.heads = 8;
                    }

                    _header.cylinders = (int)_imageInfo.Sectors / _header.heads / _header.spt;

                    if(_header.cylinders == 0 &&
                       _header.heads     == 0 &&
                       _header.spt       == 0)
                        break;
                }
            }

            byte[] hdr = new byte[Marshal.SizeOf<Header>()];
            MemoryMarshal.Write(hdr, ref _header);

            _writingStream.Seek(0, SeekOrigin.Begin);
            _writingStream.Write(hdr, 0, hdr.Length);

            _writingStream.Flush();
            _writingStream.Close();

            IsWriting    = false;
            ErrorMessage = "";

            return true;
        }

        /// <inheritdoc />
        public bool SetMetadata(ImageInfo metadata) => true;

        /// <inheritdoc />
        public bool SetGeometry(uint cylinders, uint heads, uint sectorsPerTrack)
        {
            if(cylinders > int.MaxValue)
            {
                ErrorMessage = "Too many cylinders.";

                return false;
            }

            if(heads > int.MaxValue)
            {
                ErrorMessage = "Too many heads.";

                return false;
            }

            if(sectorsPerTrack > int.MaxValue)
            {
                ErrorMessage = "Too many sectors per track.";

                return false;
            }

            _header.spt       = (int)sectorsPerTrack;
            _header.heads     = (int)heads;
            _header.cylinders = (int)cylinders;

            return true;
        }

        /// <inheritdoc />
        public bool WriteSectorTag(byte[] data, ulong sectorAddress, SectorTagType tag)
        {
            ErrorMessage = "Unsupported feature";

            return false;
        }

        /// <inheritdoc />
        public bool WriteSectorsTag(byte[] data, ulong sectorAddress, uint length, SectorTagType tag)
        {
            ErrorMessage = "Unsupported feature";

            return false;
        }

        /// <inheritdoc />
        public bool SetDumpHardware(List<DumpHardwareType> dumpHardware) => false;

        /// <inheritdoc />
        public bool SetCicmMetadata(CICMMetadataType metadata) => false;
    }
}