﻿using System;
using System.Collections;
using System.Collections.Generic;
using BurnOutSharp.Models.MicrosoftCabinet;
using BurnOutSharp.Models.MicrosoftCabinet.MSZIP;
using BurnOutSharp.Utilities;

/// <see href="https://interoperability.blob.core.windows.net/files/MS-MCI/%5bMS-MCI%5d.pdf"/>
/// <see href="https://www.rfc-editor.org/rfc/rfc1951"/>
namespace BurnOutSharp.FileType
{
    public static class MSZIPBlockBuilder
    {
        public static Block Create(byte[] data)
        {
            if (data == null)
                return null;

            Block block = new Block();
            int offset = 0;

            block.Signature = data.ReadUInt16(ref offset);
            if (block.Signature != 0x4B43)
                return null;

            block.Data = data.ReadBytes(ref offset, data.Length - 2);

            return block;
        }
    }

    public static class MSZIPDeflateBlockBuilder
    {
        public static DeflateBlock Create(ulong data)
        {
            DeflateBlock deflateBlock = new DeflateBlock();

            deflateBlock.BFINAL = (data & 0b100) != 0;
            deflateBlock.BTYPE = (DeflateCompressionType)(data & 0b011);
            
            return deflateBlock;
        }
    }

    public static class MSZIPDynamicHuffmanCompressedBlockBuilder
    {
        public static DynamicHuffmanCompressedBlock Create(BitStream stream)
        {
            DynamicHuffmanCompressedBlock dynamicHuffmanCompressedBlock = new DynamicHuffmanCompressedBlock();

            // # of Literal/Length codes - 257
            ulong HLIT = stream.ReadBits(5).AsUInt64() + 257;

            // # of Distance codes - 1
            ulong HDIST = stream.ReadBits(5).AsUInt64() + 1;

            // HCLEN, # of Code Length codes - 4
            ulong HCLEN = stream.ReadBits(5).AsUInt64() + 4;

            // (HCLEN + 4) x 3 bits: code lengths for the code length
            //  alphabet given just above
            // 
            //  These code lengths are interpreted as 3-bit integers
            //  (0-7); as above, a code length of 0 means the
            //  corresponding symbol (literal/ length or distance code
            //  length) is not used.
            int[] codeLengthAlphabet = new int[19];
            for (ulong i = 0; i < HCLEN; i++)
                codeLengthAlphabet[MSZIPDeflate.BitLengthOrder[i]] = (int)stream.ReadBits(3).AsUInt64();

            for (ulong i = HCLEN; i < 19; i++)
                codeLengthAlphabet[MSZIPDeflate.BitLengthOrder[i]] = 0;

            // Code length Huffman code
            int[] codeLengthHuffmanCode = MSZIPDeflate.CreateTable(codeLengthAlphabet);

            // HLIT + 257 code lengths for the literal/length alphabet,
            //  encoded using the code length Huffman code
            dynamicHuffmanCompressedBlock.LiteralLengths = BuildHuffmanTree(stream, HLIT, codeLengthHuffmanCode);

            // HDIST + 1 code lengths for the distance alphabet,
            //  encoded using the code length Huffman code
            dynamicHuffmanCompressedBlock.DistanceCodes = BuildHuffmanTree(stream, HDIST, codeLengthHuffmanCode);

            return dynamicHuffmanCompressedBlock;
        }

        /// <summary>
        /// The alphabet for code lengths is as follows
        /// </summary>
        private static int[] BuildHuffmanTree(BitStream stream, ulong codeCount, int[] codeLengths)
        {
            // Setup the huffman tree
            int[] tree = new int[codeCount];

            // Setup the loop variables
            int lastCode = 0, repeatLength = 0;
            for (ulong i = 0; i < codeCount; i++)
            {
                int code = codeLengths[(int)stream.ReadBits(7).AsUInt64()];

                // Represent code lengths of 0 - 15
                if (code > 0 && code <= 15)
                {
                    lastCode = code;
                    tree[i] = code;
                }

                // Copy the previous code length 3 - 6 times.
                // The next 2 bits indicate repeat length (0 = 3, ... , 3 = 6)
                // Example:  Codes 8, 16 (+2 bits 11), 16 (+2 bits 10) will expand to 12 code lengths of 8 (1 + 6 + 5)
                else if (code == 16)
                {
                    repeatLength = (int)stream.ReadBits(2).AsUInt64();
                    repeatLength += 2;
                    code = lastCode;
                }

                // Repeat a code length of 0 for 3 - 10 times.
                // (3 bits of length)
                else if (code == 17)
                {
                    repeatLength = (int)stream.ReadBits(3).AsUInt64();
                    repeatLength += 3;
                    code = 0;
                }

                // Repeat a code length of 0 for 11 - 138 times
                // (7 bits of length)
                else if (code == 18)
                {
                    repeatLength = (int)stream.ReadBits(7).AsUInt64();
                    repeatLength += 11;
                    code = 0;
                }

                // Everything else
                else
                {
                    throw new ArgumentOutOfRangeException();
                }

                // If we had a repeat length
                for (; repeatLength > 0; repeatLength--)
                {
                    tree[i++] = code;
                }
            }

            return tree;
        }
    }

