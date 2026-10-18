# Builds DM02 Hell's Highways' animated sky: multiplayer-pk3/graphics/HHSKY1.png to HHSKY16.png,
# which multiplayer-pk3/animdefs/hells-highways.yaml plays as HHSKY (game-info `sky: HHSKY`).
# Run it after changing anything below:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-hells-highways-sky.ps1
#
# Each frame is 512x128 (the sky goes round the view twice) and wraps left to right: near-black
# red at the top to a burning orange at the horizon. Smoke bands drift sideways, the horizon glow
# breathes, flames lick along the bottom edge and embers rise and fade. Everything moves by whole
# turns of the loop's phase, so the last frame runs back into the first: the smoke drifts a whole
# band a loop, so more frames (at the animdefs' tics) is slower, not choppier. The engine draws
# each in the game's palette, nearest color.

param(
    [int]$Frames = 16,
    [int]$EmberRise = 3     # pixels an ember climbs a frame
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;

public static class HellSky
{
    const int W = 512, H = 128;
    static readonly double[] Top = { 18, 2, 2 }, Mid = { 110, 14, 6 }, Low = { 230, 96, 24 };

    public static Bitmap Frame(int f, int n, int rise)
    {
        var bmp = new Bitmap(W, H);
        double p = 2 * Math.PI * f / n;
        double glow = 0.07 * Math.Sin(p);          // the horizon breathing
        var c = new double[3];
        for (int y = 0; y < H; y++)
        {
            double t = y / (double)(H - 1);
            for (int x = 0; x < W; x++)
            {
                double a = 2 * Math.PI * x / W;
                // smoke: bands in whole turns round the sky, drifting sideways with the phase
                double smoke = Math.Sin(a * 3 + y * .09 + p) * .5 + Math.Sin(a * 7 - y * .05 + 1.3 + 2 * p) * .3
                    + Math.Sin(a * 13 + y * .21 - p) * .2;
                double dark = Math.Max(0, smoke) * 0.45 * (1 - Math.Abs(t - 0.55));
                for (int i = 0; i < 3; i++)
                {
                    if (t < 0.6) { double u = t / 0.6; c[i] = Top[i] + (Mid[i] - Top[i]) * u; }
                    else { double u = (t - 0.6) / 0.4; c[i] = (Mid[i] + (Low[i] - Mid[i]) * u * u) * (1 + glow * u); }
                    c[i] *= 1 - dark;
                }
                // flames along the horizon: their height waves along the edge as the loop turns
                double flame = Math.Max(2, 8 + 4 * Math.Sin(a * 11 + p) + 3 * Math.Sin(a * 23 - 2 * p) + 2 * Math.Sin(a * 37 + 3 * p));
                int up = H - 1 - y;
                if (up < flame)
                {
                    double k = 1 - up / flame;
                    c[0] += (255 - c[0]) * k * .8; c[1] += (170 - c[1]) * k * .7; c[2] += (40 - c[2]) * k * .5;
                }
                bmp.SetPixel(x, y, Color.FromArgb(Clamp(c[0]), Clamp(c[1]), Clamp(c[2])));
            }
        }

        // embers: each rises `rise` pixels a frame from its own spot, for the whole loop, dimming
        // at the end of its climb (the same 220 every frame, each a frame along from the last,
        // starting at their own frames so they don't climb together)
        uint s = 666;
        Func<double> next = () => (s = s * 1664525 + 1013904223) / 4294967296.0;
        for (int e = 0; e < 220; e++)
        {
            int ex = (int)(next() * W), ey0 = (int)(H - 1 - Math.Pow(next(), 1.8) * (H - 8)), ph = (int)(next() * n);
            bool bright = next() < 0.5;
            int k = (f + ph) % n, ey = ey0 - k * rise;
            if (ey < 4)
                continue;
            var col = k >= n - n / 4 ? Color.FromArgb(200, 90, 20) : bright ? Color.FromArgb(255, 220, 90) : Color.FromArgb(255, 140, 30);
            bmp.SetPixel(ex, ey, col);
        }
        return bmp;
    }

    static int Clamp(double v) { return (int)Math.Max(0, Math.Min(255, v)); }
}
'@

$dir = Join-Path $PSScriptRoot '..\..\multiplayer-pk3\graphics'
for ($f = 0; $f -lt $Frames; $f++) {
    $bmp = [HellSky]::Frame($f, $Frames, $EmberRise)
    $path = Join-Path (Resolve-Path $dir).Path "HHSKY$($f + 1).png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "Wrote $path"
}
