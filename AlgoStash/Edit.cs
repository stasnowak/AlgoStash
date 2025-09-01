namespace DiffCore;

public enum EditKind { Match, Insert, Delete }

public readonly record struct Edit<T>(EditKind Kind, int AIndex, int BIndex, int Length, IReadOnlyList<T>? Items = null);
