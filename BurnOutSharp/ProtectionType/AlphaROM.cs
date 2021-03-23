﻿using System.Collections.Generic;
using BurnOutSharp.Matching;

namespace BurnOutSharp.ProtectionType
{
    public class AlphaROM : IContentCheck
    {
        /// <inheritdoc/>
        public string CheckContents(string file, byte[] fileContent, bool includePosition = false)
        {
            var matchers = new List<ContentMatchSet>
            {
                // SETTEC
                new ContentMatchSet(new byte?[] { 0x53, 0x45, 0x54, 0x54, 0x45, 0x43 }, "Alpha-ROM"),
            };

            return MatchUtil.GetFirstContentMatch(file, fileContent, matchers, includePosition);
        }
    }
}