    public static class MSZIPNonCompressedBlockBuilder
    {
        public static NonCompressedBlock Create(byte[] data)
        {
            // If we have invalid header data
            if (data == null || data.Length < 4)
                throw new ArgumentException();

            NonCompressedBlock nonCompressedBlock = new NonCompressedBlock();
            int offset = 0;

            nonCompressedBlock.LEN = data.ReadUInt16(ref offset);
            nonCompressedBlock.NLEN = data.ReadUInt16(ref offset);
            // TODO: Confirm NLEN is 1's compliment of LEN

            return nonCompressedBlock;
        }
    }

    #region Deflate Implementation    

    /// <see href="https://www.rfc-editor.org/rfc/rfc1951"/>
    public class MSZIPDeflate
    {
        #region Constants

        /// <summary>
        /// Maximum Huffman code bit count
        /// </summary>
        public const int MAX_BITS = 16;

        #endregion

        #region Properties

        /// <summary>
        /// Match lengths for literal codes 257..285
        /// </summary>
        /// <remarks>Each value here is the lower bound for lengths represented</remarks>
        public static Dictionary<int, int> LiteralLengths
        {
            get
            {
                // If we have cached length mappings, use those
                if (_literalLengths != null)
                    return _literalLengths;

                // Otherwise, build it from scratch
                _literalLengths = new Dictionary<int, int>
                {
                    [257] = 3,
                    [258] = 4,
                    [259] = 5,
                    [260] = 6,
                    [261] = 7,
                    [262] = 8,
                    [263] = 9,
                    [264] = 10,
                    [265] = 11, // 11,12
                    [266] = 13, // 13,14
                    [267] = 15, // 15,16
                    [268] = 17, // 17,18
                    [269] = 19, // 19-22
                    [270] = 23, // 23-26
                    [271] = 27, // 27-30
                    [272] = 31, // 31-34
                    [273] = 35, // 35-42
                    [274] = 43, // 43-50
                    [275] = 51, // 51-58
                    [276] = 59, // 59-66
                    [277] = 67, // 67-82
                    [278] = 83, // 83-98
                    [279] = 99, // 99-114
                    [280] = 115, // 115-130
                    [281] = 131, // 131-162
                    [282] = 163, // 163-194
                    [283] = 195, // 195-226
                    [284] = 227, // 227-257
                    [285] = 258,
                };

                return _literalLengths;
            }
        }

        /// <summary>
        /// Extra bits for literal codes 257..285
        /// </summary>
        public static Dictionary<int, int> LiteralExtraBits
        {
            get
            {
                // If we have cached bit mappings, use those
                if (_literalExtraBits != null)
                    return _literalExtraBits;

                // Otherwise, build it from scratch
                _literalExtraBits = new Dictionary<int, int>();

                // Literal Value 257 - 264, 0 bits
                for (int i = 257; i < 265; i++)
                    _literalExtraBits[i] = 0;

                // Literal Value 265 - 268, 1 bit
                for (int i = 265; i < 269; i++)
                    _literalExtraBits[i] = 1;

                // Literal Value 269 - 272, 2 bits
                for (int i = 269; i < 273; i++)
                    _literalExtraBits[i] = 2;

                // Literal Value 273 - 276, 3 bits
                for (int i = 273; i < 277; i++)
                    _literalExtraBits[i] = 3;

                // Literal Value 277 - 280, 4 bits
                for (int i = 277; i < 281; i++)
                    _literalExtraBits[i] = 4;

                // Literal Value 281 - 284, 5 bits
                for (int i = 281; i < 285; i++)
                    _literalExtraBits[i] = 5;

                // Literal Value 285, 0 bits
                _literalExtraBits[285] = 0;

                return _literalExtraBits;
            }
        }

