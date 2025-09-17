using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Bogus;
using AlgoStash;

BenchmarkRunner.Run<Program>();

[MemoryDiagnoser]
[RankColumn]
[WarmupCount(3)]
[IterationCount(10)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class Program
{
    private Person[]? OldPeople { get; set; }
    private Person[]? NewPeople { get; set; }

    // PathFinder benchmark state
    private PathFinder? PfLine { get; set; }
    private PathFinder? PfRand { get; set; }
    private int LineSize { get; set; }
    private int RandSize { get; set; }

    // StateMachine helper state (used to avoid dead-code elimination via side effects)
    private int _smCounter;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 1000;

        Randomizer.Seed = new Random(1337);
        var rnd = new Random(1337);

        var personFaker = new Faker<Person>()
            .StrictMode(true)
            .RuleFor(p => p.Id, f => f.IndexFaker + 1)
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Age, f => f.Random.Int(18, 90))
            .RuleFor(p => p.Email, (f, p) => f.Internet.Email(p.FirstName, p.LastName));

        var oldList = personFaker.Generate(count);

        var newList = oldList.Select(p => p with { }).ToList();

        int mods = count / 20;

        var modifyIdx = Enumerable.Range(0, newList.Count).OrderBy(_ => rnd.Next()).Take(mods).ToArray();
        foreach (var idx in modifyIdx)
        {
            var np = newList[idx];
            var f = new Faker();
            newList[idx] = np with
            {
                LastName = f.Name.LastName(),
                Age = Math.Clamp(np.Age + rnd.Next(-3, 4), 18, 90),
                Email = f.Internet.Email(np.FirstName, np.LastName)
            };
        }

        var removeIdx = Enumerable.Range(0, newList.Count)
            .Except(modifyIdx)
            .OrderBy(_ => rnd.Next())
            .Take(mods)
            .OrderByDescending(i => i)
            .ToArray();

        foreach (var idx in removeIdx)
            newList.RemoveAt(idx);

        var insertFaker = new Faker<Person>()
            .StrictMode(true)
            .RuleFor(p => p.Id, f => count + f.IndexFaker + 1)
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Age, f => f.Random.Int(18, 90))
            .RuleFor(p => p.Email, (f, p) => f.Internet.Email(p.FirstName, p.LastName));

        for (int i = 0; i < mods; i++)
        {
            int pos = rnd.Next(0, newList.Count + 1);
            newList.Insert(pos, insertFaker.Generate());
        }

        OldPeople = oldList.ToArray();
        NewPeople = newList.ToArray();

        // ---------------------------
        // PathFinder datasets
        // ---------------------------
        var rndPf = new Random(424242);

        // Line graph: 1 -> 2 -> ... -> LineSize
        LineSize = 2000;
        var pfLine = new PathFinder();
        for (int i = 1; i < LineSize; i++)
        {
            pfLine.AddTransition(i, i + 1, () => true);
        }

        PfLine = pfLine;

        // Random shortcuts graph: base line + forward shortcuts
        RandSize = 2000;
        var pfRand = new PathFinder();
        for (int i = 1; i < RandSize; i++)
        {
            pfRand.AddTransition(i, i + 1, () => true);
        }

        // Track existing edges to avoid duplicates (which would throw on Add)
        var randEdges = new HashSet<(int from, int to)>();
        for (int i = 1; i < RandSize; i++)
            randEdges.Add((i, i + 1));

        // Add sparse forward shortcuts to create alternative shorter paths
        int edgesPerNode = 3;
        for (int i = 1; i <= RandSize; i++)
        {
            for (int e = 0; e < edgesPerNode; e++)
            {
                int jump = rndPf.Next(2, 15);
                int to = Math.Min(RandSize, i + jump);
                if (to > i && randEdges.Add((i, to)))
                {
                    pfRand.AddTransition(i, to, () => true);
                }
            }
        }

        PfRand = pfRand;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Diffs")]
    public object Lcs_Diff_Persons() => Diffs.CreateLCS(OldPeople, NewPeople, EqualityComparer<Person>.Default);

    [Benchmark]
    [BenchmarkCategory("Diffs")]
    public object Mayer_Diff_Persons() => Diffs.CreateMayer(OldPeople, NewPeople, EqualityComparer<Person>.Default);

    // ---------------------------
    // PathFinder benchmarks
    // ---------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PathFinder", "Line")]
    public int PathFinder_Line_GetStates() => PfLine!.GetStatesToReach(1, LineSize).Count;

    [Benchmark]
    [BenchmarkCategory("PathFinder", "Line")]
    public int PathFinder_Line_GetTransitions() => PfLine!.GetTransitions(1, LineSize).Count;

    [Benchmark]
    [BenchmarkCategory("PathFinder", "Line", "Execute")]
    public int PathFinder_Line_ExecuteActions()
    {
        int executed = 0;
        var transitions = PfLine!.GetTransitions(1, LineSize);
        foreach (var t in transitions)
        {
            if (t.Item3()) executed++;
        }

        return executed;
    }

    [Benchmark]
    [BenchmarkCategory("PathFinder", "Line", "Create")]
    public int PathFinder_Line_Create()
    {
        var pf = new PathFinder();
        for (int i = 1; i < LineSize; i++)
        {
            pf.AddTransition(i, i + 1, () => true);
        }

        // small usage to avoid dead code
        return pf.GetStatesToReach(1, LineSize).Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PathFinder", "Random")]
    public int PathFinder_Rand_GetStates() => PfRand!.GetStatesToReach(1, RandSize).Count;

    [Benchmark]
    [BenchmarkCategory("PathFinder", "Random")]
    public int PathFinder_Rand_GetTransitions() => PfRand!.GetTransitions(1, RandSize).Count;
}

public record Person
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; init; }
    public int Age { get; init; }
    public required string Email { get; init; }
}