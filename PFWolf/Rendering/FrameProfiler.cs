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
        }
        Array.Clear(_frame);
        _frameStart = _mark = now;
        _allocStart = _allocMark = alloc;
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
