using FluentAssertions;
using Xunit;

namespace AlgoStash.Tests.Unit;

public class DiffsMayerTests
{
    [Fact]
    public void Mayer_WhenSequencesAreEqual_ShouldReturnAllStable()
    {
        var a = new[] {1, 2, 3};
        var b = new[] {1, 2, 3};

        var diff = Diffs.CreateMayer(a, b, EqualityComparer<int>.Default);

        diff.Entries.Select(e => e.Type).Should().Equal(new[] {DiffType.Stable, DiffType.Stable, DiffType.Stable});
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }

    [Fact]
    public void Mayer_WhenInsertionsOnly_ShouldMarkAllAsInsert()
    {
        var a = Array.Empty<int>();
        var b = new[] {1, 2, 3};

        var diff = Diffs.CreateMayer(a, b, EqualityComparer<int>.Default);

        diff.Entries.Should().AllSatisfy(e => e.Type.Should().Be(DiffType.Insert));
        diff.Entries.Select(e => e.Value).Should().Equal(b);
    }

    [Fact]
    public void Mayer_WhenRemovalsOnly_ShouldMarkAllAsRemove()
    {
        var a = new[] {1, 2, 3};
        var b = Array.Empty<int>();

        var diff = Diffs.CreateMayer(a, b, EqualityComparer<int>.Default);

        diff.Entries.Should().AllSatisfy(e => e.Type.Should().Be(DiffType.Remove));
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }

    [Fact]
    public void Mayer_Mixed_SampleFromProgram_ShouldBeHeuristicButValid()
    {
        var a = new[] {0, 1, 2, 3, 4, 5};
        var b = new[] {0, 2, 3, 4, 5, 6};

        var diff = Diffs.CreateMayer(a, b, EqualityComparer<int>.Default);

        Apply(a, diff.Entries).Should().Equal(b);
    }

    [Fact]
    public void Mayer_WithCustomEqualityComparer_ShouldUseIt()
    {
        var a = new[] {10, 21, 32};
        var b = new[] {0, 1, 2};

        var diff = Diffs.CreateMayer(a, b, new Mod10Comparer());

        diff.Entries.Select(e => e.Type).Should().Equal(new[] {DiffType.Stable, DiffType.Stable, DiffType.Stable});
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }

    private static int[] Apply(int[] oldSeq, IList<DiffEntry<int>> entries)
    {
        var list = new List<int>(oldSeq);
        int index = 0;
        foreach (var e in entries)
        {
            switch (e.Type)
            {
                case DiffType.Stable:
                    index++;
                    break;
                case DiffType.Remove:
                    list.RemoveAt(index);
                    break;
                case DiffType.Insert:
                    list.Insert(index, e.Value);
                    index++;
                    break;
            }
        }
        return list.ToArray();
    }

    private sealed class Mod10Comparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % 10 == y % 10;
        public int GetHashCode(int obj) => obj % 10;
    }
}
