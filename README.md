[![🔍 • CodeQL](https://github.com/stasnowak/AlgoStash/actions/workflows/codeql.yml/badge.svg)](https://github.com/stasnowak/AlgoStash/actions/workflows/codeql.yml)

[![🚀 • .NET CI](https://github.com/stasnowak/AlgoStash/actions/workflows/build.yml/badge.svg)](https://github.com/stasnowak/AlgoStash/actions/workflows/build.yml)


# AlgoStash

A small .NET 9 library with:
- Sequence diff algorithms (LCS-based and a fast heuristic)
- A lightweight graph PathFinder utility for finding shortest paths and retrieving edge transitions

The library is designed to be simple, fast, and easy to drop into your projects.

## Features

- LCS-based diff (CreateLCS): minimal edit script via dynamic programming + backtracking
- Heuristic diff (CreateMayer): very fast forward scan, not guaranteed minimal
- Generic APIs over T[] with pluggable IEqualityComparer<T>
- PathFinder: build a directed graph, find shortest paths (BFS), and get transitions with actions
- Thorough unit tests and initial performance benchmarks

## Project structure

- AlgoStash: Class library with algorithms and a small console demo in Program.cs
- AlgoStash.Tests.Unit: xUnit test project

## Requirements

- .NET 9 SDK

## Build, run, test

- Build:
  - dotnet build
- Run demo:
  - dotnet run --project .\AlgoStash
- Run tests:
  - dotnet test

## Installation / Usage in your solution

The library is not published to a public feed by default. You can:
- Add a project reference to AlgoStash from your application project, or
- Pack locally and reference the nupkg:
  - dotnet pack .\AlgoStash -c Release
  - dotnet nuget add source .\AlgoStash\bin\Release
  - dotnet add <your-project> package AlgoStash -s .\AlgoStash\bin\Release

## Quickstart

### Diffs

Compute differences between two sequences using either algorithm.