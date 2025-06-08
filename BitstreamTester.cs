
namespace MakeCodeImageParserV3
{
    internal class BitstreamTester
    {
        public static void TestPackers(byte[][,] frames)
        {
            byte[][] test1A =
            {
                new byte[] { 1, 2 },
                new byte[] { 3, 4, 5},
            };
            byte[][] test1B =
            {
                new byte[] { 1, 2 },
                new byte[] { 3, 4, 5},
            };
            Console.WriteLine("Data matched for test 1: " + AreEqual(test1A, test1B));
            Console.ReadKey();

            byte[] singleFrameA = Helper.FlattenFrameHorizontal(Helper.Pack8To1Horizontal(frames[37]));
            ChunkedBitstream singleFrameBBS = Helper.PackLinear(Helper.FlattenFrameHorizontal(frames[37]), 1, 8);
            byte[] singleFrameB = singleFrameBBS.GetCompressedData();

            Console.WriteLine("Size of frame A: " + singleFrameA.Length + ", Size of frame B: " + singleFrameB.Length);
            Console.WriteLine("Data matched for single frame: " + singleFrameA.SequenceEqual(singleFrameB));
            Console.ReadKey();

            // FileManager.ShowGrayscaleImagePopup(Helper.UnflattenFrameHorizontal(Helper.UnpackLinear(singleFrameBBS, 1, 8), 160, 120));
            //Console.ReadKey();
            // FileManager.ShowGrayscaleImagePopup(Helper.Unpack8To1Horizontal(Helper.UnflattenFrameHorizontal(singleFrameA, 160 / 8, 120)));
            //Console.ReadKey();

            // First one is pack 8 together, then flatten in a line, both horizontal. Second is flatten horizontal, then pack linearly.
            byte[][] frameDataA = Helper.ApplyToAllFrames<byte[,], byte[]>(frames, f => Helper.FlattenFrameHorizontal(Helper.Pack8To1Horizontal(f)));
            byte[][] frameDataB = Helper.ApplyToAllFrames<byte[,], byte[]>(frames, f => Helper.PackLinear(Helper.FlattenFrameHorizontal(f), 1, 8).GetCompressedData());

            Console.WriteLine("Data matched: " + AreEqual(frameDataA, frameDataB));
        }

        public static void TestPackers2(byte[][,] frames)
        {
            byte[,] frame = frames[37];

            byte[,] processed = Helper.ChunkArrToByteBoxHorizontal(Helper.PackHorizontal(frame, 1, 8), 1, 8);
            //byte[,] unprocessed = Helper.Unpack8To1Horizontal(processed);
            byte[,] unprocessed = Helper.UnpackHorizontal(processed, 1, 8);
            FileManager.ShowGrayscaleImagePopup(unprocessed);
            byte[,] processed2 = Helper.ChunkArrToByteBoxVertical(Helper.PackVertical(frame, 1, 8));
            byte[,] unprocessed2 = Helper.UnpackVertical(processed2, 1, 8);
            FileManager.ShowGrayscaleImagePopup(unprocessed2);
            Console.ReadKey();
        }

        public static void TestPackers3(byte[][,] frames)
        {
            int colorBits = 1;
            int packCount = 8;
            int numberBits = 8;
            int dataBits = colorBits * packCount;

            // #FIXME temp downsampling code for estimation
            int factor = 4;
            byte[][,] framesDownsampled = Helper.ApplyToAllFrames<byte[,], byte[,]>(frames, frame => Helper.DownsampleFrameSimple(frame, factor));

            // Func<byte[,], Bitstream> compresser = (frame) => Helper.RLEPart(new Bitstream(Helper.FlattenFrameVertical(Helper.PackHorizontalToBox(frame, colorBits, packCount))), dataBits, numberBits).ToBitstream();
            Func<byte[,], Bitstream> newRLESquis = (frame) => 
            Helper.RLEPart(
                new Bitstream(
                    Helper.FlattenFrameVertical(
                        Helper.PackHorizontalToBox(
                            frame, 
                            colorBits, 
                            packCount
                            )
                        )
                    ), 
                dataBits, 
                numberBits)
            .ToBitstream();

            // Outputs 100.00% like it should
            Func<byte[,], Bitstream> justTestPacking = (frame) => (new Bitstream(Helper.FlattenFrameHorizontal(Helper.PackHorizontalToBox(frame, colorBits, packCount))));

            // Code for the old squishing
            Func<byte[,], Bitstream> RLESquis = (frame) => new Bitstream(OldHelper.RLESquis(frame));

            Helper.EstimateSavings(framesDownsampled, colorBits, justTestPacking, "packing test");
            Helper.EstimateSavings(framesDownsampled, colorBits, RLESquis, "old RLEsquis");
            Helper.EstimateSavings(framesDownsampled, colorBits, newRLESquis, "new RLEsquis");
        }
        
