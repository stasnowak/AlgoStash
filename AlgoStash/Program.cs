using System.Text.Json;
using AlgoStash;

int[] a = [0, 1, 2, 3, 4, 5];
int[] b = [0, 2, 3, 4, 5, 6];

var diffLCS = Diffs.CreateLCS(a, b, EqualityComparer<int>.Default);

foreach (var entry in diffLCS.Entries)
{
    Console.WriteLine(JsonSerializer.Serialize(entry));   
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine();

var diffMayer = Diffs.CreateMayer(a, b, EqualityComparer<int>.Default);

foreach (var entry in diffMayer.Entries)
{
    Console.WriteLine(JsonSerializer.Serialize(entry));   
}
