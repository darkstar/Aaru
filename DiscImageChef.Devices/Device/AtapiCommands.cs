﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : AtapiCommands.cs
// Version        : 1.0
// Author(s)      : Natalia Portillo
//
// Component      : Direct device access
//
// Revision       : $Revision$
// Last change by : $Author$
// Date           : $Date$
//
// --[ Description ] ----------------------------------------------------------
//
// Contains ATAPI commands
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
// Copyright (C) 2011-2015 Claunia.com
// ****************************************************************************/
// //$Id$
using System;
using DiscImageChef.Console;

namespace DiscImageChef.Devices
{
    public partial class Device
    {
        /// <summary>
        /// Sends the ATA IDENTIFY PACKET DEVICE command to the device, using default device timeout
        /// </summary>
        /// <returns><c>true</c> if the command failed and <paramref name="statusRegisters"/> contains the error registers.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="statusRegisters">Status registers.</param>
        public bool AtapiIdentify(out byte[] buffer, out AtaErrorRegistersCHS statusRegisters)
        {
            return AtapiIdentify(out buffer, out statusRegisters, Timeout);
        }

        /// <summary>
        /// Sends the ATA IDENTIFY PACKET DEVICE command to the device, using default device timeout
        /// </summary>
        /// <returns><c>true</c> if the command failed and <paramref name="statusRegisters"/> contains the error registers.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="statusRegisters">Status registers.</param>
        /// <param name="duration">Duration.</param>
        public bool AtapiIdentify(out byte[] buffer, out AtaErrorRegistersCHS statusRegisters, out double duration)
        {
            return AtapiIdentify(out buffer, out statusRegisters, Timeout, out duration);
        }

        /// <summary>
        /// Sends the ATA IDENTIFY PACKET DEVICE command to the device
        /// </summary>
        /// <returns><c>true</c> if the command failed and <paramref name="statusRegisters"/> contains the error registers.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="statusRegisters">Status registers.</param>
        /// <param name="timeout">Timeout.</param>
        public bool AtapiIdentify(out byte[] buffer, out AtaErrorRegistersCHS statusRegisters, uint timeout)
        {
            double duration;
            return AtapiIdentify(out buffer, out statusRegisters, timeout, out duration);
        }

        /// <summary>
        /// Sends the ATA IDENTIFY PACKET DEVICE command to the device
        /// </summary>
        /// <returns><c>true</c> if the command failed and <paramref name="statusRegisters"/> contains the error registers.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="statusRegisters">Status registers.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="duration">Duration.</param>
        public bool AtapiIdentify(out byte[] buffer, out AtaErrorRegistersCHS statusRegisters, uint timeout, out double duration)
        {
            buffer = new byte[512];
            AtaRegistersCHS registers = new AtaRegistersCHS();
            bool sense;

            registers.command = (byte)AtaCommands.IdentifyPacketDevice;

            lastError = SendAtaCommand(registers, out statusRegisters, AtaProtocol.PioIn, AtaTransferRegister.NoTransfer,
                ref buffer, timeout, false, out duration, out sense);
            error = lastError != 0;

            DicConsole.DebugWriteLine("ATA Device", "IDENTIFY PACKET DEVICE took {0} ms.", duration);

            return sense;
        }
    }
}

