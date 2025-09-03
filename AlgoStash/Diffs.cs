using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace AlgoStash;

public static class Diffs
{
    private const int StackAllocThreshold = 4096;
    private const int HuntSzymanskiMatchLimit = 2_000_000;

    public static Diff<T> CreateLCS<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer) where T : notnull
    {
        ComputeTrim(oldSequence, newSequence, equalityComparer, out var prefixLen, out var suffixLen);

        var output = new List<DiffEntry<T>>(oldSequence.Length + newSequence.Length);

        for (var i = 0; i < prefixLen; i++)
            output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = oldSequence[i] });

        var a = oldSequence.AsSpan(prefixLen, oldSequence.Length - prefixLen - suffixLen);
        var b = newSequence.AsSpan(prefixLen, newSequence.Length - prefixLen - suffixLen);

        if (a.Length == 0)
        {
            foreach (var t in b)
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = t });
        }
        else if (b.Length == 0)
        {
            foreach (var t in a)
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = t });
        }
        else
        {
            if (TryHuntSzymanskiDiff(a, b, equalityComparer, output))
            {
            }
            else
            {
                DiffHirschberg(a, b, equalityComparer, output);
            }
        }

        var aTailStart = oldSequence.Length - suffixLen;
        for (var i = aTailStart; i < oldSequence.Length; i++)
            output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = oldSequence[i] });

        return new Diff<T> { Entries = output };
    }

    public static Diff<T> CreateMayer<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer)
    {
        var diffEntries = new List<DiffEntry<T>>(oldSequence.Length + newSequence.Length);

        var i = 0;
        var j = 0;

        while (i < oldSequence.Length && j < newSequence.Length)
        {
            if (equalityComparer.Equals(oldSequence[i], newSequence[j]))
            {
                diffEntries.Add(new DiffEntry<T> { Value = oldSequence[i], Type = DiffType.Stable });
                i++;
                j++;
            }
            else
            {
                var ii = i;
                var jj = j;
                var removed = false;

                while (ii < oldSequence.Length && jj < newSequence.Length && !equalityComparer.Equals(oldSequence[ii], newSequence[jj]))
                {
                    ii++;
                }

                for (var k = i; k < ii; k++)
                {
                    diffEntries.Add(new DiffEntry<T> { Value = oldSequence[k], Type = DiffType.Remove });
                    i++;
                    removed = true;
                }

                if (removed)
                    continue;

                while (ii < oldSequence.Length && jj < newSequence.Length && !equalityComparer.Equals(oldSequence[ii], newSequence[jj]))
                {
                    jj++;
                }

                for (var k = j; k < jj; k++)
                {
                    diffEntries.Add(new DiffEntry<T> { Value = newSequence[k], Type = DiffType.Insert });
                    j++;
                }
            }
        }

        for (var k = i; k < oldSequence.Length; k++)
        {
            diffEntries.Add(new DiffEntry<T> { Value = oldSequence[k], Type = DiffType.Remove });
        }

        for (var k = j; k < newSequence.Length; k++)
        {
            diffEntries.Add(new DiffEntry<T> { Value = newSequence[k], Type = DiffType.Insert });
        }

        return new Diff<T> { Entries = diffEntries.ToArray() };
    }

    private static bool TryHuntSzymanskiDiff<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, IEqualityComparer<T> cmp, List<DiffEntry<T>> output) where T : notnull
    {
        var map = new Dictionary<T, List<int>>(b.Length, cmp);
        for (var j = 0; j < b.Length; j++)
        {
            var val = b[j];
            if (!map.TryGetValue(val, out var list))
                map[val] = list = new List<int>(1);
            list.Add(j);
        }

        long totalMatches = 0;
        foreach (var t in a)
            if (map.TryGetValue(t, out var lst)) totalMatches += lst.Count;

        switch (totalMatches)
        {
            case 0:
            {
                foreach (var t in a)
                    output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = t });

                foreach (var t in b)
                    output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = t });

                return true;
            }
            case > HuntSzymanskiMatchLimit:
                return false;
        }

        var r = (int)totalMatches;

        var js = ArrayPool<int>.Shared.Rent(r);
        var aiIdx = ArrayPool<int>.Shared.Rent(r);
        var tCount = 0;

        for (var i = 0; i < a.Length; i++)
        {
            if (!map.TryGetValue(a[i], out var lst)) continue;
            var arr = CollectionsMarshal.AsSpan(lst);
            for (var p = arr.Length - 1; p >= 0; p--)
            {
                if (tCount == js.Length) break;
                js[tCount] = arr[p];
                aiIdx[tCount] = i;
                tCount++;
            }
        }

        if (tCount == 0)
        {
            foreach (var t in a)
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = t });

            foreach (var t in b)
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = t });

            ArrayPool<int>.Shared.Return(js, true);
            ArrayPool<int>.Shared.Return(aiIdx, true);
            return true;
        }

        var maxL = Math.Min(a.Length, b.Length);
        var tailsVal = ArrayPool<int>.Shared.Rent(maxL);
        var tailsPtr = ArrayPool<int>.Shared.Rent(maxL);
        var prev = ArrayPool<int>.Shared.Rent(tCount);

        var l = 0;
        for (var t = 0; t < tCount; t++)
        {
            var jVal = js[t];

            int lo = 0, hi = l;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (tailsVal[mid] < jVal) lo = mid + 1; else hi = mid;
            }
            var pos = lo;

            tailsVal[pos] = jVal;
            tailsPtr[pos] = t;
            prev[t] = (pos > 0) ? tailsPtr[pos - 1] : -1;

            if (pos == l) l++;
        }

        var lcsT = ArrayPool<int>.Shared.Rent(l);
        var k = l - 1;
        var cur = tailsPtr[l - 1];
        while (cur >= 0)
        {
            lcsT[k--] = cur;
            cur = prev[cur];
        }

        int ia = 0, jb = 0;
        for (var idx = 0; idx < l; idx++)
        {
            var t = lcsT[idx];
            var pa = aiIdx[t];
            var pb = js[t];

            for (var i = ia; i < pa; i++)
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });

            for (var j = jb; j < pb; j++)
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });

            output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[pa] });

            ia = pa + 1;
            jb = pb + 1;
        }

        for (var i = ia; i < a.Length; i++)
            output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
        for (var j = jb; j < b.Length; j++)
            output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });

        ArrayPool<int>.Shared.Return(js, true);
        ArrayPool<int>.Shared.Return(aiIdx, true);
        ArrayPool<int>.Shared.Return(tailsVal, true);
        ArrayPool<int>.Shared.Return(tailsPtr, true);
        ArrayPool<int>.Shared.Return(prev, true);

        return true;
    }

    private static void DiffHirschberg<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, IEqualityComparer<T> cmp, List<DiffEntry<T>> output)
    {
        while (!a.IsEmpty && !b.IsEmpty && cmp.Equals(a[0], b[0]))
        {
            output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[0] });
            a = a[1..];
            b = b[1..];
        }

        if (a.IsEmpty)
        {
            for (var j = 0; j < b.Length; j++)
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            return;
        }

        if (b.IsEmpty)
        {
            for (var i = 0; i < a.Length; i++)
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
            return;
        }

        if (a.Length == 1)
        {
            var k = IndexOf(b, a[0], cmp);
            if (k >= 0)
            {
                for (var j = 0; j < k; j++) output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
                output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[0] });
                for (var j = k + 1; j < b.Length; j++) output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            }
            else
            {
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[0] });
                for (var j = 0; j < b.Length; j++) output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            }
            return;
        }

        if (b.Length == 1)
        {
            var k = IndexOf(a, b[0], cmp);
            if (k >= 0)
            {
                for (var i = 0; i < k; i++) output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
                output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[k] });
                for (var i = k + 1; i < a.Length; i++) output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
            }
            else
            {
                for (var i = 0; i < a.Length; i++) output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[0] });
            }
            return;
        }

        var mid = a.Length / 2;
        var n = b.Length;

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> leftRow = stackalloc int[n + 1];
            Span<int> rightRow = stackalloc int[n + 1];
            leftRow.Clear();
            rightRow.Clear();

            LcsLengthsForward(a[..mid], b, cmp, leftRow);
            LcsLengthsBackward(a[mid..], b, cmp, rightRow);

            int split = 0, max = -1;
            for (var j = 0; j <= n; j++)
            {
                var val = leftRow[j] + rightRow[j];
                if (val > max) { max = val; split = j; }
            }

            DiffHirschberg(a[..mid], b[..split], cmp, output);
            DiffHirschberg(a[mid..], b[split..], cmp, output);
        }
        else
        {
            var pool = ArrayPool<int>.Shared;
            var leftArr = pool.Rent(n + 1);
            var rightArr = pool.Rent(n + 1);
            try
            {
                Array.Clear(leftArr, 0, n + 1);
                Array.Clear(rightArr, 0, n + 1);

                LcsLengthsForward(a[..mid], b, cmp, leftArr.AsSpan(0, n + 1));
                LcsLengthsBackward(a[mid..], b, cmp, rightArr.AsSpan(0, n + 1));

                int split = 0, max = -1;
                var left = leftArr.AsSpan(0, n + 1);
                var right = rightArr.AsSpan(0, n + 1);
                for (var j = 0; j <= n; j++)
                {
                    var val = left[j] + right[j];
                    if (val > max) { max = val; split = j; }
                }

                DiffHirschberg(a[..mid], b[..split], cmp, output);
                DiffHirschberg(a[mid..], b[split..], cmp, output);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(leftArr, clearArray: true);
                ArrayPool<int>.Shared.Return(rightArr, clearArray: true);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOf<T>(ReadOnlySpan<T> span, T value, IEqualityComparer<T> cmp)
    {
        for (var i = 0; i < span.Length; i++)
            if (cmp.Equals(span[i], value)) return i;
        return -1;
    }

    private static void LcsLengthsForward<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, IEqualityComparer<T> cmp, Span<int> result)
    {
        var n = b.Length;
        if (result.Length < n + 1) throw new ArgumentException("Result span too small.", nameof(result));

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> prev = stackalloc int[n + 1];
            Span<int> cur = stackalloc int[n + 1];
            prev.Clear(); cur.Clear();

            for (var i = 0; i < a.Length; i++)
            {
                cur[0] = 0;
                var ai = a[i];
                for (var j = 1; j <= n; j++)
                    cur[j] = cmp.Equals(ai, b[j - 1]) ? prev[j - 1] + 1 : (prev[j] >= cur[j - 1] ? prev[j] : cur[j - 1]);
                var tmp = prev;
                prev = cur;
                cur = tmp;
            }

            prev.CopyTo(result);
        }
        else
        {
            var pool = ArrayPool<int>.Shared;
            var prevArr = pool.Rent(n + 1);
            var curArr = pool.Rent(n + 1);
            try
            {
                Array.Clear(prevArr, 0, n + 1);

                for (var i = 0; i < a.Length; i++)
                {
                    curArr[0] = 0;
                    var ai = a[i];
                    for (var j = 1; j <= n; j++)
                    {
                        var t = cmp.Equals(ai, b[j - 1]) ? prevArr[j - 1] + 1 : (prevArr[j] >= curArr[j - 1] ? prevArr[j] : curArr[j - 1]);
                        curArr[j] = t;
                    }
                    (prevArr, curArr) = (curArr, prevArr);
                }

                new ReadOnlySpan<int>(prevArr, 0, n + 1).CopyTo(result);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(prevArr, clearArray: true);
                ArrayPool<int>.Shared.Return(curArr, clearArray: true);
            }
        }
    }

    private static void LcsLengthsBackward<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, IEqualityComparer<T> cmp, Span<int> result)
    {
        var n = b.Length;
        if (result.Length < n + 1) throw new ArgumentException("Result span too small.", nameof(result));

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> prev = stackalloc int[n + 1];
            Span<int> cur = stackalloc int[n + 1];
            prev.Clear(); cur.Clear();

            for (var i = a.Length - 1; i >= 0; i--)
            {
                var ai = a[i];
                cur[n] = 0;
                for (var j = n - 1; j >= 0; j--)
                    cur[j] = cmp.Equals(ai, b[j]) ? prev[j + 1] + 1 : (prev[j] >= cur[j + 1] ? prev[j] : cur[j + 1]);
                var tmp = prev;
                prev = cur;
                cur = tmp;
            }

            prev.CopyTo(result);
        }
        else
        {
            var pool = ArrayPool<int>.Shared;
            var prevArr = pool.Rent(n + 1);
            var curArr = pool.Rent(n + 1);
            try
            {
                Array.Clear(prevArr, 0, n + 1);

                for (var i = a.Length - 1; i >= 0; i--)
                {
                    var ai = a[i];
                    curArr[n] = 0;
                    for (var j = n - 1; j >= 0; j--)
                    {
                        var t = cmp.Equals(ai, b[j]) ? prevArr[j + 1] + 1 : (prevArr[j] >= curArr[j + 1] ? prevArr[j] : curArr[j + 1]);
                        curArr[j] = t;
                    }
                    (prevArr, curArr) = (curArr, prevArr);
                }

                new ReadOnlySpan<int>(prevArr, 0, n + 1).CopyTo(result);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(prevArr, clearArray: true);
                ArrayPool<int>.Shared.Return(curArr, clearArray: true);
            }
        }
    }

    private static void ComputeTrim<T>(
        T[] oldSeq, T[] newSeq, IEqualityComparer<T> cmp,
        out int prefixLen, out int suffixLen)
    {
        var n = oldSeq.Length;
        var m = newSeq.Length;

        var i = 0;
        while (i < n && i < m && cmp.Equals(oldSeq[i], newSeq[i])) i++;

        var j = 0;
        while (j + i < n && j + i < m && cmp.Equals(oldSeq[n - 1 - j], newSeq[m - 1 - j])) j++;

        prefixLen = i;
        suffixLen = j;
    }
}

public class Diff<T>
{
    public required IList<DiffEntry<T>> Entries { get; set; }
}

public class DiffEntry<T>
{
    public DiffType Type { get; set; }
    public required T Value { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiffType
{
    Insert,
    Stable,
    Remove,
}
