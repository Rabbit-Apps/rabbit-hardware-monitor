namespace HardwareMonitor;

// Fixed-size histogram: no per-frame allocation or growing session history.
internal sealed class FpsStatistics
{
    readonly long[] counts = new long[10002];
    readonly double[] totals = new double[10002];
    long count;
    double total;
    public void Reset() { Array.Clear(counts); Array.Clear(totals); count = 0; total = 0; }
    public void Add(double ms)
    {
        if (!double.IsFinite(ms) || ms <= 0) return;
        int bucket = ms > 1000 ? 10001 : (int)Math.Ceiling(ms * 10);
        counts[bucket]++; totals[bucket] += ms; count++; total += ms;
    }
    public (double? Average, double? Low) Read()
    {
        if (count < 100 || total < 2000) return (null, null);
        long wanted = (long)Math.Ceiling(count * .01), remaining = wanted;
        double slowTotal = 0;
        for (int i = counts.Length - 1; i >= 0 && remaining > 0; i--)
        {
            long take = Math.Min(remaining, counts[i]);
            if (take == 0) continue;
            slowTotal += totals[i] * take / counts[i];
            remaining -= take;
        }
        return (1000 * count / total, 1000 * wanted / slowTotal);
    }
}
