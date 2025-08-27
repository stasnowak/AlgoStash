using System.Collections;

int[] array = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

SomeIn(array);
array.ForEach();

SomeOut(out var outArray);
outArray.ForEach();


void SomeIn(in int[] inArray)
{
    inArray[3] = 100;
}

void SomeOut(out int[] outArray)
{
    outArray = [1, 2, 3];
}

bool TryDivide(int a, int b, out int result)
{
    if (b == 0)
    {
        result = 0;
        return false;
    }
    result = a / b;
    return true;
}

if (TryDivide(10, 2, out var q))
{
    Console.WriteLine(q);
}
else
{
    Console.WriteLine("Dzielenie przez zero");
}

void Swap<T>(ref T a, ref T b)
{
    (a, b) = (b, a);
}

int x = 1, y = 2;
Swap(ref x, ref y);

double Distance(in Vec2 a, in Vec2 b)
{
    double dx = b.X - a.X;
    double dy = b.Y - a.Y;
    return Math.Sqrt(dx*dx + dy*dy);
}

var p1 = new Vec2(0, 0);
var p2 = new Vec2(3, 4);
Console.WriteLine(Distance(in p1, in p2));

public readonly struct Vec2
{
    public readonly double X, Y;
    public Vec2(double x, double y) { X = x; Y = y; }
}

public static class ArrayExtensions
{
    public static int[] AddingWhatWhere(this int[] array, int what, int where)
    {
        for (int i = array.Length - 2; i >= where; i--)
        {
            array[i + 1] = array[i];
        }
        
        array[where] = what;
        
        return array;
    }

    public static int[] SubtractingWhatWhere(this int[] array, int what, int where)
    {
        for (int i = 1; i < where; i++)
        {
            array[i - 1] = array[i];
        }
        
        array[where-1] = what;
        
        return array;
    }
    
    public static void ForEach(this int[] array)
    {
        foreach (var t in array)
        {
            Console.WriteLine(t);
        }

        Console.WriteLine();
    }
}
