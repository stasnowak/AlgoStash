using System.Text.Json;
using DiffCore;

int[] a = [0, 1, 2, 3, 4, 5];
int[] b = [0, 2, 3, 4, 5, 6];

var output = Diff.Compute(a, b);

foreach (var e in output)
{
    Console.WriteLine(JsonSerializer.Serialize(e));
}

foreach (var e in ApplyEdits(b, output))
{
    Console.WriteLine(e);
}

IReadOnlyList<T> ApplyEdits<T>(IReadOnlyList<T> a, IReadOnlyList<Edit<T>> edits)
{
    var result = new List<T>();
    int ai = 0;
    foreach (var e in edits)
    {
        if (e.Kind == EditKind.Match)
        {
            for (int k = 0; k < e.Length; k++) result.Add(a[e.AIndex + k]);
            ai = e.AIndex + e.Length;
        }
        else if (e.Kind == EditKind.Delete)
        {
            ai = e.AIndex + e.Length;
        }
        else
        {
            if (e.Items is { } items) result.AddRange(items);
            else throw new InvalidOperationException("Insert Items must be provided or derivable.");
        }
    }

    return result;
}