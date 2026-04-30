using System.Collections.Generic;

public class TabHistory
{
    private readonly List<(string EntityType, int EntityId)> _stack = new();
    private int _index = -1;

    public bool CanGoBack    => _index > 0;
    public bool CanGoForward => _index < _stack.Count - 1;
    public bool HasCurrent   => _index >= 0;

    public (string EntityType, int EntityId) Current => _stack[_index];

    public void Push(string entityType, int entityId)
    {
        if (_index >= 0 && _stack[_index] == (entityType, entityId)) return;
        _stack.RemoveRange(_index + 1, _stack.Count - _index - 1);
        _stack.Add((entityType, entityId));
        _index++;
    }

    public (string EntityType, int EntityId) Back()
    {
        if (CanGoBack) _index--;
        return Current;
    }

    public (string EntityType, int EntityId) Forward()
    {
        if (CanGoForward) _index++;
        return Current;
    }
}
