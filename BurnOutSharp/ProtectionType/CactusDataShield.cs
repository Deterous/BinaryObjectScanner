﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BurnOutSharp.ProtectionType
{
    public class CactusDataShield : IContentCheck, IPathCheck
    {
        /// <inheritdoc/>
        public string CheckContents(string file, byte[] fileContent, bool includePosition = false)
        {
            var mappings = new Dictionary<byte?[], string>
            {
                // DATA.CDS
                [new byte?[] { 0x44, 0x41, 0x54, 0x41, 0x2E, 0x43, 0x44, 0x53 }] = "Cactus Data Shield 200",

                // \*.CDS
                [new byte?[] { 0x5C, 0x2A, 0x2E, 0x43, 0x44, 0x53 }] = "Cactus Data Shield 200",

                // CDSPlayer
                [new byte?[] { 0x43, 0x44, 0x53, 0x50, 0x6C, 0x61, 0x79, 0x65, 0x72 }] = "Cactus Data Shield 200",
            };

            return Utilities.GetContentMatches(fileContent, mappings, includePosition);
        }

        /// <inheritdoc/>
        public string CheckDirectoryPath(string path, IEnumerable<string> files)
        {
            if (files.Any(f => Path.GetFileName(f).Equals("CACTUSPJ.exe", StringComparison.OrdinalIgnoreCase))
                || files.Any(f => Path.GetFileName(f).Equals("CDSPlayer.app", StringComparison.OrdinalIgnoreCase))
                || files.Any(f => Path.GetFileName(f).Equals("PJSTREAM.DLL", StringComparison.OrdinalIgnoreCase))
                || files.Any(f => Path.GetFileName(f).Equals("wmmp.exe", StringComparison.OrdinalIgnoreCase))
                || files.Any(f => Path.GetExtension(f).Trim('.').Equals("cds", StringComparison.OrdinalIgnoreCase)))
            {
                string versionPath = files.FirstOrDefault(f => Path.GetFileName(f).Equals("version.txt", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(versionPath))
                {
                    string version = GetVersion(versionPath);
                    if (!string.IsNullOrWhiteSpace(version))
                        return $"Cactus Data Shield {version}";
                }

                return "Cactus Data Shield 200";
            }

            return null;
        }

        /// <inheritdoc/>
        public string CheckFilePath(string path)
        {
            if (Path.GetFileName(path).Equals("CACTUSPJ.exe", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("CDSPlayer.app", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("PJSTREAM.DLL", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("wmmp.exe", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Trim('.').Equals("cds", StringComparison.OrdinalIgnoreCase))
            {
                return "Cactus Data Shield 200";
            }
            
            return null;
        }

        private static string GetVersion(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using (var sr = new StreamReader(path, Encoding.Default))
                {
                    return $"{sr.ReadLine().Substring(3)} ({sr.ReadLine()})";
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