        /// <summary>
        /// Match offsets for distance codes 0..29
        /// </summary>
        /// <remarks>Each value here is the lower bound for lengths represented</remarks>
        public static readonly int[] DistanceOffsets = new int[30]
        {
            1, 2, 3, 4, 5, 7, 9, 13, 17, 25,
            33, 49, 65, 97, 129, 193, 257, 385, 513, 769,
            1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577,
        };

        /// <summary>
        /// Extra bits for distance codes 0..29
        /// </summary>
        public static readonly int[] DistanceExtraBits = new int[30]
        {
            0, 0, 0, 0, 1, 1, 2, 2, 3, 3,
            4, 4, 5, 5, 6, 6, 7, 7, 8, 8,
            9, 9, 10, 10, 11, 11, 12, 12, 13, 13,
        };

        /// <summary>
        /// The order of the bit length Huffman code lengths
        /// </summary>
        public static readonly int[] BitLengthOrder = new int[19]
        {
            16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15,
        };

        #endregion

        #region Instance Variables

        /// <summary>
        /// Match lengths for literal codes 257..285
        /// </summary>
        private static Dictionary<int, int> _literalLengths = null;

        /// <summary>
        /// Extra bits for literal codes 257..285
        /// </summary>
        private static Dictionary<int, int> _literalExtraBits = null;

        #endregion

        /// <summary>
        /// The decoding algorithm for the actual data
        /// </summary>
        public static void Decode(BitStream data)
        {
            // Create the output byte array
            List<byte> decodedBytes = new List<byte>();

            // Create the loop variable block
            DeflateBlock block;

            do
            {
                ulong header = data.ReadBits(3).AsUInt64();
                block = MSZIPDeflateBlockBuilder.Create(header);

                // We should never get a reserved block
                if (block.BTYPE == DeflateCompressionType.Reserved)
                    throw new Exception();

                // If stored with no compression
                if (block.BTYPE == DeflateCompressionType.NoCompression)
                {
                    // Skip any remaining bits in current partially processed byte
                    data.DiscardBuffer();

                    // Read LEN and NLEN
                    byte[] nonCompressedHeader = data.ReadBytes(4);
                    block.BlockData = MSZIPNonCompressedBlockBuilder.Create(nonCompressedHeader);

                    // Copy LEN bytes of data to output
                    ushort length = ((NonCompressedBlock)block.BlockData).LEN;
                    ((NonCompressedBlock)block.BlockData).Data = data.ReadBytes(length);
                    decodedBytes.AddRange(((NonCompressedBlock)block.BlockData).Data);
                }

                // Otherwise
                else
                {
                    // If compressed with dynamic Huffman codes
                    // read representation of code trees
                    block.BlockData = block.BTYPE == DeflateCompressionType.DynamicHuffman
                        ? (IBlockData)MSZIPDynamicHuffmanCompressedBlockBuilder.Create(data)
                        : (IBlockData)new FixedHuffmanCompressedBlock();

                    var compressedBlock = (block.BlockData as CompressedBlock);

                    // 9 bits per entry, 288 max symbols
                    int[] literalDecodeTable = CreateTable(compressedBlock.LiteralLengths);

                    // 6 bits per entry, 32 max symbols
                    int[] distanceDecodeTable = CreateTable(compressedBlock.DistanceCodes);

                    // Loop until end of block code recognized
                    while (true)
                    {
                        // Decode literal/length value from input stream
                        int symbol = literalDecodeTable[data.ReadBits(9).AsUInt64()];

                        // Copy value (literal byte) to output stream
                        if (symbol < 256)
                        {
                            decodedBytes.Add((byte)symbol);
                        }
                        // End of block (256)
                        else if (symbol == 256)
                        {
                            break;
                        }
                        else
                        {
                            // Decode distance from input stream
                            ulong length = data.ReadBits(LiteralExtraBits[symbol]).AsUInt64();
                            length += (ulong)LiteralLengths[symbol];

                            int code = distanceDecodeTable[length];

                            ulong distance = data.ReadBits(DistanceExtraBits[code]).AsUInt64();
                            distance += (ulong)DistanceOffsets[code];


                            // Move backwards distance bytes in the output
                            // stream, and copy length bytes from this
                            // position to the output stream.
                        }
                    }
                }
            } while (!block.BFINAL);

            /*
             Note that a duplicated string reference may refer to a string
             in a previous block; i.e., the backward distance may cross one
             or more block boundaries.  However a distance cannot refer past
             the beginning of the output stream.  (An application using a
             preset dictionary might discard part of the output stream; a
             distance can refer to that part of the output stream anyway)
             Note also that the referenced string may overlap the current
             position; for example, if the last 2 bytes decoded have values
             X and Y, a string reference with <length = 5, distance = 2>
             adds X,Y,X,Y,X to the output stream.
            */
        }