        public static void TestBitstreamExtendSegment()
        {
            Bitstream bsA = new Bitstream();
            Bitstream bsB = new Bitstream();
            bsA.Add(0xFF, 4);
            bsB.Append(bsA);
            Console.WriteLine("bsA: " + bsA.GetData(0, (int)bsA.TotalBits));
            Console.WriteLine("bsB: " + bsB.GetData(0, (int)bsA.TotalBits));
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
            for(int arg = 0; arg < args.Length; arg++)
            {
                (int minInclusive, int maxExclusive) = argRanges[arg];
                List<int> valsForArg = new List<int>();
                for(int val = minInclusive; val < maxExclusive; val++)
                {
                    valsForArg.Add(val);
                }
                args[arg] = valsForArg.ToArray();
            }

            Func<byte[][,], int[], Bitstream[]> allCompFuncs = (frames, args) =>
            {
                int packCount = args[0];
                int numberBits = args[1];
                bool doDelta = args[2] > 0 ? true : false;
                bool packVertical = args[3] > 0 ? true : false;
                bool flattenVertical = args[4] > 0 ? true : false;

                int dataBits = colorBits * packCount;

                // pack
                byte[][,] packedFrames;
                if(packCount > 1)
                    if(packVertical)
                        packedFrames = Helper.ApplyToAllFrames(frames, frame => Helper.PackVerticalToBox(frame, colorBits, packCount));
                    else
                        packedFrames = Helper.ApplyToAllFrames(frames, frame => Helper.PackHorizontalToBox(frame, colorBits, packCount));
                else
                    packedFrames = frames;

                // flatten
                byte[][] flattenedFrames;
                if (flattenVertical)
                    flattenedFrames = Helper.ApplyToAllFrames(packedFrames, frame => Helper.FlattenFrameVertical(frame));
                else
                    flattenedFrames = Helper.ApplyToAllFrames(packedFrames, frame => Helper.FlattenFrameHorizontal(frame));

                // delta encode
                byte[][] deltaEncodedFrames;
                if (doDelta)
                {
                    deltaEncodedFrames = new byte[frames.Length - 1][];
                    for (int i = 1; i < flattenedFrames.Length; i++)
                    {
                        deltaEncodedFrames[i - 1] = Helper.DeltaEncodeLinear(flattenedFrames[i], flattenedFrames[i - 1]);
                    }
                }
                else
                    deltaEncodedFrames = flattenedFrames;

                // if number bits > 1, then do RLE.
                if(numberBits > 1)
                {
                    return Helper.ApplyToAllFrames(deltaEncodedFrames, frame => Helper.RLEPart(new Bitstream(frame), dataBits, numberBits).ToBitstream());
                }

                // Just return the frame otherwise
                return Helper.ApplyToAllFrames(deltaEncodedFrames, frame => new Bitstream(frame));
            };

            Helper.GetBestSavingPermutation(Helper.ApplyToAllFrames(frames, frame => Helper.DownsampleFrameSimple(frame, factor)), colorBits, args, allCompFuncs);
        }

        public static bool AreEqual(byte[][] array1, byte[][] array2)
        {
            if (array1 == null || array2 == null)
                return array1 == array2;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                var inner1 = array1[i];
                var inner2 = array2[i];

                if (inner1 == null || inner2 == null)
                {
                    if (inner1 != inner2)
                        return false;
                    continue;
                }

                if (inner1.Length != inner2.Length)
                    return false;

                for (int j = 0; j < inner1.Length; j++)
                {
                    if (inner1[j] != inner2[j])
                        return false;
                }
            }

            return true;
        }

        public static void SimpleQuickTest()
        {
            var bs = new Bitstream();

            // Write 40 bits (e.g., 0xABCDEF1234) starting at bit 20
            uint input = 0x12345678;
            long index = 20;
            int bitCount = 32;

            bs.SetSize(index + bitCount);
            bs.SetData(index, bitCount, input);
            uint output = bs.GetData(index, bitCount);

            Console.WriteLine($"Wrote: 0x{input:X8}, Read: 0x{output:X8}");
            Console.WriteLine(output == input ? "Passed" : "Failed");
        }

