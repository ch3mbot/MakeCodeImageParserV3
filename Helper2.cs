using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MakeCodeImageParserV3
{
    internal class Helper2
    {
        public static byte[,] DownsampleFrameSimple(byte[,] frame, int factor)
        {
            if ((frame.GetLength(0) % factor) != 0 || (frame.GetLength(1) % factor) != 0) throw new ArgumentException("Packing not yet implemented. #FIXME");

            int widthIn = frame.GetLength(0);
            int widthOut = widthIn / factor;
            int heightIn = frame.GetLength(1);
            int heightOut = heightIn / factor;

            byte[,] output = new byte[widthOut, heightOut];
            for (int x = 0; x < widthOut; x++)
            {
                for (int y = 0; y < heightOut; y++)
                {
                    output[x, y] = frame[x * factor, y * factor];
                }
            }

            return output;
        }

        // Returns packed colors to 2d chunk 2d thing as rows. ROWS
        public static ChunkedBitstream2D PackColors(byte[,] frame, int colorBits)
        {
            int w = frame.GetLength(0);
            int h = frame.GetLength(1);

            ChunkedBitstream2D output = new ChunkedBitstream2D(colorBits, h, w);
            for (int y = 0; y < h; y++)
            {
                uint mask = ((1U << colorBits) - 1);
                for (int x = 0; x < w; x++)
                {
                    output[y][x] = (frame[x, y] & mask);

                }
            }
            return output;
        }

        // assume col major by deafault
        public static ChunkedBitstream2D PackPixels(ChunkedBitstream2D frame, int packCount, bool flip)
        {
            return frame.Packed(packCount, flip);
        }

        public static byte[,] DeltaEncode2D(byte[,] current, byte[,] previous)
        {
            int w = current.GetLength(0);
            int h = previous.GetLength(1);

            byte[,] outty = new byte[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    outty[x, y] = (byte)(current[x, y] ^ previous[x, y]);
                }
            }
            return outty;
        }

        public static ChunkedBitstream DeltaEncodeStream(ChunkedBitstream curr, ChunkedBitstream prev)
        {
            ChunkedBitstream output = new ChunkedBitstream(curr.BitsPerChunk, curr.ChunkCount);
            for(int i = 0; i < curr.ChunkCount; i++)
            {
                output.SetChunk(i, curr.GetChunk(i) ^ prev.GetChunk(i));
            }
            return output;
        }

        //#FIXME direct to chunk would be faster probably
        public static ChunkedBitstream RLEPart(Bitstream bs, int dataBits, int numberBits)
        {
            int chunkBits = dataBits + numberBits;
            ChunkedBitstream outty = new ChunkedBitstream(chunkBits);

            if ((bs.TotalBits % dataBits) != 0)
            {
                // padding ig #FIXME modifies input, kinda bad
                int needed = (int)(dataBits - (bs.TotalBits % dataBits));
                bs.Add(0, needed);
            }

            int countLimit = 1 << numberBits;

            uint dataMask = (dataBits == 32) ? uint.MaxValue : ((1U << dataBits) - 1);

            uint count = 1;
            for (int i = 1; i < bs.TotalBits / dataBits; i++)
            {
                uint data = bs.GetData(i * dataBits, dataBits);
                uint lastData = bs.GetData((i - 1) * dataBits, dataBits);

                if (data == lastData)
                {
                    count++;
                    if (count > countLimit)
                    {
                        // if past ocunt limit, then add the previous piece of data
                        outty.Add((((data & dataMask) << numberBits) | (count - 2)));
                        count = 1;
                    }
                }
                else
                {
                    // if changed then add the previous piece of data
                    outty.Add((((lastData & dataMask) << numberBits) | (count - 1)));
                    count = 1;
                }
            }

            // add the last one
            outty.Add((((bs.GetData(bs.TotalBits - dataBits, dataBits) & dataMask) << numberBits) | (count - 1)));

            return outty;
        }

        //#FIXME direct from chunk would be faster
        public static ChunkedBitstream DeRLEPart(ChunkedBitstream cbs, int dataBits, int numberBits) 
        {
            return DeRLEPart(cbs.ToBitstream(), dataBits, numberBits); 
        }
        public static ChunkedBitstream DeRLEPart(Bitstream inBS, int dataBits, int numberBits)
        {
            ChunkedBitstream outCBS = new ChunkedBitstream(dataBits);

            for (long i = 0; i < inBS.TotalBits; i += numberBits + dataBits)
            {
                uint cnt = inBS.GetData(i, numberBits);
                uint val = inBS.GetData(i + numberBits, dataBits);
                for (int j = 0; j < cnt + 1; j++)
                {
                    outCBS.Add(val);
                }
            }

            return outCBS;
        }


        public static void FindOptimalPermutations(byte[][,] frames, (int minInclusive, int maxExclusive)[] argRanges)
        {
            int colorBits = 1;

            int factor = 4; //#FIXME do not use this long term

            // pack count 1 (XX) to 16
            // numberBits 1 (XX) to 16
            // delta 0 or 1
            // pack vertical 0 or 1
            // flatten vertical 0 or 1
            int[][] args = new int[argRanges.Length][];
            for (int arg = 0; arg < args.Length; arg++)
            {
                (int minInclusive, int maxExclusive) = argRanges[arg];
                List<int> valsForArg = new List<int>();
                for (int val = minInclusive; val < maxExclusive; val++)
                {
                    valsForArg.Add(val);
                }
                args[arg] = valsForArg.ToArray();
            }

            Func<byte[][,], int[], ChunkedBitstream[]> allCompFuncs = (frames, args) =>
            {
                int packCount = args[0];
                int numberBits = args[1];
                bool doDelta = args[2] > 0 ? true : false;
                bool packVertical = args[3] > 0 ? true : false;
                bool flattenVertical = args[4] > 0 ? true : false;

                int dataBits = colorBits * packCount;

                ChunkedBitstream2D[] allFrames = frames.Select(b => new ChunkedBitstream2D(b, 1)).ToArray();

                // pack
                ChunkedBitstream2D[] packedFrames;
                if (packCount > 1)
                    packedFrames = Helper.ApplyToAllFrames(allFrames, frame => frame.PackedSafe(packCount, packVertical));
                else
                    packedFrames = allFrames;

                // flatten
                ChunkedBitstream[] flattenedFrames = Helper.ApplyToAllFrames(packedFrames, frame => frame.Flatten(flattenVertical));

                // delta encode
                ChunkedBitstream[] deltaEncodedFrames;
                if (doDelta)
                {
                    deltaEncodedFrames = new ChunkedBitstream[frames.Length];
                    deltaEncodedFrames[0] = new ChunkedBitstream(flattenedFrames[0]);
                    for (int i = 1; i < flattenedFrames.Length; i++)
                    {
                        deltaEncodedFrames[i] = DeltaEncodeStream(flattenedFrames[i], flattenedFrames[i - 1]);
                    }
                }
                else
                    deltaEncodedFrames = flattenedFrames;

                // if number bits > 1, then do RLE.
                if (numberBits > 1)
                {
                    return Helper.ApplyToAllFrames(deltaEncodedFrames, frame => RLEPart(frame.ToBitstream(), packCount, numberBits));
                }

                // Just return the frame otherwise
                return Helper.ApplyToAllFrames(deltaEncodedFrames, frame => frame);
            };

            GetBestSavingPermutation(Helper.ApplyToAllFrames(frames, frame => DownsampleFrameSimple(frame, factor)), colorBits, args, allCompFuncs);
        }

        public static void GetBestSavingPermutation(byte[][,] frames, int colorBits, int[][] args, Func<byte[][,], int[], ChunkedBitstream[]> func)
        {
            List<List<int>> paramPermutations = new List<List<int>>() { new List<int>() };

            for (int arg = 0; arg < args.Length; arg++)
            {
                List<List<int>> newParamPermutations = new List<List<int>>();
                // grab everythin in list, add another element with every variation
                foreach (List<int> argList in paramPermutations)
                {
                    // loop through all variations and add to new output list
                    foreach (int paramOption in args[arg])
                    {
                        newParamPermutations.Add(new List<int>(argList) { paramOption });
                    }
                }
                paramPermutations = newParamPermutations;
            }

            Console.WriteLine("param permutation count: " + paramPermutations.Count);
            Console.WriteLine($"Testing on {frames.Length} frames of width {frames[0].GetLength(0)} and length {frames[0].GetLength(1)} with {colorBits} color bits per pixel.");

            long inputBits = frames.Length * frames[0].GetLength(0) * frames[0].GetLength(1) * colorBits;

            long lowestBitcount = inputBits;
            List<int> bestParams = new List<int>() { -1 };
            int numDone = 0;
            var fullStart = DateTime.Now;
            foreach (List<int> permut in paramPermutations)
            {
                var startMs = DateTime.Now;
                long totalBits = func(frames, permut.ToArray()).Sum(f => f.TotalBits);
                if (totalBits < lowestBitcount)
                {
                    lowestBitcount = totalBits;
                    bestParams = permut;
                }
                numDone++;
                Console.WriteLine($"Tested {numDone}/{paramPermutations.Count} ".PadRight(20) + $"bits taken: {totalBits},".PadRight(24) + $"current lowest {lowestBitcount}.".PadRight(24) + $"Took {(int)(DateTime.Now - startMs).TotalMilliseconds}ms.".PadRight(14) + $" Time since start: {(int)(DateTime.Now - fullStart).TotalMilliseconds}ms.");
                if ((numDone % 32) == 0)
                {
                }
            }

            if (bestParams[0] == -1)
            {
                Console.WriteLine("All methods failed.");
            }
            else
            {
                double kb = Math.Ceiling(lowestBitcount / 8.0) / 1024;
                double kbGoal = 96.0;
                Console.WriteLine($"Data now takes: {(double)lowestBitcount / inputBits * 100:00.00}% the space.");
                Console.WriteLine($"kilobytes taken: {kb}. Data must be compressed to {kbGoal / kb * 100:00.00}% of compressed size to fit within {kbGoal} kb.");
                Console.WriteLine("Best params for this: " + string.Join(",", bestParams.Select(n => n.ToString())));
            }
        }
    }
}
