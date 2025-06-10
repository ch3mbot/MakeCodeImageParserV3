using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MakeCodeImageParserV3
{
    internal static class HuffmanTester
    {
        public static void HuffmanTest(ChunkedBitstream inputCbs)
        {
            Console.WriteLine("Basic test: ");
            // save bits per chunk and chunk count for decoding
            int bitsPerChunk = inputCbs.BitsPerChunk;
            int chunkCount = inputCbs.ChunkCount;

            // get freq
            Dictionary<uint, int> freq = HuffmanEncoder.BuildFrequencyTable(new ChunkedBitstream[] { inputCbs });

            // build encoding
            HuffmanNode root = HuffmanEncoder.BuildHuffmanTree(freq);
            Dictionary<uint, int> codeLengths = HuffmanEncoder.GetCodeLengths(root);
            Dictionary<uint, (uint, int)> encodingTable = HuffmanEncoder.AssignCanonicalCodes(codeLengths);

            // encode
            Bitstream encodedStream = HuffmanEncoder.EncodeSingleStream(inputCbs, encodingTable);

            // write codebook
            Bitstream codebook = HuffmanEncoder.WriteCodebook(codeLengths);

            // read codebook
            Dictionary<uint, int> decodedLengths = HuffmanEncoder.ReadCodebook(codebook);
            Dictionary<uint, (uint, int)> decodingTable = HuffmanEncoder.AssignCanonicalCodes(decodedLengths);

         

            // decode
            ChunkedBitstream decodedCbs = HuffmanEncoder.Decode(encodedStream, decodingTable, bitsPerChunk, out long finalOffset);

            Console.WriteLine("encoded: ".PadRight(16) + inputCbs.ToString(",", "X2"));
            Console.WriteLine("decoded: ".PadRight(16) + decodedCbs.ToString(",", "X2"));

            bool failed = false;
            for(int i = 0; i < chunkCount; i++)
            {
                if(inputCbs[i] != decodedCbs[i])
                {
                    failed = true;
                    Console.WriteLine("Mismatch at index " + i + " value " + decodedCbs[i].ToString("X") + " should be " + inputCbs[i].ToString("X") + ".");
                }
            }
            Console.WriteLine("Huffman encoding " + (failed ? "failed." : "sucecss!"));
            Console.WriteLine();

            Console.WriteLine("Huffman test with frame: ");

        }
    }
}
