using System;

namespace DeskMate.Logic
{
    /// <summary>Generates a soft two-note bell as an in-memory 16-bit PCM WAV, so the app
    /// ships a pleasant alert sound without embedding any audio file.</summary>
    public static class ChimeWave
    {
        const int SampleRate = 22050;

        /// <summary>~1.9s: a gentle fifth (A5 → E6) with exponential decay and a hint of shimmer.</summary>
        public static byte[] GenerateSoftChime()
        {
            double sec = 1.9;
            int n = (int)(SampleRate * sec);
            var samples = new short[n];
            AddNote(samples, 0, 880.0, 0.9, 0.55);       // A5, long decay
            AddNote(samples, SampleRate / 2, 1318.51, 1.3, 0.5); // E6, softer
            return BuildWav(samples);
        }

        static void AddNote(short[] buffer, int offset, double freq, double decaySec, double amp)
        {
            for (int i = 0; i < buffer.Length - offset; i++)
            {
                double t = (double)i / SampleRate;
                double env = Math.Exp(-t / decaySec);
                // fundamental + soft octave partial + slow tremolo for warmth
                double v = Math.Sin(2 * Math.PI * freq * t)
                         + 0.35 * Math.Sin(2 * Math.PI * freq * 2 * t);
                v *= env * amp * (1 - 0.12 * Math.Sin(2 * Math.PI * 5.2 * t));
                int s = (int)(v * 28000);
                int mixed = buffer[offset + i] + s;
                buffer[offset + i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, mixed));
            }
        }

        static byte[] BuildWav(short[] samples)
        {
            int dataLen = samples.Length * 2;
            var bytes = new byte[44 + dataLen];
            var riff = System.Text.Encoding.ASCII.GetBytes("RIFF");
            var wave = System.Text.Encoding.ASCII.GetBytes("WAVE");
            var fmt = System.Text.Encoding.ASCII.GetBytes("fmt ");
            var data = System.Text.Encoding.ASCII.GetBytes("data");
            Array.Copy(riff, 0, bytes, 0, 4);
            Array.Copy(BitConverter.GetBytes(36 + dataLen), 0, bytes, 4, 4);
            Array.Copy(wave, 0, bytes, 8, 4);
            Array.Copy(fmt, 0, bytes, 12, 4);
            Array.Copy(BitConverter.GetBytes(16), 0, bytes, 16, 4);      // fmt chunk size
            Array.Copy(BitConverter.GetBytes((short)1), 0, bytes, 20, 2); // PCM
            Array.Copy(BitConverter.GetBytes((short)1), 0, bytes, 22, 2); // mono
            Array.Copy(BitConverter.GetBytes(SampleRate), 0, bytes, 24, 4);
            Array.Copy(BitConverter.GetBytes(SampleRate * 2), 0, bytes, 28, 4); // byte rate
            Array.Copy(BitConverter.GetBytes((short)2), 0, bytes, 32, 2); // block align
            Array.Copy(BitConverter.GetBytes((short)16), 0, bytes, 34, 2); // bits
            Array.Copy(data, 0, bytes, 36, 4);
            Array.Copy(BitConverter.GetBytes(dataLen), 0, bytes, 40, 4);
            for (int i = 0; i < samples.Length; i++)
            {
                bytes[44 + i * 2] = (byte)(samples[i] & 0xFF);
                bytes[44 + i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
            }
            return bytes;
        }
    }
}
