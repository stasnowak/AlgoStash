using FluentAssertions;
using Xunit;

namespace AlgoStash.Tests.Unit;

public class DiffsLcsTests
{
    [Fact]
    public void Lcs_WhenSequencesAreEqual_ShouldReturnAllStable()
    {
        var a = new[] {1, 2, 3};
        var b = new[] {1, 2, 3};

        var diff = Diffs.CreateLCS(a, b, EqualityComparer<int>.Default);

        diff.Entries.Select(e => e.Type).Should().Equal(new[] {DiffType.Stable, DiffType.Stable, DiffType.Stable});
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }

    [Fact]
    public void Lcs_WhenInsertionsOnly_ShouldMarkAllAsInsert()
    {
        var a = Array.Empty<int>();
        var b = new[] {1, 2, 3};

        var diff = Diffs.CreateLCS(a, b, EqualityComparer<int>.Default);

        diff.Entries.Should().AllSatisfy(e => e.Type.Should().Be(DiffType.Insert));
        diff.Entries.Select(e => e.Value).Should().Equal(b);
    }

    [Fact]
    public void Lcs_WhenRemovalsOnly_ShouldMarkAllAsRemove()
    {
        var a = new[] {1, 2, 3};
        var b = Array.Empty<int>();

        var diff = Diffs.CreateLCS(a, b, EqualityComparer<int>.Default);

        diff.Entries.Should().AllSatisfy(e => e.Type.Should().Be(DiffType.Remove));
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }

    [Fact]
    public void Lcs_Mixed_ShouldFollowLcsMatrixBacktracking()
    {
        var a = new[] {0, 1, 2, 3, 4, 5};
        var b = new[] {0, 2, 3, 4, 5, 6};

        var diff = Diffs.CreateLCS(a, b, EqualityComparer<int>.Default);

        var expected = new (DiffType Type, int Val)[]
        {
            (DiffType.Stable, 0),
            (DiffType.Remove, 1),
            (DiffType.Stable, 2),
            (DiffType.Stable, 3),
            (DiffType.Stable, 4),
            (DiffType.Stable, 5),
            (DiffType.Insert, 6)
        };

        diff.Entries.Select(e => (e.Type, e.Value)).Should().Equal(expected);
    }

    private sealed class Mod10Comparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % 10 == y % 10;
        public int GetHashCode(int obj) => obj % 10;
    }

    [Fact]
    public void Lcs_WithCustomEqualityComparer_ShouldUseIt()
    {
        var a = new[] {10, 21, 32};
        var b = new[] {0, 1, 2};

        var diff = Diffs.CreateLCS(a, b, new Mod10Comparer());

        diff.Entries.Select(e => e.Type).Should().Equal(new[] {DiffType.Stable, DiffType.Stable, DiffType.Stable});
        diff.Entries.Select(e => e.Value).Should().Equal(a);
    }
}
