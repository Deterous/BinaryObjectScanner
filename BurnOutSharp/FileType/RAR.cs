﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;

namespace BurnOutSharp.FileType
{
    internal class RAR
    {
        public static bool ShouldScan(byte[] magic)
        {
            // RAR archive version 1.50 onwards
            if (magic.StartsWith(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x00 }))
                return true;

            // RAR archive version 5.0 onwards
            if (magic.StartsWith(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x01, 0x00 }))
                return true;

            return false;
        }

        public static Dictionary<string, List<string>> Scan(Scanner parentScanner, Stream stream)
        {
            // If the rar file itself fails
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                // Create a new scanner for the new temp path
                Scanner subScanner = new Scanner(parentScanner.FileProgress)
                {
                    IncludePosition = parentScanner.IncludePosition,
                    ScanAllFiles = parentScanner.ScanAllFiles,
                    ScanArchives = parentScanner.ScanArchives,
                };

                using (RarArchive zipFile = RarArchive.Open(stream))
                {
                    foreach (var entry in zipFile.Entries)
                    {
                        // If an individual entry fails
                        try
                        {
                            // If we have a directory, skip it
                            if (entry.IsDirectory)
                                continue;

                            string tempFile = Path.Combine(tempPath, entry.Key);
                            entry.WriteToFile(tempFile);
                        }
                        catch { }
                    }
                }

                // Collect and format all found protections
                var protections = subScanner.GetProtections(tempPath);

                // If temp directory cleanup fails
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch { }

                return protections;
            }
            catch { }

            return null;
        }
    }
}
