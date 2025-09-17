using AlgoStash;
using FluentAssertions;
using Xunit;

namespace AlgoStash.Tests.Unit;

public class PathFinderTests
{
    [Fact]
    public void GetStatesToReach_SimpleLine_ShouldReturnOrderedStateIds()
    {
        var pf = new PathFinder();
        pf.AddTransition(1, 2, () => true);
        pf.AddTransition(2, 3, () => true);

        var path = pf.GetStatesToReach(1, 3);

        path.Select(s => s.Id).Should().Equal(new[] { 1, 2, 3 });
    }

    [Fact]
    public void GetTransitions_SimpleLine_ShouldReturnTransitionsWithActions()
    {
        bool aCalled = false;
        bool bCalled = false;

        var pf = new PathFinder();
        pf.AddTransition(1, 2, () => { aCalled = true; return true; });
        pf.AddTransition(2, 3, () => { bCalled = true; return true; });

        var transitions = pf.GetTransitions(1, 3);

        transitions.Should().HaveCount(2);
        transitions[0].Item1.Id.Should().Be(1);
        transitions[0].Item2.Id.Should().Be(2);
        transitions[1].Item1.Id.Should().Be(2);
        transitions[1].Item2.Id.Should().Be(3);

        // Invoke actions to ensure they are wired correctly
        transitions[0].Item3.Invoke().Should().BeTrue();
        transitions[1].Item3.Invoke().Should().BeTrue();
        aCalled.Should().BeTrue();
        bCalled.Should().BeTrue();
    }

    [Fact]
    public void AddTransition_ShouldAutoAddMissingStates()
    {
        var pf = new PathFinder();
        // No AddStates call beforehand
        pf.AddTransition(10, 11, () => true);

        var path = pf.GetStatesToReach(10, 11);
        path.Select(s => s.Id).Should().Equal(new[] { 10, 11 });
    }

    [Fact]
    public void GetStatesToReach_WhenNoPath_ShouldReturnEmpty()
    {
        var pf = new PathFinder();
        pf.AddTransition(1, 2, () => true);
        pf.AddTransition(3, 4, () => true);

        var path = pf.GetStatesToReach(1, 4);

        path.Should().BeEmpty();
    }

    [Fact]
    public void GetStatesToReach_ShouldFindShortestPath()
    {
        var pf = new PathFinder();
        // Longer route: 1 -> 2 -> 3 -> 4
        pf.AddTransition(1, 2, () => true);
        pf.AddTransition(2, 3, () => true);
        pf.AddTransition(3, 4, () => true);

        // Shorter route: 1 -> 5 -> 4
        pf.AddTransition(1, 5, () => true);
        pf.AddTransition(5, 4, () => true);

        var path = pf.GetStatesToReach(1, 4);
        path.Select(s => s.Id).Should().Equal(new[] { 1, 5, 4 });
    }
}
