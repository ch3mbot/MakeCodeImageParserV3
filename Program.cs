// See https://aka.ms/new-console-template for more information

using MakeCodeImageParserV3;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

public static partial class Program
{
    private class MenuItem
    {
        public string Description { get; set; }
        public Action<string[]> Execute { get; set; }
    }

    // Our private list of menu items that will be displayed to the user
    private static List<MenuItem> MenuItems;

    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        PopulateMenuItems();
        MainMenu();
    }

    static void MainMenu()
    {
        ClearAndShowHeading("Main Menu");

        // Write out the menu options
        for (int i = 0; i < MenuItems.Count; i++)
        {
            Console.WriteLine($"  {i}. {MenuItems[i].Description}");
        }

        // Get the cursor position for later use 
        // (to clear the line if they enter invalid input)
        int cursorTop = Console.CursorTop + 1;
        string userInput;
        int userChoice;
        string[] execArgs;

        // Get the user input
        do
        {
            // These three lines clear the previous input so we can re-display the prompt
            Console.SetCursorPosition(0, cursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, cursorTop);

            Console.Write($"Enter a choice (0 - {MenuItems.Count - 1}): ");

            var matches = Regex.Matches(Console.ReadLine(), @"(?<=^| )""[^""]*""|[^"" ]+");
            string[] bits = matches.Select(m => m.Value.Trim()).ToArray();
            if(bits.Length == 0) bits = new string[] { "" };
            bool functioned = int.TryParse(bits[0], out userChoice);
            execArgs = bits.Skip(1).ToArray();
            if (!functioned) userChoice = -1;
        } while (userChoice < 0 || userChoice >= MenuItems.Count);

        // Execute the menu item function
        MenuItems[userChoice].Execute(execArgs);
    }

    private static void PopulateMenuItems()
    {
        MenuItems = new List<MenuItem>
        {
            new MenuItem { Description = "Exit",                        Execute = Exit },
            new MenuItem { Description = "Print Test",                  Execute = PrintTest },
            new MenuItem { Description = "Export Images",               Execute = ExportImages },
            new MenuItem { Description = "Load Images",                 Execute = LoadImages },
            new MenuItem { Description = "Test Show Image",             Execute = TestShowImage },
            new MenuItem { Description = "Test Bitstream",              Execute = TestBitstream },
            new MenuItem { Description = "Test Packing",                Execute = TestPacking },
            new MenuItem { Description = "Find Optimal Params",         Execute = FindOptimalPermutations },
            new MenuItem { Description = "Preview Image From Params",   Execute = PreviewImageFromParams },
            new MenuItem { Description = "Partial tests",               Execute = BetterPartialTest },
            new MenuItem { Description = "Bitstream Addition Tests",    Execute = BitstreamAdditionTests },
            new MenuItem { Description = "Test RLE Only",               Execute = TestRLEOnly },
            new MenuItem { Description = "Show Permutation",            Execute = ShowPermutation },
            new MenuItem { Description = "Huffman Encoding Test",       Execute = HuffmanTest },
            new MenuItem { Description = "Actually Apply Encoding",     Execute = s => ActuallyEncode(s) },
        }; 
    }
    

    private static void Exit(string[] args)
    {
        ClearAndShowHeading("Exiting...");
    }

    private static void PrintTest(string[] args)
    {
        ClearAndShowHeading("Printing args for test: ");
        args.ToList().ForEach(Console.WriteLine);

        GoToMainMenu();
    }

    private static void TestRLEOnly(string[] args)
    {
        Bitstream bs = new Bitstream(new byte[] { 0x0F, 0x33 }, 16);

        Console.WriteLine(bs.ToString());
        Console.ReadKey();

        ChunkedBitstream compressed = Helper2.RLEPart(bs, 1, 2);
        Console.Write("comp data: ");
        for(int i = 0; i < compressed.ChunkCount; i++)
        {
            Console.Write(compressed[i] + ",");
        }
        Console.WriteLine();
        ChunkedBitstream obs = Helper2.DeRLEPart(compressed, 1, 2);

        Console.WriteLine(obs.ToString());

        GoToMainMenu();
    }

    private static void TestBitstream(string[] args)
    {
        ClearAndShowHeading("Testing Bitstream Class");

        if (args.Length == 0)
        {
            BitstreamTester.TestAndVerifyBitstream();
        }
        else if (args.Length == 1)
        {
            BitstreamTester.TestAndVerifyBitstream(int.Parse(args[0]));
        }
        else
        {
            BitstreamTester.TestAndVerifyBitstream(int.Parse(args[0]), int.Parse(args[1]));
        }


        GoToMainMenu();
    }

    private static void TestPacking(string[] args)
    {
        ClearAndShowHeading("Testing Bitstream Class");
        BitstreamTester.TestPackers3(allFrameData);
        GoToMainMenu();
    }

    private static void FindOptimalPermutations(string[] args)
    {
        ClearAndShowHeading("Testing Optimal Packing");

        // pack count 1 (XX) to 16
        // numberBits 1 (XX) to 16
        // delta 0 or 1
        // pack vertical 0 or 1
        // flatten vertical 0 or 1
        (int, int) packRange = (int.Parse(args[0]), int.Parse(args[1]));
        (int, int) numberBits = (int.Parse(args[2]), int.Parse(args[3]));
        (int, int) delta = (0, 2);
        (int, int) packVert = (0, 2);
        (int, int) flattenVert = (0, 2);
        (int, int) huffman = (1, 2);

        Console.WriteLine($"RLE packing bit range inclusive: from {packRange.Item1} to {packRange.Item2 - 1}");
        Console.WriteLine($"RLE number bit range inclusive: from {numberBits.Item1} to {numberBits.Item2 - 1}");
        Helper2.FindOptimalPermutations(allFrameData, new (int, int)[] { packRange, numberBits, delta, packVert, flattenVert, huffman });

        GoToMainMenu();
    }

    //#FIXME data bits is just packCount, but in theory RLE could take more? is that not just packing again? Could try double packing
    private static void ShowPermutation(string[] args)
    {
        int packCount = int.Parse(args[0]);
        int numberBits = int.Parse(args[1]);
        int dataBits = int.Parse(args[2]);
        bool doDelta = int.Parse(args[3]) > 0;
        bool packVertical = int.Parse(args[4]) > 0;
        bool flattenVertical = int.Parse(args[5]) > 0;
        bool huffman = int.Parse(args[6]) > 0;

        ClearAndShowHeading($"Testing Permutation: packCount: {packCount}, dataBits: {dataBits}, numberBits: {numberBits}, delta: {doDelta}, packing: {(packVertical ? "vert" : "hori")}, flattening: {(flattenVertical ? "vert" : "hori")}, huffman: {huffman}");

        Helper2.DoAndUndoPermutation(allFrameData, 36, packCount, numberBits, doDelta, packVertical, flattenVertical, huffman);

        GoToMainMenu();
    }

    private static long ActuallyEncode(string[] args)
    {
        int packCount = int.Parse(args[0]);
        int numberBits = int.Parse(args[1]);
        // int dataBits = int.Parse(args[1]);
        bool doDelta = int.Parse(args[2]) > 0;
        bool packVertical = int.Parse(args[3]) > 0;
        bool flattenVertical = int.Parse(args[4]) > 0;
        bool huffman = int.Parse(args[5]) > 0;

        int width = allFrameData[0].GetLength(0) / 4;
        int height = allFrameData[0].GetLength(1) / 4;

        ClearAndShowHeading($"Encoding using permutation: packCount: {packCount}, numberBits: {numberBits}, delta: {doDelta}, packing: {(packVertical ? "vert" : "hori")}, flattening: {(flattenVertical ? "vert" : "hori")}, huffman: {huffman}");

        int frameCount = allFrameData.Length;

        ChunkedBitstream[] preHuffFrames = new ChunkedBitstream[frameCount];
        ChunkedBitstream[] lastFrames = new ChunkedBitstream[frameCount];
        for (int i = 1; i < frameCount; i++)
        {
            preHuffFrames[i] = Helper2.DoPermutation(allFrameData, i, packCount, numberBits, doDelta, packVertical, flattenVertical, out ChunkedBitstream last);
            lastFrames[i] = last;
        }

        // get freq
        Dictionary<uint, int> freq = HuffmanEncoder.BuildFrequencyTable(preHuffFrames.Skip(1).ToArray());

        // gen table and codebook
        HuffmanNode root = HuffmanEncoder.BuildHuffmanTree(freq);
        Dictionary<uint, int> codeLengths = HuffmanEncoder.GetCodeLengths(root);

        // write codebook
        Bitstream codebook = HuffmanEncoder.WriteCodebook(codeLengths);


        int maxBits = 0;
        foreach (uint number in codeLengths.Keys)
        {
            int bits = number == 0 ? 1 : (int)Math.Floor(Math.Log(number, 2)) + 1;
            if (bits > maxBits)
                maxBits = bits;
        }
        Console.WriteLine("codebook max used bits: " + maxBits);

        // remove 0th fake element.
        Bitstream[] postHuffFrames = Helper2.DoHuffman(preHuffFrames.Skip(1).ToArray(), codebook);

        int bitsPerChunk = preHuffFrames[1].BitsPerChunk;
        long totalBitsTaken = postHuffFrames.Sum(s => s.TotalBits);

        Console.WriteLine("Total bits taken: " + totalBitsTaken);
        Console.WriteLine("Total kb taken: " + (Math.Ceiling((double)totalBitsTaken / 8) / 1024).ToString("F2"));

        // add back 0th fake element
        Console.WriteLine("bits per chunk: " + bitsPerChunk);
        ChunkedBitstream[] deHuffFrames = Helper2.UndoHuffman(postHuffFrames, codebook, bitsPerChunk).Prepend(null).ToArray();
        ChunkedBitstream2D[] unProcced = new ChunkedBitstream2D[frameCount];
        for (int i = 1; i < frameCount; i++)
        {
            unProcced[i] = Helper2.UndoPermutation(deHuffFrames[i], lastFrames[i], width, height, packCount, numberBits, doDelta, packVertical, flattenVertical);
        }

        for(int i = 16; i <  frameCount; i++)
        {
            FileManager.ShowGrayscaleImagePopup(unProcced[i].ToSparseByteArray());
        }

        return totalBitsTaken;
    }

    private static void PreviewImageFromParams(string[] args)
    {
        ClearAndShowHeading("Testing Optimal Packing");
        // pack count 1 (XX) to 16
        // numberBits 1 (XX) to 16
        // delta 0 or 1
        // pack vertical 0 or 1
        // flatten vertical 0 or 1

        //(int, int) packRange = (int.Parse(args[0]), int.Parse(args[1]));
        //(int, int) numberBits = (int.Parse(args[2]), int.Parse(args[3]));
        //(int, int) delta = (0, 2);
        //(int, int) packVert = (0, 2);
        //(int, int) flattenVert = (0, 2);

        byte[,] frame36 = Helper.DownsampleFrameSimple(allFrameData[36], 4);
        byte[,] frame37 = Helper.DownsampleFrameSimple(allFrameData[37], 4);

        byte[,] original = frame36;

        ChunkedBitstream[] preproc01 = Helper.PackVertical(frame36, 1, 2);
        ChunkedBitstream[] preproc02 = Helper.PackVertical(frame37, 1, 2);
        Console.WriteLine("total bits after packing: " + preproc01.Sum(cbs => cbs.TotalBits));

        byte[,] preproc11 = Helper.ChunkArrToByteBoxVertical(preproc01);
        byte[,] preproc12 = Helper.ChunkArrToByteBoxVertical(preproc02);

        Console.WriteLine("total bits after conversion to box: " + (preproc11.GetLength(0) * preproc11.GetLength(1) * 8));

        byte[] semiproc1 = Helper.FlattenFrameHorizontalStreamed(preproc01).ToByteArray();
        byte[] semiproc2 = Helper.FlattenFrameHorizontalStreamed(preproc02).ToByteArray();

        Console.WriteLine("total bits after flattening: " + (semiproc1.Length * 8));

        byte[] deltered = Helper.DeltaEncodeLinear(semiproc1, semiproc2);

        Console.WriteLine("Data after deltering bits: " + (deltered.Length * 8));

        Bitstream processed = Helper.RLEPart(new Bitstream(deltered), 2, 9).ToBitstream();

        Bitstream deRLEd = Helper.DeRLEPart(processed, 2, 9);

        Console.WriteLine("Data before RLE length: " + deltered.Length + " data after RLE but before De: " + (processed.TotalBits / 8) + " data after RLE and DeRLE: " + (deRLEd.TotalBits / 8));
        
        byte[] deDeltad = Helper.DeltaEncodeLinear(deRLEd.ToByteArray(), semiproc1);
        Console.WriteLine("dedeltad length: " + deDeltad.Length);

        Bitstream[] somewhatundone1 = Helper.UnflattenFrameHorizontalStreamed(new Bitstream(deDeltad), 40, 30);
        Console.WriteLine("somewhatundone1 width: " + somewhatundone1[0].TotalBits + ", height: " + somewhatundone1.Length);
        var somewhatundone2 = somewhatundone1.Select(bs => new ChunkedBitstream(bs, 1)).ToArray();
        Console.WriteLine("somewhatundone2 width: " + somewhatundone2[0].TotalBits + ", height: " + somewhatundone2.Length);
        byte[,] somewhatundone3 = Helper.ChunkArrToByteBoxHorizontal(somewhatundone2, 1, 2);
        Console.WriteLine("somewhatundone3 width: " + somewhatundone3.GetLength(0) + ", height: " + somewhatundone3.GetLength(1));
        byte[,] unprocced = Helper.UnpackVertical(somewhatundone3, 1, 1);

        FileManager.ShowGrayscaleImagePopup(original);
        FileManager.ShowGrayscaleImagePopup(unprocced);
        Console.WriteLine("processed kb: " + ((double)processed.TotalBits / (8 * 1024)));

        GoToMainMenu();
    }

    public static void HuffmanTest(string[] args)
    {
        ClearAndShowHeading("Huffman test...");

        int bitsPerChunk = int.Parse(args[0]);

        List<uint> vals = args.Select(uint.Parse).ToList();

        ChunkedBitstream testStream = new ChunkedBitstream(bitsPerChunk);
        vals.ForEach(v => testStream.Add(v));

        HuffmanTester.HuffmanTest(testStream);

        GoToMainMenu();
    }

    private static void BitstreamAdditionTests(string[] args)
    {
        BitstreamTester.TestBitstreamExtendSegment();
        GoToMainMenu();
    }

    private static void BetterPartialTest(string[] args)
    {
        byte[,] frame36 = Helper.DownsampleFrameSimple(allFrameData[36], 4);
        byte[,] frame37 = Helper.DownsampleFrameSimple(allFrameData[37], 4);

        ChunkedBitstream2D f36bs = new ChunkedBitstream2D(frame36, 1);
        ChunkedBitstream2D f37bs = new ChunkedBitstream2D(frame37, 1);

        byte[,] original = f36bs.ToSparseByteArray();

        Console.WriteLine("Chunked2D conversion test");
        FileManager.ShowGrayscaleImagePopup(original);

        {
            Console.WriteLine("packing flipped test");
            ChunkedBitstream2D packedf = f36bs.Packed(8, true);
            ChunkedBitstream2D unpackedf = packedf.Unpacked(8, true);
            FileManager.ShowGrayscaleImagePopup(unpackedf.ToSparseByteArray());
        }

        {
            Console.WriteLine("packing unflipped pad trim test");
            ChunkedBitstream2D packed = f36bs.PadLineLength(8).Packed(8, false);
            ChunkedBitstream2D unpacked = packed.Unpacked(8, false).Trim(30);
            FileManager.ShowGrayscaleImagePopup(unpacked.ToSparseByteArray());
        }

        {
            Console.WriteLine("flatten test");
            ChunkedBitstream flattened = f36bs.Flatten(true);
            ChunkedBitstream2D unflattened = new ChunkedBitstream2D(flattened, 40, 30, false);
            FileManager.ShowGrayscaleImagePopup(unflattened.ToSparseByteArray());
        }

        {
            Console.WriteLine("delta test");
            ChunkedBitstream deltad = Helper2.DeltaEncodeStream(f36bs.Flatten(true), f37bs.Flatten(true));
            ChunkedBitstream undeltad = Helper2.DeltaEncodeStream(deltad, f37bs.Flatten(true));
            ChunkedBitstream2D undeltflat = new ChunkedBitstream2D(undeltad, 40, 30, true);
            FileManager.ShowGrayscaleImagePopup(undeltflat.ToSparseByteArray());
        }

        {
            Console.WriteLine("RLE test");
            ChunkedBitstream postRLE = Helper2.RLEPart(Helper2.DeltaEncodeStream(f36bs.Flatten(true), f37bs.Flatten(true)).ToBitstream(), 1, 4);
            ChunkedBitstream deRLE = Helper2.DeRLEPart(postRLE, 1, 4);
            ChunkedBitstream2D fixedRLE = new ChunkedBitstream2D(Helper2.DeltaEncodeStream(deRLE, f37bs.Flatten(true)), 40, 30, true);
            FileManager.ShowGrayscaleImagePopup(fixedRLE.ToSparseByteArray());
        }

        {
            Console.WriteLine("combo test");
            ChunkedBitstream in36packflat = f36bs.Packed(2, true).Flatten(true);
            ChunkedBitstream in37packflat = f37bs.Packed(2, true).Flatten(true);
            ChunkedBitstream postComboProc = Helper2.RLEPart(Helper2.DeltaEncodeStream(in36packflat, in37packflat).ToBitstream(), 2, 9);
            ChunkedBitstream postComboDERLE = Helper2.DeRLEPart(postComboProc, 2, 9);
            ChunkedBitstream2D almostThere = new ChunkedBitstream2D(Helper2.DeltaEncodeStream(postComboDERLE, in37packflat), 20, 30, true);
            FileManager.ShowGrayscaleImagePopup(almostThere.Unpacked(2, true).ToSparseByteArray());
        }


    }

    private static void PartialTests(string[] args)
    {
        byte[,] frame36 = Helper.DownsampleFrameSimple(allFrameData[36], 4);
        byte[,] frame37 = Helper.DownsampleFrameSimple(allFrameData[37], 4);

        byte[,] original = frame36;
        Console.WriteLine("showing original");
        FileManager.ShowGrayscaleImagePopup(original);

        Console.WriteLine("packing test");
        byte[,] packed = Helper.ChunkArrToByteBoxVertical(Helper.PackVertical(frame36, 1, 1));
        byte[,] unpacked = Helper.UnpackVertical(packed, 1, 1);
        FileManager.ShowGrayscaleImagePopup(unpacked);

        Console.WriteLine("flattening test");
        // pack vertically
        ChunkedBitstream[] vertPackedChunked = Helper.PackVertical(frame36, 1, 1);

        // convert that to byte arr
        byte[] flattened = Helper.FlattenFrameVerticalStreamed(vertPackedChunked).ToByteArray();

        // convert that to bitstream
        Bitstream flattenedAsStream = new Bitstream(flattened);

        // make sure flattened and the equivalent bitstream are equal
        for (int i = 0; i < flattened.Length; i++)
        {
            if (flattened[i] != flattenedAsStream.ToByteArray()[i])
                throw new Exception("sfgkhjdfgh1");
            else
                Console.Write(flattened[i]);
        }

        // unflatten to bitstream array
        Bitstream[] unflattenedStreams = Helper.UnflattenFrameVerticalStreamed(flattenedAsStream, 40, 30);

        // convert to chunked bitstream array
        ChunkedBitstream[] unflattenedChunked = unflattenedStreams.Select(bs => new ChunkedBitstream(bs, 1)).ToArray();

        // make sure bitstream and chunked bitstream arrays are the same
        for (int j = 0; j < unflattenedChunked.Length; j++) 
        {
            var asdf1 = unflattenedStreams[j].ToByteArray();
            var asdf2 = unflattenedChunked[j].ToBitstream().ToByteArray();
            for (int i = 0; i < asdf1.Length; i++)
            {
                if (asdf1[i] != asdf2[i])
                    throw new Exception("sfgkhjdfgh2");
            } 
        }

        // convert chunked bitstream array to box
        byte[,] unflattenedBoxed = Helper.ChunkArrToByteBoxVertical(unflattenedChunked);

        //Debug.Assert(unflattenedBoxed.GetLength(0) == unflattenedChunked.Length);
        //Debug.Assert(unflattenedBoxed.GetLength(1) == unflattenedChunked[0].TotalBits);

        Console.WriteLine();
        // make sure post boxed is the same
        for (int j = 0; j < unflattenedChunked.Length; j++)
        {
            var asdf = unflattenedChunked[j].ToBitstream();
            for (int i = 0; i < asdf.TotalBits; i++)
            {
                //Console.WriteLine(asdf.GetData(i, 1) + ", " + unflattenedBoxed[j, i]);
                if (asdf.GetData(i, 1) != unflattenedBoxed[j, i])
                    throw new Exception("sfgkhjdfgh3");
            }
        }

        // unpack
        byte[,] unflattened = Helper.UnpackVertical(unflattenedBoxed, 1, 1);
        FileManager.ShowGrayscaleImagePopup(unflattened);


        GoToMainMenu();
    }

    private static void ExportImages(string[] args)
    {
        ClearAndShowHeading("Exporting images...");
        try
        {
            string srcPathFolder = args[0].Replace("\"", "");
            string destPath = args[1].Replace("\"", ""); ;
            int width = int.Parse(args[2]);
            int height = int.Parse(args[3]);
            int colorBits = int.Parse(args[4]);

            FileManager.ExportImages(srcPathFolder, destPath, width, height, colorBits);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        GoToMainMenu();
    }

    public static byte[][,] allFrameData;
    private static void LoadImages(string[] args)
    {
        ClearAndShowHeading("Loading frame data...");
        try
        {
            string srcPathFolder = args[0].Replace("\"", "");
            allFrameData = FileManager.LoadImageData(srcPathFolder);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        GoToMainMenu();
    }

    private static void TestShowImage(string[] args)
    {
        ClearAndShowHeading("Image display test...");
        try
        {
            int imageNum = int.Parse(args[0]);

            FileManager.ShowGrayscaleImagePopup(allFrameData[imageNum]);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        GoToMainMenu();
    }

    private static void ClearAndShowHeading(string heading)
    {
        Console.Clear();
        Console.WriteLine(heading);
        Console.WriteLine(new string('-', heading?.Length ?? 0));
    }

    private static void GoToMainMenu()
    {
        Console.Write("\nPress any key to go to the main menu...");
        Console.ReadKey();
        MainMenu();
    }


}
