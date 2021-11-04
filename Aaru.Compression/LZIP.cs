// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : LZIP.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Compression algorithms.
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

using System.Runtime.InteropServices;

namespace Aaru.Compression;

public class LZIP
{
    /// <summary>
    /// Set to <c>true</c> if this algorithm is supported, <c>false</c> otherwise.
    /// </summary>
    public static bool IsSupported => Native.IsSupported;

    [DllImport("libAaru.Compression.Native", SetLastError = true)]
    static extern int AARU_lzip_decode_buffer(byte[] dst_buffer, int dst_size, byte[] src_buffer, int src_size);

    [DllImport("libAaru.Compression.Native", SetLastError = true)]
    static extern int AARU_lzip_encode_buffer(byte[] dst_buffer, int dst_size, byte[] src_buffer, int src_size,
                                              int dictionary_size, int match_len_limit);

    /// <summary>Decodes a buffer compressed with LZIP</summary>
    /// <param name="source">Encoded buffer</param>
    /// <param name="destination">Buffer where to write the decoded data</param>
    /// <returns>The number of decoded bytes</returns>
    public static int DecodeBuffer(byte[] source, byte[] destination) =>
        Native.IsSupported ? AARU_lzip_decode_buffer(destination, destination.Length, source, source.Length) : 0;

    /// <summary>Compresses a buffer using LZIP</summary>
    /// <param name="source">Data to compress</param>
    /// <param name="destination">Buffer to store the compressed data</param>
    /// <returns></returns>
    public static int EncodeBuffer(byte[] source, byte[] destination, int dictionarySize, int matchLengthLimit) =>
        Native.IsSupported ? AARU_lzip_encode_buffer(destination, destination.Length, source, source.Length,
                                                     dictionarySize, matchLengthLimit) : 0;
}