        public static void TestAndVerifyBitstream(int iterations = 100_000, int seed = -1)
        {
            const int maxBitCount = 32;

            Bitstream bs = new Bitstream();

            if (seed == -1) seed = DateTime.Now.Millisecond;
            Random rng = new Random(seed);

            List<bool> referenceBits = new List<bool>();

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int op = rng.Next(4); // 0 = Add, 1 = Get, 2 = Set, 3 = Remove

                switch (op)
                {
                    case 0: // Add
                        {
                            int bitCount = rng.Next(1, maxBitCount + 1);
                            uint value = rng.NextUInt();

                            long start = bs.TotalBits;
                            bs.Add(value, bitCount);

                            //Console.WriteLine("adding " + bitCount + " bits. BitCount is now " + bs.Count + " vs " + (referenceBits.Count + bitCount));

                            for (int j = 0; j < bitCount; j++)
                            {
                                bool bit = ((value & (1U << j)) != 0);
                                referenceBits.Add(bit);
                            }
                        }
                        break;

                    case 1: // Get
                        {
                            if (referenceBits.Count == 0) break;
                            if (bs.TotalBits == 0) break;

                            int bitCount = rng.Next(1, maxBitCount + 1);
                            bitCount = (int)Math.Min(bitCount, bs.TotalBits);
                            long index = rng.NextInt64(0, bs.TotalBits - bitCount + 1);

                            try
                            {
                                // Read value from Bitstream
                                uint bitstreamValue = bs.GetData(index, bitCount);

                                for (int j = 0; j < bitCount; j++)
                                {
                                    bool bitFromBitstream = (bitstreamValue & (1U << j)) != 0;

                                    bool bitFromReference = referenceBits[(int)(index + j)];

                                    if (bitFromBitstream != bitFromReference)
                                    {
                                        Console.WriteLine($"""
                                            [FAIL][GET]
                                            Iteration: {iteration + 1}
                                            Index:     {index}
                                            Bit Count: {bitCount}
                                            Bit Pos:   {j} (Global Bit: {index + j})
                                            Chunk:     {index / 32}, Offset: {index % 32}
                                            Bitstream: {bitFromBitstream}
                                            Reference: {bitFromReference}
                                        """);
                                        throw new Exception($"Verification failed at bit {(index + j)}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[EXCEPTION][GET] {ex.Message}");
                            }
                        }
                        break;

                    case 2: // Remove
                        {
                            if (bs.TotalBits < 1) break;

                            int bitCount = rng.Next(1, (int)Math.Min(32, bs.TotalBits) + 1);
                            try
                            {
                                bs.Remove(bitCount);
                                referenceBits.RemoveRange(referenceBits.Count - bitCount, bitCount);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[EXCEPTION][REMOVE] {ex.Message}");
                            }
                        }
                        break;

                    case 3: // Set
                        {
                            if (bs.TotalBits < 1) break;

                            int bitCount = rng.Next(1, maxBitCount + 1);
                            if (bs.TotalBits < bitCount) break;

                            long index = rng.NextInt64(0, bs.TotalBits - bitCount + 1);
                            uint value = rng.NextUInt();

                            try
                            {
                                bs.SetData(index, bitCount, value);

                                for (int j = 0; j < bitCount; j++)
                                {
                                    bool bit = ((value & (1U << j)) != 0);
                                    referenceBits[(int)(index + j)] = bit;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[EXCEPTION][SET] {ex.Message}");
                            }
                        }
                        break;
                }

                if (!VerifyEntireStream(bs, referenceBits))
                {
                    Console.WriteLine("iteration " + iteration + " has issues.");
                }

                if ((((iteration + 1) % 1000) == 0) || (iteration == (iterations - 1)))
                {
                    Console.WriteLine((iteration + 1) + "/" + iterations);
                }
            }

            Console.WriteLine("Bitstream randomized test completed.");
        }

        public static bool VerifyEntireStream(Bitstream bs, List<bool> referenceBits)
        {
            long bitCount = bs.TotalBits;

            if (bitCount != referenceBits.Count)
            {
                Console.WriteLine($"[VERIFY][FAIL] Bit count mismatch: Bitstream={bitCount}, Reference={referenceBits.Count}");
                throw new Exception("Bit count mismatch");
            }

            for (long i = 0; i < bitCount; i++)
            {
                try
                {
                    uint bit = bs.GetData(i, 1);
                    bool bitstreamBit = (bit != 0);
                    bool referenceBit = referenceBits[(int)i];

                    if (bitstreamBit != referenceBit)
                    {
                        Console.WriteLine($"""
                            [VERIFY][FAIL]
                            Index:     {i}
                            Chunk:     {i / 32}
                            Offset:    {i % 32}
                            Bitstream: {bitstreamBit}
                            Reference: {referenceBit}
                        """);
                        throw new Exception($"Mismatch at bit {i}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VERIFY][EXCEPTION] at index {i}: {ex.Message}");
                    return false;
                }
            }
            return true;
        }
    }

    static class RandomExtensions
    {
        public static uint NextUInt(this Random rng)
        {
            var bytes = new byte[4];
            rng.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static long NextInt64(this Random rng, long minValue, long maxValue)
        {
            if (minValue >= maxValue)
                return minValue;

            ulong range = (ulong)(maxValue - minValue);
            ulong rand = ((ulong)rng.Next() << 32) | (uint)rng.Next();
            return (long)(rand % range) + minValue;
        }
    }
}
