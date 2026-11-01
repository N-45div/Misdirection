using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Misdirection.Spike
{
    /// <summary>
    /// Plain CSV writer for tracking-spike runs. Lines starting with '#' carry metadata.
    /// Column order is a contract with tools/analyze_tracking_spike.py.
    /// </summary>
    public sealed class SpikeCsvLog : IDisposable
    {
        public const string FormatTag = "misdirection_tracking_spike v1";

        public const string Header =
            "t,frame,dt,phase,cond,repeat,secret_hand,turn,target_deg,look,head_yaw,head_pitch," +
            "hand,valid,tracked,high_conf,pinch,hand_az,hand_el,hand_lx,hand_ly,hand_lz";

        private StreamWriter writer;

        public string FilePath { get; private set; }

        public SpikeCsvLog(string filePath, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            FilePath = filePath;
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            writer = new StreamWriter(filePath, false, new UTF8Encoding(false), 1 << 16);
            writer.NewLine = "\n";
            writer.WriteLine("# " + FormatTag);
            foreach (KeyValuePair<string, string> pair in metadata)
            {
                writer.WriteLine("# " + pair.Key + "=" + Sanitize(pair.Value));
            }
            writer.WriteLine(Header);
        }

        public void WriteLine(string line)
        {
            if (writer != null)
            {
                writer.WriteLine(line);
            }
        }

        public void Flush()
        {
            if (writer != null)
            {
                writer.Flush();
            }
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
                writer = null;
            }
        }

        private static string Sanitize(string value)
        {
            if (value == null)
            {
                return "";
            }
            return value.Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
