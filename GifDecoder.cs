using System.Collections.Generic;
using System;
using UnityEngine;

public static class GifDecoder
{
    public struct Frame { public Texture2D tex; public float delaySec; }

    public static bool TryDecode(byte[] data, out List<Frame> frames)
    {
        frames = new List<Frame>();
        if (data == null || data.Length < 13) return false;
        if (!(data[0] == 'G' && data[1] == 'I' && data[2] == 'F')) return false;

        int w = data[6] | (data[7] << 8);
        int h = data[8] | (data[9] << 8);
        if (w <= 0 || h <= 0) return false;

        bool hasGct = (data[10] & 0x80) != 0;
        int gctSize = hasGct ? 1 << (data[10] & 7) : 0;
        Color[] gct = ReadPalette(data, 13, gctSize);

        var canvasIdx = new int[w * h];
        for (int i = 0; i < canvasIdx.Length; i++) canvasIdx[i] = -1;

        float delaySec = 0.1f;
        bool hasTrans = false; byte transIdx = 0; int curDisposal = 1;
        int prevLeft = -1, prevTop = 0, prevW = 0, prevH = 0, prevDisposal = 1;

        int pos = 13 + gctSize * 3;
        while (pos < data.Length)
        {
            byte b = data[pos];
            if (b == 0x21 && pos + 8 <= data.Length && data[pos + 1] == 0xF9)
            {
                byte packed = data[pos + 3];
                hasTrans = (packed & 1) != 0;
                curDisposal = (packed >> 2) & 7;
                delaySec = Mathf.Max(0.03f, (data[pos + 4] | (data[pos + 5] << 8)) / 100f);
                transIdx = data[pos + 6];
                pos += 8;
            }
            else if (b == 0x2C && pos + 10 < data.Length)
            {
                int left = data[pos + 1] | (data[pos + 2] << 8);
                int top = data[pos + 3] | (data[pos + 4] << 8);
                int fw = data[pos + 5] | (data[pos + 6] << 8);
                int fh = data[pos + 7] | (data[pos + 8] << 8);
                byte packed2 = data[pos + 9];
                bool hasLct = (packed2 & 0x80) != 0;
                bool interlaced = (packed2 & 0x40) != 0;
                int lctSize = hasLct ? 1 << (packed2 & 7) : 0;
                pos += 10;

                Color[] pal = gct;
                if (hasLct && pos + lctSize * 3 <= data.Length) { pal = ReadPalette(data, pos, lctSize); pos += lctSize * 3; }

                byte minCode = data[pos++];
                var stream = new List<byte>();
                while (pos < data.Length)
                {
                    int len = data[pos++];
                    if (len == 0) break;
                    for (int k = 0; k < len && pos < data.Length; k++) stream.Add(data[pos++]);
                }

                byte[] pix = DecodeLzw(stream.ToArray(), minCode);
                if (pix == null || pix.Length != fw * fh) { Debug.LogWarning("GifDecoder: LZW decode failed for a frame"); return false; }

                if (prevDisposal == 2 && prevLeft >= 0) ClearRect(canvasIdx, w, h, prevLeft, prevTop, prevW, prevH);

                int tIdx = hasTrans ? transIdx : -1;
                WriteFrame(canvasIdx, w, h, left, top, fw, fh, pix, interlaced, tIdx);

                frames.Add(new Frame { tex = MakeTexture(w, h, canvasIdx, pal), delaySec = delaySec });

                prevLeft = left; prevTop = top; prevW = fw; prevH = fh; prevDisposal = curDisposal;
            }
            else if (b == 0x3B) break;
            else if (b == 0x21 && pos + 2 <= data.Length)
            {
                pos += 2;
                while (pos < data.Length) { int len = data[pos++]; for (int k = 0; k < len && pos < data.Length; k++) pos++; if (len == 0) break; }
            }
            else pos++;
        }

        return frames.Count > 0;
    }

    static Color[] ReadPalette(byte[] d, int p, int n)
    {
        var pal = new Color[n];
        for (int i = 0; i < n && p + 2 < d.Length; i++)
            pal[i] = new Color(d[p], d[p + 1], d[p + 2], 1f);
        return pal;
    }

    static void ClearRect(int[] canvas, int W, int H, int x0, int y0, int fw, int fh)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y0 + fh); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x0 + fw); x++)
                canvas[y * W + x] = -1;
    }

    static void WriteFrame(int[] canvas, int W, int H, int left, int top, int fw, int fh, byte[] pix, bool interlaced, int tIdx)
    {
        var rows = new List<int>();
        if (interlaced)
        {
            int[] starts = { 0, 4, 2, 1 };
            int[] steps = { 8, 8, 4, 2 };
            for (int pass = 0; pass < 4; pass++)
                for (int y = starts[pass]; y < fh; y += steps[pass]) rows.Add(y);
        }
        else
            for (int y = 0; y < fh; y++) rows.Add(y);

        int p = 0;
        foreach (int ry in rows)
            for (int x = 0; x < fw; x++, p++)
            {
                int v = pix[p];
                if (v == tIdx) continue;
                int X = left + x, Y = top + ry;
                if (X >= 0 && X < W && Y >= 0 && Y < H) canvas[Y * W + X] = v;
            }
    }

    static Texture2D MakeTexture(int w, int h, int[] idx, Color[] pal)
    {
        var px = new Color32[idx.Length];
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0 && idx[i] < pal.Length)
            {
                Color c = pal[idx[i]];
                px[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    static byte[] DecodeLzw(byte[] stream, int minCodeSize)
    {
        var output = new List<byte>();
        int clearCode = 1 << minCodeSize;
        int endCode = clearCode + 1;

        var dict = new List<byte[]>();
        int codeSize, nextCode;
        void ResetDict()
        {
            dict.Clear();
            for (int i = 0; i < clearCode; i++) dict.Add(new byte[] { (byte)i });
            nextCode = endCode + 1;
            codeSize = minCodeSize + 1;
        }

        int bitPos = 0, acc = 0, accBits = 0;
        int ReadCode()
        {
            while (accBits < codeSize)
            {
                if (bitPos >= stream.Length) return -1;
                acc |= stream[bitPos++] << accBits;
                accBits += 8;
            }
            int c = acc & ((1 << codeSize) - 1);
            acc >>= codeSize;
            accBits -= codeSize;
            return c;
        }

        ResetDict();
        byte[] prevOut = null;
        while (true)
        {
            int code = ReadCode();
            if (code < 0) break;
            if (code == clearCode) { ResetDict(); prevOut = null; continue; }
            if (code == endCode) break;

            byte[] s;
            if (code < dict.Count) s = dict[code];
            else if (code == nextCode && prevOut != null)
            {
                var kwk = new byte[prevOut.Length + 1];
                Array.Copy(prevOut, kwk, prevOut.Length);
                kwk[prevOut.Length] = prevOut[0];
                s = kwk;
            }
            else { Debug.LogWarning("GifDecoder: invalid LZW code " + code); return null; }

            if (prevOut != null)
            {
                var entry = new byte[prevOut.Length + 1];
                Array.Copy(prevOut, entry, prevOut.Length);
                entry[prevOut.Length] = s[0];
                dict.Add(entry);
                nextCode++;
                if (nextCode >= (1 << codeSize) && codeSize < 12) codeSize++;
            }

            output.AddRange(s);
            prevOut = s;
        }
        return output.ToArray();
    }
}
