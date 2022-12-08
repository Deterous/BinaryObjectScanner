﻿using System.Collections.Generic;
using System.Linq;
using BurnOutSharp.Interfaces;
using BurnOutSharp.Matching;
using BurnOutSharp.Wrappers;

namespace BurnOutSharp.ProtectionType
{
    public class CDSHiELDSE : IPortableExecutableCheck
    {
        /// <inheritdoc/>
        public string CheckPortableExecutable(string file, PortableExecutable pex, bool includeDebug)
        {
            // Get the sections from the executable, if possible
            var sections = pex?.SectionTable;
            if (sections == null)
                return null;

            // TODO: Indicates Hypertech Crack Proof as well?
            //// Get the import directory table
            //if (pex.ImportTable?.ImportDirectoryTable != null)
            //{
            //    bool match = pex.ImportTable.ImportDirectoryTable.Any(idte => idte.Name == "KeRnEl32.dLl");
            //    if (match)
            //        return "CDSHiELD SE";
            //}

            // Get the code/CODE section, if it exists
            var codeSectionRaw = pex.GetFirstSectionData("code") ?? pex.GetFirstSectionData("CODE");
            if (codeSectionRaw != null)
            {
                var matchers = new List<ContentMatchSet>
                {
                    // ~0017.tmp
                    new ContentMatchSet(new byte?[] { 0x7E, 0x30, 0x30, 0x31, 0x37, 0x2E, 0x74, 0x6D, 0x70 }, "CDSHiELD SE"),
                };

                string match = MatchUtil.GetFirstMatch(file, codeSectionRaw, matchers, includeDebug);
                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }

            return null;
        }
    }
}
