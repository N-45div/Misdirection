// Runs the game's real scoring code (Assets/.../Spike/SpikeModel.cs) outside Unity on a spike CSV,
// so tests can check it agrees with tools/analyze_tracking_spike.py.
// Build: mcs -out:harness.exe SpikeModelHarness.cs ../../../Assets/Misdirection/Scripts/Runtime/Spike/SpikeModel.cs
// Run:   mono harness.exe file.csv [window_s] [dropout_s] [max_fail_rate]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Misdirection.Spike;

public static class SpikeModelHarness
{
    public static int Main(string[] args)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        double window = args.Length > 1 ? double.Parse(args[1], inv) : 1.5;
        double dropout = args.Length > 2 ? double.Parse(args[2], inv) : 0.15;
        float maxFail = args.Length > 3 ? float.Parse(args[3], inv) : 0.1f;

        string[] header = null;
        Dictionary<int, List<string[]>> byCond = new Dictionary<int, List<string[]>>();
        List<int> order = new List<int>();
        foreach (string line in File.ReadLines(args[0]))
        {
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;
            string[] parts = line.Split(new[] { ',' });
            if (header == null) { header = parts; continue; }
            if (parts[Col(header, "phase")] != "record") continue;
            if (parts[Col(header, "hand")] != parts[Col(header, "secret_hand")]) continue;
            int cond = int.Parse(parts[Col(header, "cond")], inv);
            List<string[]> rows;
            if (!byCond.TryGetValue(cond, out rows)) { rows = new List<string[]>(); byCond[cond] = rows; order.Add(cond); }
            rows.Add(parts);
        }

        Dictionary<string, SpikeCell> cells = new Dictionary<string, SpikeCell>();
        List<float> angles = new List<float>();
        foreach (int cond in order)
        {
            List<string[]> rows = byCond[cond];
            rows.Sort((a, b) => double.Parse(a[Col(header, "t")], inv).CompareTo(double.Parse(b[Col(header, "t")], inv)));
            string[] first = rows[0];
            HandSide hand = first[Col(header, "secret_hand")] == "L" ? HandSide.Left : HandSide.Right;
            string turnText = first[Col(header, "turn")];
            TurnKind turn = turnText == "away" ? TurnKind.Away : turnText == "toward" ? TurnKind.Toward : TurnKind.Center;
            float angle = (float)Math.Round(Math.Abs(double.Parse(first[Col(header, "target_deg")], inv)));
            if (!angles.Contains(angle)) angles.Add(angle);

            MoveWindowScorer scorer = new MoveWindowScorer(window, dropout);
            foreach (string[] r in rows)
            {
                scorer.Add(double.Parse(r[Col(header, "t")], inv), r[Col(header, "tracked")] == "1", r[Col(header, "high_conf")] == "1");
            }
            scorer.Finish();

            string key = SpikeScoring.Key(hand, turn, angle);
            SpikeCell cell;
            if (!cells.TryGetValue(key, out cell)) { cell = new SpikeCell(); cells[key] = cell; }
            cell.Add(scorer);
        }

        List<string> keys = new List<string>(cells.Keys);
        keys.Sort(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            SpikeCell c = cells[key];
            Console.WriteLine("CELL " + key + " " + c.Windows + " " + c.FailedWindows + " " + c.Frames + " " + c.HighFrames);
        }
        Console.WriteLine("ARC away " + SpikeScoring.UsableAngle(cells, angles, TurnKind.Away, maxFail).ToString(inv));
        Console.WriteLine("ARC toward " + SpikeScoring.UsableAngle(cells, angles, TurnKind.Toward, maxFail).ToString(inv));
        return 0;
    }

    private static int Col(string[] header, string name)
    {
        int i = Array.IndexOf(header, name);
        if (i < 0) throw new InvalidDataException("missing column " + name);
        return i;
    }
}
