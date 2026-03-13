namespace MidiFighter64
{
    public struct GridButton
    {
        public int row;         // 1-8, 1 = top
        public int col;         // 1-8, 1 = left
        public int linearIndex; // 0-63
        public int noteNumber;

        public bool IsValid => MidiFighter64InputMap.IsInRange(noteNumber);

        public override string ToString()
            => $"Grid[R{row},C{col}] note={noteNumber}";
    }

    /// <summary>
    /// Converts raw MIDI note numbers from the Midi Fighter 64 to logical grid
    /// coordinates. The MF64 uses four 4x4 quadrants, all bottom-to-top:
    ///
    ///   Top-left  (rows 1-4, cols 1-4): notes 52-67  (note 64 = row 1 col 1)
    ///   Top-right (rows 1-4, cols 5-8): notes 84-99  (note 96 = row 1 col 5)
    ///   Bot-left  (rows 5-8, cols 1-4): notes 36-51  (note 36 = row 8 col 1)
    ///   Bot-right (rows 5-8, cols 5-8): notes 68-83  (note 68 = row 8 col 5)
    ///
    /// Physical layout (row 1 = top):
    ///   Row 1: [64][65][66][67] [96][97][98][99]
    ///   Row 2: [60][61][62][63] [92][93][94][95]
    ///   Row 3: [56][57][58][59] [88][89][90][91]
    ///   Row 4: [52][53][54][55] [84][85][86][87]
    ///   Row 5: [48][49][50][51] [80][81][82][83]
    ///   Row 6: [44][45][46][47] [76][77][78][79]
    ///   Row 7: [40][41][42][43] [72][73][74][75]
    ///   Row 8: [36][37][38][39] [68][69][70][71]
    /// </summary>
    public static class MidiFighter64InputMap
    {
        public const int NOTE_OFFSET = 36;
        public const int GRID_SIZE   = 8;
        public const int NOTE_MAX    = 99;

        public static GridButton FromNote(int noteNumber)
        {
            int row, col;

            if (noteNumber >= 52 && noteNumber <= 67)
            {
                // Top-left quadrant: rows 1-4, cols 1-4, bottom-to-top
                int idx = noteNumber - 52;
                row = 4 - idx / 4;
                col = idx % 4 + 1;
            }
            else if (noteNumber >= 84 && noteNumber <= 99)
            {
                // Top-right quadrant: rows 1-4, cols 5-8, bottom-to-top
                int idx = noteNumber - 84;
                row = 4 - idx / 4;
                col = idx % 4 + 5;
            }
            else if (noteNumber >= 36 && noteNumber <= 51)
            {
                // Bottom-left quadrant: rows 5-8, cols 1-4, bottom-to-top
                int idx = noteNumber - 36;
                row = 8 - idx / 4;
                col = idx % 4 + 1;
            }
            else
            {
                // Bottom-right quadrant: rows 5-8, cols 5-8, bottom-to-top (notes 68-83)
                int idx = noteNumber - 68;
                row = 8 - idx / 4;
                col = idx % 4 + 5;
            }

            return new GridButton
            {
                row         = row,
                col         = col,
                linearIndex = (row - 1) * GRID_SIZE + (col - 1),
                noteNumber  = noteNumber
            };
        }

        public static bool IsInRange(int noteNumber)
            => noteNumber >= NOTE_OFFSET && noteNumber <= NOTE_MAX;
    }
}
