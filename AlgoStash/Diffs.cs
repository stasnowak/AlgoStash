using System.Buffers;
using System.Text.Json.Serialization;

namespace AlgoStash;

public static class Diffs
{
    private const int StackAllocThreshold = 2048;

    public static Diff<T> CreateLCS<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer)
    {
        var output = new List<DiffEntry<T>>(oldSequence.Length + newSequence.Length);
        DiffHirschberg(oldSequence.AsSpan(), newSequence.AsSpan(), equalityComparer, output);
        return new Diff<T> { Entries = output.ToArray() };
    }

    public static Diff<T> CreateMayer<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer)
    {
        var diffEntries = new List<DiffEntry<T>>(oldSequence.Length + newSequence.Length);

        int i = 0;
        int j = 0;

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
                int ii = i;
                int jj = j;
                bool removed = false;

                while (ii < oldSequence.Length && jj < newSequence.Length && !equalityComparer.Equals(oldSequence[ii], newSequence[jj]))
                {
                    ii++;
                }

                for (int k = i; k < ii; k++)
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

                for (int k = j; k < jj; k++)
                {
                    diffEntries.Add(new DiffEntry<T> { Value = newSequence[k], Type = DiffType.Insert });
                    j++;
                }
            }
        }

        for (int k = i; k < oldSequence.Length; k++)
        {
            diffEntries.Add(new DiffEntry<T> { Value = oldSequence[k], Type = DiffType.Remove });
        }

        for (int k = j; k < newSequence.Length; k++)
        {
            diffEntries.Add(new DiffEntry<T> { Value = newSequence[k], Type = DiffType.Insert });
        }

        return new Diff<T> { Entries = diffEntries.ToArray() };
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
            for (int j = 0; j < b.Length; j++)
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            return;
        }

        if (b.IsEmpty)
        {
            for (int i = 0; i < a.Length; i++)
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
            return;
        }

        if (a.Length == 1)
        {
            int k = IndexOf(b, a[0], cmp);
            if (k >= 0)
            {
                for (int j = 0; j < k; j++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });

                output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[0] });

                for (int j = k + 1; j < b.Length; j++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            }
            else
            {
                output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[0] });
                for (int j = 0; j < b.Length; j++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[j] });
            }
            return;
        }

        if (b.Length == 1)
        {
            int k = IndexOf(a, b[0], cmp);
            if (k >= 0)
            {
                for (int i = 0; i < k; i++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });

                output.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = a[k] });

                for (int i = k + 1; i < a.Length; i++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
            }
            else
            {
                for (int i = 0; i < a.Length; i++)
                    output.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = a[i] });
                output.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = b[0] });
            }
            return;
        }

        int mid = a.Length / 2;

        int n = b.Length;

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> leftRow = stackalloc int[n + 1];
            Span<int> rightRow = stackalloc int[n + 1];
            leftRow.Clear();
            rightRow.Clear();

            LcsLengthsForward(a[..mid], b, cmp, leftRow);
            LcsLengthsBackward(a[mid..], b, cmp, rightRow);

            int max = -1;
            int split = 0;
            for (int j = 0; j <= n; j++)
            {
                int val = leftRow[j] + rightRow[j];
                if (val > max)
                {
                    max = val;
                    split = j;
                }
            }

            DiffHirschberg(a[..mid], b[..split], cmp, output);
            DiffHirschberg(a[mid..], b[split..], cmp, output);
        }
        else
        {
            int[] leftArr = ArrayPool<int>.Shared.Rent(n + 1);
            int[] rightArr = ArrayPool<int>.Shared.Rent(n + 1);
            try
            {
                Array.Clear(leftArr, 0, n + 1);
                Array.Clear(rightArr, 0, n + 1);

                Span<int> leftRow = leftArr.AsSpan(0, n + 1);
                Span<int> rightRow = rightArr.AsSpan(0, n + 1);

                LcsLengthsForward(a[..mid], b, cmp, leftRow);
                LcsLengthsBackward(a[mid..], b, cmp, rightRow);

                int max = -1;
                int split = 0;
                for (int j = 0; j <= n; j++)
                {
                    int val = leftRow[j] + rightRow[j];
                    if (val > max)
                    {
                        max = val;
                        split = j;
                    }
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

    private static int IndexOf<T>(ReadOnlySpan<T> span, T value, IEqualityComparer<T> cmp)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (cmp.Equals(span[i], value))
                return i;
        }
        return -1;
    }

    private static void LcsLengthsForward<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, IEqualityComparer<T> cmp, Span<int> result)
    {
        int n = b.Length;
        if (result.Length < n + 1) throw new ArgumentException("Result span too small.", nameof(result));

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> prev = stackalloc int[n + 1];
            Span<int> cur = stackalloc int[n + 1];
            prev.Clear();
            cur.Clear();

            for (int i = 0; i < a.Length; i++)
            {
                cur[0] = 0;
                var ai = a[i];
                for (int j = 1; j <= n; j++)
                {
                    cur[j] = cmp.Equals(ai, b[j - 1]) ? prev[j - 1] + 1 : Math.Max(prev[j], cur[j - 1]);
                }

                var tmp = prev;
                prev = cur;
                cur = tmp;
            }

            prev.CopyTo(result);
        }
        else
        {
            int[] prevArr = ArrayPool<int>.Shared.Rent(n + 1);
            int[] curArr = ArrayPool<int>.Shared.Rent(n + 1);
            try
            {
                Array.Clear(prevArr, 0, n + 1);
                Array.Clear(curArr, 0, n + 1);

                for (int i = 0; i < a.Length; i++)
                {
                    curArr[0] = 0;
                    var ai = a[i];
                    for (int j = 1; j <= n; j++)
                    {
                        curArr[j] = cmp.Equals(ai, b[j - 1]) ? prevArr[j - 1] + 1 : Math.Max(prevArr[j], curArr[j - 1]);
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
        int n = b.Length;
        if (result.Length < n + 1) throw new ArgumentException("Result span too small.", nameof(result));

        if (n + 1 <= StackAllocThreshold)
        {
            Span<int> prev = stackalloc int[n + 1];
            Span<int> cur = stackalloc int[n + 1];
            prev.Clear();
            cur.Clear();

            for (int i = a.Length - 1; i >= 0; i--)
            {
                var ai = a[i];
                cur[n] = 0;
                for (int j = n - 1; j >= 0; j--)
                {
                    cur[j] = cmp.Equals(ai, b[j]) ? prev[j + 1] + 1 : Math.Max(prev[j], cur[j + 1]);
                }
                var tmp = prev;
                prev = cur;
                cur = tmp;
            }

            prev.CopyTo(result);
        }
        else
        {
            int[] prevArr = ArrayPool<int>.Shared.Rent(n + 1);
            int[] curArr = ArrayPool<int>.Shared.Rent(n + 1);
            try
            {
                Array.Clear(prevArr, 0, n + 1);
                Array.Clear(curArr, 0, n + 1);

                for (int i = a.Length - 1; i >= 0; i--)
                {
                    var ai = a[i];
                    curArr[n] = 0;
                    for (int j = n - 1; j >= 0; j--)
                    {
                        curArr[j] = cmp.Equals(ai, b[j]) ? prevArr[j + 1] + 1 : Math.Max(prevArr[j], curArr[j + 1]);
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
}

public class Diff<T>
{
    public required DiffEntry<T>[] Entries { get; set; }  
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
