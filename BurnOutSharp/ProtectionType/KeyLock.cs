﻿using System.Collections.Generic;

namespace BurnOutSharp.ProtectionType
{
    public class KeyLock : IContentCheck
    {
        /// <inheritdoc/>
        public string CheckContents(string file, byte[] fileContent, bool includePosition = false)
        {
            var mappings = new Dictionary<byte?[], string>
            {
                // KEY-LOCK COMMAND
                [new byte?[] { 0x4B, 0x45, 0x59, 0x2D, 0x4C, 0x4F, 0x43, 0x4B, 0x20, 0x43, 0x4F, 0x4D, 0x4D, 0x41, 0x4E, 0x44 }] = "Key-Lock (Dongle)",
            };

            return Utilities.GetContentMatches(fileContent, mappings, includePosition);
        }
    }
}
