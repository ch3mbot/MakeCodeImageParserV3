using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MakeCodeImageParserV3
{
    internal static class Helper
    {
        /// <summary>
        /// Applies a certain function to all frames.
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="O"></typeparam>
        /// <param name="frames"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static O[] ApplyToAllFrames<I,O>(I[] frames, Func<I, O> func)
        {
            O[] allProcessedFrames = new O[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                allProcessedFrames[i] = func(frames[i]);
            }
            return allProcessedFrames;
        }

        /// <summary>
        /// Given a compression function and input data, find out how many bits that input data becomes
        /// </summary>
        /// <param name="frames"></param>
        /// <param name="colorBits"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static long TotalBits(byte[][,] frames, Func<byte[,], Bitstream> func) { return frames.Sum(frame => func(frame).TotalBits); }
        public static long TotalBits(byte[][,] frames, Func<byte[][,], Bitstream> func) { return func(frames).TotalBits; }
        public static long TotalBits(byte[][,] frames, Func<byte[][,], Bitstream[]> func) { return func(frames).Sum(bs => bs.TotalBits); }

        public static void EstimateSavings(byte[][,] frames, int colorBits, Func<byte[,], Bitstream> func, string name)
        {
            Console.WriteLine("");
            Console.WriteLine("Estimating savings for " + name);
            string prefix = (name + ":").PadRight(20).PadLeft(4);
            Console.WriteLine($"{prefix}Frames: {frames.Length}, w: {frames[0].GetLength(0)}, h: {frames[0].GetLength(0)}, colorBits: {colorBits}");
            long inputBits = frames.Length * frames[0].GetLength(0) * frames[0].GetLength(1) * colorBits;
            Console.WriteLine($"{prefix}Total bits taken by input: {inputBits} ~= {(double)inputBits / 8} bytes");
            long outputBits = TotalBits(frames, func);
            Console.WriteLine($"{prefix}Total bits taken by output: {outputBits} ~= {(double)outputBits / 8} bytes");

            Console.WriteLine($"{prefix}Data now takes: {(double)outputBits / inputBits * 100:00.00}% the space.");
            
            double kb = Math.Ceiling(outputBits / 8.0) / 1024;
            double kbGoal = 96.0;
            Console.WriteLine($"{prefix}kilobytes taken: {kb}. Data must be compressed to {kbGoal / kb * 100:00.00}% of compressed size to fit within {kbGoal} kb.");
        }

        // tuple of min and max (inclusive, exclusive) nums to try.
        public static void GetBestSavingPermutation(byte[][,] frames, int colorBits, int[][] args, Func<byte[][,], int[], Bitstream[]> func)
        {
            List<List<int>> paramPermutations = new List<List<int>>() { new List<int>() };

            for (int arg = 0; arg < args.Length; arg++)
            {
                List<List<int>> newParamPermutations = new List<List<int>>();
                // grab everythin in list, add another element with every variation
                foreach(List<int> argList in paramPermutations)
                {
                    // loop through all variations and add to new output list
                    foreach(int paramOption in args[arg])
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
                long totalBits = TotalBits(frames, frame => func(frame, permut.ToArray()));
                if(totalBits < lowestBitcount)
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

        /// <summary>
        /// Just sample every factor pixels. Ignore pixels inbetween, do not blend, error on any issues.
        /// This function should not be used other than for estimation. In reality ffmpeg should output different res/framerate.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="factor"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[,] DownsampleFrameSimple(byte[,] frame, int factor)
        {
            if ((frame.GetLength(0) % factor) != 0 || (frame.GetLength(1) % factor) != 0) throw new ArgumentException("Packing not yet implemented. #FIXME");

            int widthIn = frame.GetLength(0);
            int widthOut = widthIn / factor;
            int heightIn = frame.GetLength(1);
            int heightOut = heightIn / factor;

            byte[,] output = new byte[widthOut, heightOut];
            for(int x = 0; x < widthOut; x++)
            {
                for(int y = 0; y < heightOut; y++)
                {
                    output[x, y] = frame[x * factor, y * factor];
                }
            }

            return output;
        }

        /// <summary>
        /// Flattens a 2dbyte frame horizontally (rows placed end to end)
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public static byte[] FlattenFrameHorizontal(byte[,] frame)
        {
            int width = frame.GetLength(0);
            int height = frame.GetLength(1);
            byte[] output = new byte[width * height];
            int index = 0;
            for(int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    output[index] = frame[x, y];
                    index++;
                }
            }
            return output;
        }

        public static Bitstream FlattenFrameHorizontalStreamed(ChunkedBitstream[] rows) { return FlattenFrameHorizontalStreamed(rows.Select(cbs => cbs.ToBitstream()).ToArray()); }
        public static Bitstream FlattenFrameHorizontalStreamed(Bitstream[] rows)
        {
            Bitstream outputStream = new Bitstream();
            Console.WriteLine("Some data:");
            for (int i = 0; i < rows.Length; i++)
            {
                Console.Write(rows[i].GetData(4, 4));
                outputStream.Append(rows[i]);
            }
            Console.WriteLine();

            Console.WriteLine("Some other data:");
            for (int i = 0; i < rows.Length; i++)
            {
                Console.Write(outputStream.GetData(4 * i, 4));
            }
            Console.WriteLine();
            return outputStream;
        }

        // duplicate lmao why
        public static Bitstream FlattenFrameVerticalStreamed(ChunkedBitstream[] cols) { return FlattenFrameHorizontalStreamed(cols.Select(cbs => cbs.ToBitstream()).ToArray()); }
        public static Bitstream FlattenFrameVerticalStreamed(Bitstream[] cols)
        {
            Bitstream outputStream = new Bitstream();
            Console.WriteLine("Some data:");
            for (int i = 0; i < cols.Length; i++)
            {
                Console.Write(cols[i].GetData(4, 4));
                outputStream.Append(cols[i]);
            }
            Console.WriteLine();

            Console.WriteLine("Some other data:");
            for (int i = 0; i < cols.Length; i++)
            {
                Console.Write(outputStream.GetData(4 * i, 4));
            }
            Console.WriteLine();
            return outputStream;
        }

        /// <summary>
        /// Flattens a 2D byte frame vertically (cols placed end to end)
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public static byte[] FlattenFrameVertical(byte[,] frame)
        {
            int width = frame.GetLength(0);
            int height = frame.GetLength(1);
            byte[] output = new byte[width * height];
            int index = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    output[index] = frame[x, y];
                    index++;
                }
            }
            return output;
        }

        /// <summary>
        /// Unflattends a 1D byte frame into a 2D byte frame (opposite of FlattenHorizontal)
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static byte[,] UnflattenFrameHorizontal(byte[] frame, int width, int height)
        {
            byte[,] output = new byte[width, height];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    output[x, y] = frame[index];
                    index++;
                }
            }
            return output;
        }

        // #FIXME width and height are swapped randomly no idea what's going on
        public static Bitstream[] UnflattenFrameHorizontalStreamed(Bitstream stream, int width, int height)
        {
            Bitstream[] output = new Bitstream[height];
            for (int i = 0; i < height; i++)
            {
                output[i] = stream.TakeSegment(i * width, width);
            }
            return output;
        }
        public static Bitstream[] UnflattenFrameVerticalStreamed(Bitstream stream, int width, int height)
        {
            Bitstream[] output = new Bitstream[width];
            for (int i = 0; i < width; i++)
            {
                output[i] = stream.TakeSegment(i * height, height);
            }
            return output;
        }

        /// <summary>
        /// Unflattends a 1D byte frame into a 2D byte frame (opposite of FlattenVertical)
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static byte[,] UnflattenFrameVertical(byte[] frame, int width, int height)
        {
            byte[,] output = new byte[width, height];
            int index = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    output[x, y] = frame[index];
                    index++;
                }
            }
            return output;
        }

        /// <summary>
        /// A byte array comparer
        /// </summary>
        public class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] left, byte[] right)
            {
                if (left == null || right == null)
                {
                    return left == right;
                }
                return left.SequenceEqual(right);
            }
            public int GetHashCode(byte[] key)
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return key.Sum(b => b);
            }
        }

        /// <summary>
        /// Old code, replaced by modular chunk packer.
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public static byte[,] Pack8To1Horizontal(byte[,] imageBytes)
        {
            int w = imageBytes.GetLength(0);
            int h = imageBytes.GetLength(1);

            byte[,] outty = new byte[w / 8, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w / 8; x++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        outty[x, y] |= (byte)(imageBytes[x * 8 + i, y] << (i));
                    }
                }
            }

            return outty;
        }

        /// <summary>
        /// Old code, replaced by modular chunk packer.
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public static byte[,] Unpack8To1Horizontal(byte[,] imageBytes)
        {
            int w = imageBytes.GetLength(0) * 8;
            int h = imageBytes.GetLength(1);

            byte[,] outty = new byte[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w / 8; x++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        outty[x * 8 + i, y] = (byte)((imageBytes[x, y] >> i) & 1);
                    }
                }
            }

            return outty;
        }

        /// <summary>
        /// Old code, replaced by modular chunk packer.
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public static byte[,] Pack8To1Vertical(byte[,] imageBytes)
        {
            int w = imageBytes.GetLength(0);
            int h = imageBytes.GetLength(1);

            byte[,] outty = new byte[w, h / 8];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h / 8; y++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        outty[x, y] |= (byte)(imageBytes[x, y * 8 + i] << (7 - i));
                    }
                }
            }

            return outty;
        }

        // #FIXME add safety for bad arguments
        /// <summary>
        /// Given a 2d byte frame, squish horizontally, packing a certain number of pixels together
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        public static ChunkedBitstream[] PackHorizontal(byte[,] frame, int colorBits, int packCount)
        {
            int w = frame.GetLength(0);
            int h = frame.GetLength(1);

            ChunkedBitstream[] output = new ChunkedBitstream[h];
            for (int y = 0; y < h; y++)
            {
                output[y] = new ChunkedBitstream(packCount * colorBits);
                uint mask = ((1U << colorBits) - 1);
                for (int x = 0; x < w; x += packCount)
                {
                    uint chunk = 0U;
                    for (int spix = 0; spix < packCount; spix++)
                    {
                        if (x + spix >= w) continue;
                        chunk |= ((frame[x + spix, y] & mask) << (colorBits * spix));
                    }
                    output[y].Add(chunk);

                }
            }
            return output;
        }

        public static byte[,] PackHorizontalToBox(byte[,] frame, int colorBits, int packCount)
        {
            return ChunkArrToByteBoxHorizontal(PackHorizontal(frame, colorBits, packCount), colorBits, packCount);
        }
        public static byte[,] PackVerticalToBox(byte[,] frame, int colorBits, int packCount)
        {
            return ChunkArrToByteBoxVertical(PackVertical(frame, colorBits, packCount));
        }

        /// <summary>
        /// Unpack a 2D byte frame horizontally, unpacking a certain number of pixels from each byte 
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        // #FIXME this should run with chunkstreams in theory
        public static byte[,] UnpackHorizontal(byte[,] frame, int colorBits, int packCount)
        {
            int w = frame.GetLength(0) * packCount;
            int h = frame.GetLength(1);
            byte[,] output = new byte[w, h];

            for (int y = 0; y < h; y++)
            {
                uint mask = ((1U << colorBits) - 1);
                for (int x = 0; x < w; x += packCount)
                {
                    uint chunk = frame[x / packCount, y];
                    for (int spix = 0; spix < packCount; spix++)
                    {
                        output[x + spix, y] = (byte)((chunk >> (colorBits * spix)) & mask);
                    }

                }
            }
            return output;
        }

        // #FIXME add exceptions for bad arguments
        /// <summary>
        /// Given a 2d byte frame, squish vertically, packing a certain number of pixels together
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        public static ChunkedBitstream[] PackVertical(byte[,] frame, int colorBits, int packCount)
        {
            int w = frame.GetLength(0);
            int h = frame.GetLength(1);

            ChunkedBitstream[] outputCols = new ChunkedBitstream[w];
            for (int x = 0; x < w; x++)
            {
                outputCols[x] = new ChunkedBitstream(packCount * colorBits);
                uint mask = ((1U << colorBits) - 1);
                for (int y = 0; y < h; y += packCount)
                {
                    uint chunk = 0U;
                    int remaining = Math.Min(packCount, w - x);
                    for (int spix = 0; spix < packCount; spix++)
                    {
                        if (y + spix >= h) continue;
                        chunk |= ((frame[x, y + spix] & mask) << (colorBits * spix));
                    }
                    outputCols[x].Add(chunk);

                }
            }
            return outputCols;
        }

        /// <summary>
        /// Unpack a 2D byte frame verticaally, unpacking a certain number of pixels from each byte 
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        // #FIXME this should run with chunkstreams in theory
        public static byte[,] UnpackVertical(byte[,] frame, int colorBits, int packCount)
        {
            int w = frame.GetLength(0);
            int h = frame.GetLength(1) * packCount;
            byte[,] output = new byte[w, h];

            int addedPixels = 0;
            for (int x = 0; x < w; x++)
            {
                uint mask = ((1U << colorBits) - 1);
                for (int y = 0; y < h; y += packCount)
                {
                    uint chunk = frame[x, y / packCount];
                    int remaining = Math.Min(packCount, h - y);
                    for (int spix = 0; spix < remaining; spix++)
                    {
                        output[x, y + spix] = (byte)((chunk >> (colorBits * spix)) & mask);
                        addedPixels++;
                    }

                }
            }
            return output;
        }

        /// <summary>
        /// Convert a chunked bitstream array into a 2d byte array horizontally. Each chunkedbitstream represents one row.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        // #FIXME only works for 8s ig? this sucks.
        // #FIXME redo this whole thing later this is horrid
        // #FIXME maybe works now?
        public static byte[,] ChunkArrToByteBoxHorizontal(ChunkedBitstream[] frame, int colorBits, int packCount)
        {
            int width = frame[0].ChunkCount;
            int height = frame.Length;

            byte[,] packed = new byte[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    packed[x, y] = (byte)frame[y].GetChunk(x);
                }
            }

            return packed;
        }

        /// <summary>
        /// Convert a chunked bitstream array into a 2d byte array vertically. Each chunkedbitstream represents one column.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        // #FIXME make work with bits like func above? prob.
        public static byte[,] ChunkArrToByteBoxVertical(ChunkedBitstream[] frame)
        {
            /*
            int height = frame[0].GetCompressedData().Length; //#FIXME is this correct for padding? seems dubious...
            byte[,] output = new byte[frame.Length, height];
            for (int col = 0; col < frame.Length; col++)
            {
                byte[] colData = frame[col].GetCompressedData();
                for (int row = 0; row < height; row++)
                {
                    output[col, row] = colData[row];
                }
            }
            return output;*/


            int width = frame.Length;
            int height = frame[0].ChunkCount;

            byte[,] packed = new byte[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    packed[x, y] = (byte)frame[x].GetChunk(y);
                }
            }

            return packed;
        }

        /// <summary>
        /// Pack a linear pixel stream of bytes into a chunked bitstream.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static ChunkedBitstream PackLinear(byte[] frame, int colorBits, int packCount)
        {
            int totalPixels = frame.Length;
            if((totalPixels % packCount) != 0) throw new ArgumentException("Total pixels must be divisible by pack count.");

            int bitsPerChunk = colorBits * packCount;

            if ((bitsPerChunk < 1) || (bitsPerChunk > 32)) throw new ArgumentException("Bits per chunk must be between 1 and 32.");

            if (packCount <= 1) throw new ArgumentException("Number of pixels packed must be more than 1.");

            ChunkedBitstream cbs = new ChunkedBitstream(bitsPerChunk);
            uint mask = ((1U << colorBits) - 1);

            for (int i = 0; i < totalPixels; i += packCount)
            {
                uint chunk = 0U;
                for(int spix = 0; spix < packCount; spix++)
                {
                    chunk |= ((frame[i + spix] & mask) << (colorBits * spix));
                }
                cbs.Add(chunk);
            }
            return cbs;
        }

        /// <summary>
        /// Unpack a chunked bitstreaam into a linear pixel stream of bytes
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="colorBits"></param>
        /// <param name="packCount"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] UnpackLinear(ChunkedBitstream frame, int colorBits, int packCount)
        {
            if (packCount != frame.BitsPerChunk / colorBits) throw new ArgumentException("Packing incorrect.");

            int totalPixels = frame.ChunkCount * packCount;
            int bitsPerChunk = colorBits * packCount;

            if ((bitsPerChunk < 1) || (bitsPerChunk > 32)) throw new ArgumentException("Bits per chunk must be between 1 and 32.");

            byte[] outArr = new byte[totalPixels];

            uint mask = ((1U << colorBits) - 1);

            for (int i = 0; i < totalPixels; i += packCount)
            {
                uint chunk = frame[i / packCount];
                for (int spix = 0; spix < packCount; spix++)
                {
                    outArr[i + spix] = (byte)((chunk >> (colorBits * spix)) & mask);
                }
            }

            return outArr;
        }

        /// <summary>
        /// Given two 2D byte arrays, output the difference between them (xor)
        /// </summary>
        /// <param name="current"></param>
        /// <param name="previous"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Given two byte arrays, output the difference between them (xor)
        /// </summary>
        /// <param name="current"></param>
        /// <param name="previous"></param>
        /// <returns></returns>
        public static byte[] DeltaEncodeLinear(byte[] current, byte[] previous)
        {
            byte[] outty = new byte[current.Length];
            for (int i = 0; i < current.Length; i++)
            {
                outty[i] = (byte)(current[i] ^ previous[i]);

            }
            return outty;
        }

        //#FIXME direct to chunk would be faster probably
        public static ChunkedBitstream RLEPart(Bitstream bs, int dataBits, int numberBits)
        {
            int chunkBits = dataBits + numberBits;
            Bitstream outty = new Bitstream();

            if ((bs.TotalBits % dataBits) != 0)
            {
                // padding ig
                int needed = (int)(dataBits - (bs.TotalBits % dataBits));
                outty.Add(0, needed);
            }

            int countLimit = 1 << numberBits;
            int count = 1;
            for(int i = 1; i < bs.TotalBits / dataBits; i++)
            {
                uint data = bs.GetData(i * dataBits, dataBits);
                uint lastData = bs.GetData((i - 1) * dataBits, dataBits);
                if (data == lastData)
                {
                    count++;
                    if(count > countLimit)
                    {
                        // if past ocunt limit, then add the previous piece of data
                        outty.Add(data, dataBits);
                        outty.Add(count - 2, numberBits);
                        count = 1;
                    }
                }
                else
                {
                    // if changed then add the previous piece of data
                    outty.Add(lastData, dataBits);
                    outty.Add(count, numberBits);
                    count = 1;
                }
            }

            // add the last one
            outty.Add(bs.GetData(bs.TotalBits - dataBits, dataBits), dataBits);
            outty.Add(count - 1, numberBits);

            return new ChunkedBitstream(outty, chunkBits);
        }

        //#FIXME direct from chunk would be faster
        public static Bitstream DeRLEPart(ChunkedBitstream cbs, int dataBits, int numberBits) { return DeRLEPart(cbs.ToBitstream(), dataBits, numberBits); }
        public static Bitstream DeRLEPart(Bitstream inBS, int dataBits, int numberBits)
        {
            Bitstream outBS = new Bitstream();

            for(long i = 0; i < inBS.TotalBits; i += numberBits + dataBits)
            {
                uint val = inBS.GetData(i, dataBits);
                uint cnt = inBS.GetData(i + dataBits, numberBits);
                for(int j = 0; j < cnt + 1; j++)
                {
                    outBS.Add(val, dataBits);
                }
            }

            return outBS;
        }
    }
}
