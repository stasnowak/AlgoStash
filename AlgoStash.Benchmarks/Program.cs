using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Bogus;
using AlgoStash;

BenchmarkRunner.Run<Program>();

[MemoryDiagnoser]
[RankColumn]
[WarmupCount(3)]
[IterationCount(10)]
public partial class Program
{
    public Person[] OldPeople { get; set; }
    public Person[] NewPeople { get; set; }

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
    }

    [Benchmark(Baseline = true)]
    public object Lcs_Diff_Persons() => Diffs.CreateLCS(OldPeople, NewPeople, EqualityComparer<Person>.Default);

    [Benchmark]
    public object Mayer_Diff_Persons() => Diffs.CreateMayer(OldPeople, NewPeople, EqualityComparer<Person>.Default);
}

public record Person
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; init; }
    public int Age { get; init; }
    public required string Email { get; init; }
}