using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;

namespace PFWolf.Rendering;

/// <summary>
/// Where a frame's time goes, for the `perf` command: each section's average and worst time,
/// the slowest frames, and how much was allocated and collected. Off unless asked for, when
/// each mark is a single bool test.
/// </summary>
internal static class FrameProfiler
{
    private static bool _enabled;
    private static AllocationSampler? _sampler;

    internal static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (value)
                _sampler ??= new AllocationSampler();
            else
            {
                _sampler?.Dispose();
                _sampler = null;
            }
        }
    }

    internal enum Section { Input, Actors, World, Lights, Flats, Walls, Sprites, Weapon, ViewCopy, Overlays, Present, Other }

    private static readonly int Count = Enum.GetValues<Section>().Length;
    private static readonly long[] _total = new long[Count], _max = new long[Count], _frame = new long[Count], _alloc = new long[Count];
    private static long _mark, _allocMark, _frameStart, _frameTotal, _frameMax, _allocStart, _allocTotal;
    private static int _frames, _slowFrames, _gcStart0, _gcStart1, _gcStart2;
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    /// <summary>The tics the frame being timed runs (set once they're worked out)</summary>
    internal static int Tics;

    // How many frames ran 0, 1, 2 and 3+ tics; the slow frames (over SlowMs), latest last
    private static readonly int[] _ticCounts = new int[4];
    private static readonly Queue<string> _slow = new();
    private static long _enabledAt;
    private static int _gcFrame0, _gcFrame1, _gcFrame2;
    private static TimeSpan _gcPause;
    internal static double SlowMs = 25;

    internal static void Reset()
    {
        Array.Clear(_total);
        Array.Clear(_max);
        Array.Clear(_alloc);
        _frameTotal = _frameMax = _allocTotal = 0;
        _frames = _slowFrames = 0;
        _gcStart0 = GC.CollectionCount(0);
        _gcStart1 = GC.CollectionCount(1);
        _gcStart2 = GC.CollectionCount(2);
        _frameStart = 0;
        _sampler?.Sampled.Clear();
        Array.Clear(_ticCounts);
        _slow.Clear();
        _enabledAt = Stopwatch.GetTimestamp();
    }

    /// <summary>A new frame starts here: the last one, if any, is added up</summary>
    internal static void BeginFrame()
    {
        if (!Enabled)
            return;
        long now = Stopwatch.GetTimestamp();
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        if (_frameStart != 0)
        {
            _frame[(int)Section.Other] += now - _mark;
            _alloc[(int)Section.Other] += alloc - _allocMark;
            long length = now - _frameStart;
            _frameTotal += length;
            _frameMax = Math.Max(_frameMax, length);
            if (length * TicksToMs > 1000.0 / 70)
                _slowFrames++;
            for (int i = 0; i < Count; i++)
            {
                _total[i] += _frame[i];
                _max[i] = Math.Max(_max[i], _frame[i]);
            }
            _allocTotal += alloc - _allocStart;
            _frames++;
            _ticCounts[Math.Clamp(Tics, 0, 3)]++;
            bool collected = GC.CollectionCount(0) != _gcFrame0;
            if (length * TicksToMs > SlowMs || collected)
                NoteSlowFrame(now, length);
        }
        _gcFrame0 = GC.CollectionCount(0);
        _gcFrame1 = GC.CollectionCount(1);
        _gcFrame2 = GC.CollectionCount(2);
        _gcPause = GC.GetTotalPauseDuration();
        Array.Clear(_frame);
        _frameStart = _mark = now;
        _allocStart = _allocMark = alloc;
    }

    // A frame that took long enough to be seen: when, how long, its tics, any collection during
    // it, and the parts that took a millisecond or more. Printed at once (stdout) and kept for the report.
    private static void NoteSlowFrame(long now, long length)
    {
        var text = new StringBuilder($"perf {(length * TicksToMs > SlowMs ? "slow" : "gc  ")}:{(now - _enabledAt) * TicksToMs / 1000,7:F2}s {length * TicksToMs,6:F1} ms, {Tics} tics");
        int gc0 = GC.CollectionCount(0) - _gcFrame0, gc1 = GC.CollectionCount(1) - _gcFrame1, gc2 = GC.CollectionCount(2) - _gcFrame2;
        if (gc0 + gc1 + gc2 > 0)
            text.Append($", GC {gc0}/{gc1}/{gc2} paused {(GC.GetTotalPauseDuration() - _gcPause).TotalMilliseconds:F1}");
        for (int i = 0; i < Count; i++)
            if (_frame[i] * TicksToMs >= 1)
                text.Append($", {(Section)i} {_frame[i] * TicksToMs:F1}");
        var line = text.ToString();
        Console.WriteLine(line);
        _slow.Enqueue(line);
        if (_slow.Count > 12)
            _slow.Dequeue();
    }

    /// <summary>The time since the last mark was spent on <paramref name="section"/></summary>
    internal static void Mark(Section section)
    {
        if (!Enabled || _frameStart == 0)
            return;
        long now = Stopwatch.GetTimestamp();
        _frame[(int)section] += now - _mark;
        _mark = now;
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        _alloc[(int)section] += alloc - _allocMark;
        _allocMark = alloc;
    }

    internal static string Report()
    {
        if (_frames == 0)
            return "perf: no frames timed yet";
        var text = new StringBuilder();
        text.AppendLine($"perf: {_frames} frames, avg {_frameTotal * TicksToMs / _frames:F2} ms, worst {_frameMax * TicksToMs:F2} ms, {_slowFrames} over 14.3 ms");
        for (int i = 0; i < Count; i++)
            text.AppendLine($"  {(Section)i,-9} avg {_total[i] * TicksToMs / _frames,6:F3} ms  worst {_max[i] * TicksToMs,7:F2} ms  alloc {_alloc[i] / _frames,7} bytes/frame");
        text.Append($"  alloc {_allocTotal / _frames} bytes/frame, GCs gen0 {GC.CollectionCount(0) - _gcStart0} gen1 {GC.CollectionCount(1) - _gcStart1} gen2 {GC.CollectionCount(2) - _gcStart2}");
        text.Append($"\n  tics per frame: 0 x{_ticCounts[0]}, 1 x{_ticCounts[1]}, 2 x{_ticCounts[2]}, 3+ x{_ticCounts[3]}");
        foreach (var line in _slow)
            text.Append($"\n  {line}");
        if (_sampler is { } sampler)
            foreach (var (type, bytes) in sampler.Sampled.OrderByDescending(s => s.Value).Take(8))
                text.Append($"\n  allocated: {type} ~{bytes / 1024} KB");
        return text.ToString();
    }
}

/// <summary>
/// The runtime's allocation samples (one per ~100 KB allocated, with the type that tipped it
/// over), added up by type: what the frames allocate most of
/// </summary>
internal sealed class AllocationSampler : EventListener
{
    internal readonly ConcurrentDictionary<string, long> Sampled = new();

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);    // GC keyword: GCAllocationTick
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick", StringComparison.Ordinal) != true || e.PayloadNames == null)
            return;
        int typeAt = e.PayloadNames.IndexOf("TypeName"), amountAt = e.PayloadNames.IndexOf("AllocationAmount64");
        if (typeAt < 0 || e.Payload?[typeAt] is not string type)
            return;
        long amount = amountAt >= 0 && e.Payload[amountAt] is ulong bytes ? (long)bytes : 100_000;
        Sampled.AddOrUpdate(type, amount, (_, total) => total + amount);
    }
}
