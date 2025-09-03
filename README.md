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

## Initial benchmarks

| Method             | Mean        | Error      | StdDev     | Ratio | RatioSD | Rank | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------- |------------:|-----------:|-----------:|------:|--------:|-----:|---------:|---------:|---------:|-----------:|------------:|
| Lcs_Diff_Persons   | 5,817.71 us | 167.124 us | 110.542 us | 1.000 |    0.03 |    2 | 992.1875 | 992.1875 | 992.1875 | 3990.66 KB |        1.00 |
| Mayer_Diff_Persons |    11.47 us |   0.373 us |   0.247 us | 0.002 |    0.00 |    1 |   6.7139 |   1.1139 |        - |   109.9 KB |        0.03 |

## Updated benchmarks (less allocation)

| Method             | Mean        | Error      | StdDev     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |------------:|-----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Lcs_Diff_Persons   | 7,605.58 us | 363.619 us | 240.511 us | 1.001 |    0.04 |    2 |      - |      - |   58.7 KB |        1.00 |
| Mayer_Diff_Persons |    11.08 us |   0.734 us |   0.485 us | 0.001 |    0.00 |    1 | 5.7068 | 0.9460 |  93.34 KB |        1.59 |

## Optimized for balancing speed and memory

| Method             | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Lcs_Diff_Persons   | 107.52 us | 2.333 us | 1.543 us |  1.00 |    0.02 |    2 | 9.3994 | 2.1973 | 154.26 KB |        1.00 |
| Mayer_Diff_Persons |  10.84 us | 0.240 us | 0.158 us |  0.10 |    0.00 |    1 | 5.7068 | 0.9460 |  93.34 KB |        0.61 |
