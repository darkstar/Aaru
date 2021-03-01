﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : UFS.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Aaru unit testing.
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
// Copyright © 2011-2021 Natalia Portillo
// ****************************************************************************/

using System.IO;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Interfaces;
using Aaru.Filesystems;
using NUnit.Framework;

namespace Aaru.Tests.Filesystems.UFS
{
    [TestFixture]
    public class Whole : FilesystemTest
    {
        public Whole() : base(null) {}

        public override string _dataFolder => Path.Combine(Consts.TEST_FILES_ROOT, "Filesystems", "UNIX filesystem");

        public override IFilesystem _plugin     => new FFSPlugin();
        public override bool        _partitions => false;

        public override FileSystemTest[] Tests => new[]
        {
            new FileSystemTest
            {
                TestFile    = "amix_mf2dd.adf.lz",
                MediaType   = MediaType.CBM_AMIGA_35_DD,
                Sectors     = 1760,
                SectorSize  = 512,
                Clusters    = 880,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "netbsd_1.6_mf2hd.img.lz",
                MediaType   = MediaType.DOS_35_HD,
                Sectors     = 2880,
                SectorSize  = 512,
                Clusters    = 2880,
                ClusterSize = 512,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "att_unix_svr4v2.1_dsdd.img.lz",
                MediaType   = MediaType.DOS_525_DS_DD_9,
                Sectors     = 720,
                SectorSize  = 512,
                Clusters    = 360,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "att_unix_svr4v2.1_dshd.img.lz",
                MediaType   = MediaType.DOS_525_HD,
                Sectors     = 2400,
                SectorSize  = 512,
                Clusters    = 1200,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "att_unix_svr4v2.1_mf2dd.img.lz",
                MediaType   = MediaType.DOS_35_DS_DD_9,
                Sectors     = 1440,
                SectorSize  = 512,
                Clusters    = 720,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "att_unix_svr4v2.1_mf2hd.img.lz",
                MediaType   = MediaType.DOS_35_HD,
                Sectors     = 2880,
                SectorSize  = 512,
                Clusters    = 1440,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "solaris_2.4_mf2dd.img.lz",
                MediaType   = MediaType.DOS_35_DS_DD_9,
                Sectors     = 1440,
                SectorSize  = 512,
                Clusters    = 711,
                ClusterSize = 1024,
                Type        = "UFS"
            },
            new FileSystemTest
            {
                TestFile    = "solaris_2.4_mf2hd.img.lz",
                MediaType   = MediaType.DOS_35_HD,
                Sectors     = 2880,
                SectorSize  = 512,
                Clusters    = 1422,
                ClusterSize = 1024,
                Type        = "UFS"
            }
        };
    }
}