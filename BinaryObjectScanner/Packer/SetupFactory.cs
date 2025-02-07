﻿using System;
using System.IO;
using BinaryObjectScanner.Interfaces;
using SabreTools.Serialization.Wrappers;

namespace BinaryObjectScanner.Packer
{
    // TODO: Add extraction, which is possible but the only tools available that can
    // do this seem to be Universal Extractor 2 and InstallExplorer (https://totalcmd.net/plugring/InstallExplorer.html)
    // https://raw.githubusercontent.com/wolfram77web/app-peid/master/userdb.txt
    public class SetupFactory : IExtractable, IPortableExecutableCheck
    {
        /// <inheritdoc/>
        public string? CheckPortableExecutable(string file, PortableExecutable pex, bool includeDebug)
        {
            // Get the sections from the executable, if possible
            var sections = pex.Model.SectionTable;
            if (sections == null)
                return null;

            // Known to detect versions 7.0.5.1 - 9.1.0.0
            var name = pex.LegalCopyright;
            if (name?.StartsWith("Setup Engine", StringComparison.OrdinalIgnoreCase) == true)
                return $"Setup Factory {GetVersion(pex)}";

            name = pex.ProductName;
            if (name?.StartsWith("Setup Factory", StringComparison.OrdinalIgnoreCase) == true)
                return $"Setup Factory {GetVersion(pex)}";

            // Known to detect version 5.0.1 - 6.0.1.3
            name = pex.FileDescription;
            if (name?.StartsWith("Setup Factory", StringComparison.OrdinalIgnoreCase) == true)
                return $"Setup Factory {GetVersion(pex)}";

            // Longer version of the check that can be used if false positves become an issue:
            // "Setup Factory is a trademark of Indigo Rose Corporation"

            return null;
        }

        /// <inheritdoc/>
        public string? Extract(string file, bool includeDebug)
        {
            if (!File.Exists(file))
                return null;

            using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Extract(fs, file, includeDebug);
            }
        }

        /// <inheritdoc/>
        public string? Extract(Stream? stream, string file, bool includeDebug)
        {
            return null;
        }
    
        private string GetVersion(PortableExecutable pex)
        {
            // Check the product version explicitly
            var version = pex.ProductVersion;
            if (!string.IsNullOrEmpty(version))
                return version!;

            // Check the internal versions
            version = pex.GetInternalVersion();
            if (!string.IsNullOrEmpty(version))
                return version!;

            return "(Unknown Version)";
        }
    }
}
