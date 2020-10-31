﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace BurnOutSharp.FileType
{
    internal class BZip2
    {
        public static bool ShouldScan(byte[] magic)
        {
            if (magic.StartsWith(new byte[] { 0x42, 0x52, 0x68 }))
                return true;

            return false;
        }

        public static Dictionary<string, List<string>> Scan(Scanner parentScanner, Stream stream)
        {
            // If the 7-zip file itself fails
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

                using (BZip2Stream bz2File = new BZip2Stream(stream, CompressionMode.Decompress, true))
                {
                    // If an individual entry fails
                    try
                    {
                        string tempFile = Path.Combine(tempPath, Guid.NewGuid().ToString());
                        using (FileStream fs = File.OpenWrite(tempFile))
                        {
                            bz2File.CopyTo(fs);
                        }
                    }
                    catch { }
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
