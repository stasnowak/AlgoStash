using System.Collections.Generic;

namespace DiffCore;

public sealed class Diff
{
    public static IReadOnlyList<Edit<T>> Compute<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        var edits = new List<Edit<T>>();
        int n = a.Count;
        int m = b.Count;
        int start = 0;
        while (start < n && start < m && comparer.Equals(a[start], b[start])) start++;
        int endA = n - 1;
        int endB = m - 1;
        while (endA >= start && endB >= start && comparer.Equals(a[endA], b[endB])) { endA--; endB--; }
        if (start > 0)
            edits.Add(new Edit<T>(EditKind.Match, 0, 0, start));
        var core = MyersLinear<T>.Diff(a, start, endA, b, start, endB, comparer);
        edits.AddRange(core);
        int tailLen = (n - 1 - endA);
        if (tailLen > 0)
            edits.Add(new Edit<T>(EditKind.Match, endA + 1, endB + 1, tailLen));
        return Coalesce(a, b, edits);
    }

    private static IReadOnlyList<Edit<T>> Coalesce<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, List<Edit<T>> edits)
    {
        if (edits.Count == 0) return edits;
        var res = new List<Edit<T>>(edits.Count);
        Edit<T>? prev = null;
        foreach (var e in edits)
        {
            if (e.Length == 0) continue;
            if (prev is null)
            {
                prev = e;
                continue;
            }
            var p = prev.Value;
            if (p.Kind == e.Kind)
            {
                if (p.Kind == EditKind.Match && p.AIndex + p.Length == e.AIndex && p.BIndex + p.Length == e.BIndex)
                {
                    prev = new Edit<T>(p.Kind, p.AIndex, p.BIndex, p.Length + e.Length);
                    continue;
                }
                if (p.Kind == EditKind.Delete && p.AIndex + p.Length == e.AIndex)
                {
                    prev = new Edit<T>(EditKind.Delete, p.AIndex, p.BIndex, p.Length + e.Length);
                    continue;
                }
                if (p.Kind == EditKind.Insert && p.BIndex + p.Length == e.BIndex)
                {
                    if (p.Items is { } it1 && e.Items is { } it2)
                    {
                        var list = new List<T>(it1.Count + it2.Count);
                        list.AddRange(it1);
                        list.AddRange(it2);
                        prev = new Edit<T>(EditKind.Insert, p.AIndex, p.BIndex, p.Length + e.Length, list);
                    }
                    else
                    {
                        prev = new Edit<T>(EditKind.Insert, p.AIndex, p.BIndex, p.Length + e.Length, null);
                    }
                    continue;
                }
            }
            res.Add(p);
            prev = e;
        }
        if (prev is { } last) res.Add(last);
        return res;
    }

    private static class MyersLinear<T>
    {
        public static IReadOnlyList<Edit<T>> Diff(IReadOnlyList<T> a, int aStart, int aEnd,
            IReadOnlyList<T> b, int bStart, int bEnd, IEqualityComparer<T> cmp)
        {
            var edits = new List<Edit<T>>();
            Recurse(a, aStart, aEnd, b, bStart, bEnd, cmp, edits);
            return edits;
        }

        private static void Recurse(IReadOnlyList<T> a, int aStart, int aEnd,
            IReadOnlyList<T> b, int bStart, int bEnd, IEqualityComparer<T> cmp, List<Edit<T>> outEdits)
        {
            int n = aEnd - aStart + 1;
            int m = bEnd - bStart + 1;
            if (n <= 0 && m <= 0) return;
            if (n <= 0)
            {
                if (m > 0)
                    outEdits.Add(new Edit<T>(EditKind.Insert, aStart, bStart, m, Slice(b, bStart, m)));
                return;
            }
            if (m <= 0)
            {
                outEdits.Add(new Edit<T>(EditKind.Delete, aStart, bStart, n));
                return;
            }

            int i = aStart, j = bStart;
            while (i <= aEnd && j <= bEnd && cmp.Equals(a[i], b[j])) { i++; j++; }
            if (i > aStart)
                outEdits.Add(new Edit<T>(EditKind.Match, aStart, bStart, i - aStart));
            if (i > aEnd && j > bEnd) return;

            int ae = aEnd, be = bEnd;
            while (ae >= i && be >= j && cmp.Equals(a[ae], b[be])) { ae--; be--; }
            if (ae < i && be < j)
            {
                return;
            }

            var mid = FindMiddleSnake(a, i, ae, b, j, be, cmp);
            if (mid.length == 0 && mid.xStart == i && mid.yStart == j)
            {
                int remA = ae - i + 1;
                int remB = be - j + 1;
                if (remA >= remB)
                {
                    outEdits.Add(new Edit<T>(EditKind.Delete, i, j, 1));
                    Recurse(a, i + 1, ae, b, j, be, cmp, outEdits);
                }
                else
                {
                    outEdits.Add(new Edit<T>(EditKind.Insert, i, j, 1, Slice(b, j, 1)));
                    Recurse(a, i, ae, b, j + 1, be, cmp, outEdits);
                }
                return;
            }

            Recurse(a, i, mid.xStart - 1, b, j, mid.yStart - 1, cmp, outEdits);
            if (mid.length > 0)
                outEdits.Add(new Edit<T>(EditKind.Match, mid.xStart, mid.yStart, mid.length));
            Recurse(a, mid.xStart + mid.length, ae, b, mid.yStart + mid.length, be, cmp, outEdits);
        }

        private struct MiddleSnake
        {
            public int xStart;
            public int yStart;
            public int length;
        }

        private static MiddleSnake FindMiddleSnake(IReadOnlyList<T> a, int aStart, int aEnd,
            IReadOnlyList<T> b, int bStart, int bEnd, IEqualityComparer<T> cmp)
        {
            int n = aEnd - aStart + 1;
            int m = bEnd - bStart + 1;
            int maxD = (n + m + 1) / 2 + 1;
            int delta = n - m;
            bool odd = (delta & 1) != 0;
            int size = 2 * maxD + 1;
            int off = maxD;
            var vf = new int[size];
            var vb = new int[size];
            for (int idx = 0; idx < size; idx++) { vf[idx] = -1; vb[idx] = -1; }
            vf[off + 1] = 0;
            vb[off + 1] = 0;

            for (int d = 0; d <= maxD; d++)
            {
                for (int k = -d; k <= d; k += 2)
                {
                    int kIndex = off + k;
                    int leftF = (kIndex - 1 >= 0) ? vf[kIndex - 1] : -1;
                                        int rightF = (kIndex + 1 < size) ? vf[kIndex + 1] : -1;
                                        int x = (k == -d || (k != d && leftF < rightF)) ? rightF : leftF + 1;
                    int y = x - k;
                    while (x < n && y < m && cmp.Equals(a[aStart + x], b[bStart + y])) { x++; y++; }
                    vf[kIndex] = x;
                    if (odd && (k - delta) >= -(d - 1) && (k - delta) <= (d - 1))
                    {
                        int kBackIndex = off + (k - delta);
                        int xBack = vb[kBackIndex];
                        if (xBack != -1 && x + xBack >= n)
                        {
                            int xStart = x;
                            int yStart = x - k;
                            int len = 0;
                            while (xStart - 1 - len >= 0 && yStart - 1 - len >= 0 && cmp.Equals(a[aStart + xStart - 1 - len], b[bStart + yStart - 1 - len])) len++;
                            return new MiddleSnake { xStart = aStart + xStart - len, yStart = bStart + yStart - len, length = len };
                        }
                    }
                }

                for (int k = -d; k <= d; k += 2)
                {
                    int k2 = k + delta;
                    int kIndex = off + k2;
                    int leftB = (kIndex - 1 >= 0) ? vb[kIndex - 1] : -1;
                                        int rightB = (kIndex + 1 < size) ? vb[kIndex + 1] : -1;
                                        int x = (k2 == -d || (k2 != d && leftB < rightB)) ? rightB : leftB + 1;
                    int y = x - k2;
                    while (x < n && y < m && y >= 0 && cmp.Equals(a[aEnd - x], b[bEnd - y])) { x++; y++; }
                    vb[kIndex] = x;
                    if (!odd && k2 >= -d && k2 <= d)
                    {
                        int xF = vf[off + k2];
                        if (xF != -1 && x + xF >= n)
                        {
                            int xStart = xF;
                            int yStart = xStart - k2;
                            int len = 0;
                            while (xStart + len < n && yStart + len < m && cmp.Equals(a[aStart + xStart + len], b[bStart + yStart + len])) len++;
                            return new MiddleSnake { xStart = aStart + xStart, yStart = bStart + yStart, length = len };
                        }
                    }
                }
            }
            return new MiddleSnake { xStart = aStart, yStart = bStart, length = 0 };
        }

        private static IReadOnlyList<T> Slice(IReadOnlyList<T> list, int start, int len)
        {
            var tmp = new List<T>(len);
            for (int i = 0; i < len; i++) tmp.Add(list[start + i]);
            return tmp;
        }
    }
}
