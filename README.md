# AlgoStash

A small .NET library showcasing sequence diff algorithms.

This repository currently implements two algorithms for computing differences between sequences:
- LCS-based diff (CreateLCS)
- A simple heuristic inspired by Eugene W. Myers/Mayer-style scanning (CreateMayer)

Both algorithms operate generically on T[] with a provided IEqualityComparer<T> and return a Diff<T> composed of DiffEntry<T> elements of types Insert, Stable, Remove.

## Projects
- AlgoStash: Class library/console with algorithms and simple demo in Program.cs.
- AlgoStash.Tests.Unit: xUnit test project with unit tests for both algorithms.

## Getting started
Requires .NET 9 SDK.

Build:
- dotnet build

Run demo:
- dotnet run --project .\AlgoStash

Run tests:
- dotnet test

## API overview
- Diffs.CreateLCS<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer):
  - Uses dynamic programming LCS matrix and backtracking to produce a minimal edit script with Stable/Insert/Remove.
- Diffs.CreateMayer<T>(T[] oldSequence, T[] newSequence, IEqualityComparer<T> equalityComparer):
  - Uses a forward scan heuristic to find matches and emits removals/inserts; not guaranteed minimal but fast.

Return types:
- Diff<T> { DiffEntry<T>[] Entries }
- DiffEntry<T> { DiffType Type, T Value }
- enum DiffType { Insert, Stable, Remove }

## Example
See Program.cs for a simple demonstration:
- a = [0,1,2,3,4,5]
- b = [0,2,3,4,5,6]
It prints JSON-serialized entries for both algorithms.

## Contributing
Issues and PRs are welcome.

## Initial Bad Benchmarks

| Method             | Mean        | Error      | StdDev     | Ratio | RatioSD | Rank | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------- |------------:|-----------:|-----------:|------:|--------:|-----:|---------:|---------:|---------:|-----------:|------------:|
| Lcs_Diff_Persons   | 5,817.71 us | 167.124 us | 110.542 us | 1.000 |    0.03 |    2 | 992.1875 | 992.1875 | 992.1875 | 3990.66 KB |        1.00 |
| Mayer_Diff_Persons |    11.47 us |   0.373 us |   0.247 us | 0.002 |    0.00 |    1 |   6.7139 |   1.1139 |        - |   109.9 KB |        0.03 |
