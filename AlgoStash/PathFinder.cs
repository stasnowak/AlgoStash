namespace AlgoStash;

public class State
{
    public int Id { get; set; }
    public Func<bool> Action { get; set; }

    public State(object id)
    {
        Id = Convert.ToInt32(id);
    }

    public State(int id)
    {
        Id = id;
    }
}

public class PathFinder
{
    // Use int-keyed collections to avoid linear scans and reduce allocations.
    private readonly Dictionary<int, State> _states = new();
    private readonly Dictionary<int, HashSet<int>> _adjacency = new();
    private readonly Dictionary<(int from, int to), Func<bool>> _actions = new();

    private void EnsureState(int id)
    {
        if (_states.ContainsKey(id))
            return;

        var s = new State(id);
        _states[id] = s;
        _adjacency[id] = new HashSet<int>();
    }

    public void AddStates(params object[] ids)
    {
        if (ids == null || ids.Length == 0)
            return;

        foreach (var raw in ids)
        {
            int id;
            try
            {
                id = Convert.ToInt32(raw);
            }
            catch
            {
                // Skip non-convertible entries to keep API robust.
                continue;
            }

            EnsureState(id);
        }
    }

    public void AddTransition(object from, object to, Func<bool> action)
    {
        int f, t;
        try
        {
            f = Convert.ToInt32(from);
            t = Convert.ToInt32(to);
        }
        catch
        {
            // Invalid ids; ignore to keep method robust.
            return;
        }

        EnsureState(f);
        EnsureState(t);

        // Deduplicate edges; HashSet.Add returns false if edge exists.
        _adjacency[f].Add(t);

        // Last-writer wins for action to keep consistent state.
        _actions[(f, t)] = action;
    }

    public List<Tuple<State, State, Func<bool>>> GetTransitions(object from, object to)
    {
        var values = new List<Tuple<State, State, Func<bool>>>();

        var nodes = GetStatesToReach(from, to);
        if (nodes.Count < 2)
            return values;

        for (int i = 1; i < nodes.Count; i++)
        {
            var a = nodes[i - 1];
            var b = nodes[i];
            if (_actions.TryGetValue((a.Id, b.Id), out var action))
            {
                values.Add(Tuple.Create(a, b, action));
            }
        }

        return values;
    }

    public List<State> GetStatesToReach(object from, object to)
    {
        int f, t;
        try
        {
            f = Convert.ToInt32(from);
            t = Convert.ToInt32(to);
        }
        catch
        {
            return new List<State>();
        }

        if (!_states.ContainsKey(f) || !_states.ContainsKey(t))
            return new List<State>();

        if (f == t)
            return new List<State> { _states[f] };

        var parent = new Dictionary<int, int>(capacity: Math.Max(4, _states.Count / 4));
        var visited = new HashSet<int> { f };
        var queue = new Queue<int>();
        queue.Enqueue(f);

        bool found = false;

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            if (!_adjacency.TryGetValue(v, out var neighbors) || neighbors.Count == 0)
                continue;

            foreach (var n in neighbors)
            {
                if (!visited.Add(n))
                    continue;

                parent[n] = v;

                // Early exit when target reached to avoid exploring the entire graph.
                if (n == t)
                {
                    found = true;
                    queue.Clear();
                    break;
                }

                queue.Enqueue(n);
            }
        }

        if (!found && !parent.ContainsKey(t))
            return new List<State>();

        // Reconstruct path from t back to f
        var pathIds = new List<int>();
        int cur = t;
        pathIds.Add(cur);
        while (cur != f)
        {
            if (!parent.TryGetValue(cur, out var p))
                return new List<State>(); // No path found
            cur = p;
            pathIds.Add(cur);
        }
        pathIds.Reverse();

        var path = new List<State>(pathIds.Count);
        for (int i = 0; i < pathIds.Count; i++)
        {
            path.Add(_states[pathIds[i]]);
        }
        return path;
    }
}