        /// <summary>
        /// Given this rule, we can define the Huffman code for an alphabet
        /// just by giving the bit lengths of the codes for each symbol of
        /// the alphabet in order; this is sufficient to determine the
        /// actual codes.  In our example, the code is completely defined
        /// by the sequence of bit lengths (2, 1, 3, 3).  The following
        /// algorithm generates the codes as integers, intended to be read
        /// from most- to least-significant bit.  The code lengths are
        /// initially in tree[I].Len; the codes are produced in
        /// tree[I].Code.
        /// </summary>
        public static void CreateTable(CompressedBlock tree)
        {
            // Count the number of codes for each code length.  Let
            // bl_count[N] be the number of codes of length N, N >= 1.
            var bl_count = new Dictionary<int, int>();
            for (int i = 0; i < tree.LiteralLengths.Length; i++)
            {
                if (!bl_count.ContainsKey(tree.LiteralLengths[i]))
                    bl_count[tree.LiteralLengths[i]] = 0;

                bl_count[tree.LiteralLengths[i]]++;
            }

            // Find the numerical value of the smallest code for each
            // code length:
            var next_code = new Dictionary<int, int>();
            int code = 0;
            bl_count[0] = 0;
            for (int bits = 1; bits <= MAX_BITS; bits++)
            {
                code = (code + bl_count[bits - 1]) << 1;
                next_code[bits] = code;
            }

            // Assign numerical values to all codes, using consecutive
            // values for all codes of the same length with the base
            // values determined at step 2. Codes that are never used
            // (which have a bit length of zero) must not be assigned a
            // value.
            for (int n = 0; n <= tree.LiteralLengths.Length; n++)
            {
                int len = tree.LiteralLengths[n];
                if (len != 0)
                {
                    tree.DistanceCodes[n] = next_code[len];
                    next_code[len]++;
                }
            }
        }

        /// <summary>
        /// Given this rule, we can define the Huffman code for an alphabet
        /// just by giving the bit lengths of the codes for each symbol of
        /// the alphabet in order; this is sufficient to determine the
        /// actual codes.  In our example, the code is completely defined
        /// by the sequence of bit lengths (2, 1, 3, 3).  The following
        /// algorithm generates the codes as integers, intended to be read
        /// from most- to least-significant bit.  The code lengths are
        /// initially in tree[I].Len; the codes are produced in
        /// tree[I].Code.
        /// </summary>
        public static int[] CreateTable(int[] lengths)
        {
            // Count the number of codes for each code length.  Let
            // bl_count[N] be the number of codes of length N, N >= 1.
            var bl_count = new Dictionary<int, int>();
            for (int i = 0; i < lengths.Length; i++)
            {
                if (!bl_count.ContainsKey(lengths[i]))
                    bl_count[lengths[i]] = 0;

                bl_count[lengths[i]]++;
            }

            // Find the numerical value of the smallest code for each
            // code length:
            var next_code = new Dictionary<int, int>();
            int code = 0;
            bl_count[0] = 0;
            for (int bits = 1; bits <= MAX_BITS; bits++)
            {
                code = (code + bl_count[bits - 1]) << 1;
                next_code[bits] = code;
            }

            // Assign numerical values to all codes, using consecutive
            // values for all codes of the same length with the base
            // values determined at step 2. Codes that are never used
            // (which have a bit length of zero) must not be assigned a
            // value.
            int[] distances = new int[lengths.Length];
            for (int n = 0; n <= lengths.Length; n++)
            {
                int len = lengths[n];
                if (len != 0)
                {
                    distances[n] = next_code[len];
                    next_code[len]++;
                }
            }

            return distances;
        }
    }

    #endregion
}
