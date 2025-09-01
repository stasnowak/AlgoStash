using System.Text.Json.Serialization;

namespace AlgoStash;

public static class Diffs
{
    public static Diff<T> CreateLCS<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer)
    {
        int m = oldSequence.Length;
        int n = newSequence.Length;

        int[,] matrix = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (equalityComparer.Equals(oldSequence[i - 1], newSequence[j - 1]))
                    matrix[i, j] = matrix[i - 1, j - 1] + 1;
                else
                    matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);
            }
        }

        List<DiffEntry<T>> diffEntries = new();

        while (m > 0 && n > 0)
        {
            if (equalityComparer.Equals(oldSequence[m - 1], newSequence[n - 1]))
            {
                diffEntries.Add(new DiffEntry<T> { Type = DiffType.Stable, Value = oldSequence[m - 1] });
                m--;
                n--;
            }
            else if (matrix[m - 1, n] > matrix[m, n - 1])
            {
                diffEntries.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = oldSequence[m - 1] });
                m--;
            }
            else
            {
                diffEntries.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = newSequence[n - 1] });
                n--;
            }
        }

        while (m > 0)
        {
            diffEntries.Add(new DiffEntry<T> { Type = DiffType.Remove, Value = oldSequence[m - 1] });
            m--;
        }

        while (n > 0)
        {
            diffEntries.Add(new DiffEntry<T> { Type = DiffType.Insert, Value = newSequence[n - 1] });
            n--;
        }

        diffEntries.Reverse();

        return new Diff<T>
        {
            Entries = diffEntries.ToArray()
        };
    }

    public static Diff<T> CreateMayer<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer)
    {
        var diffEntries = new List<DiffEntry<T>>();

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
                bool removed = default;

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

        return new Diff<T>
        {
            Entries = diffEntries.ToArray()
        };
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
