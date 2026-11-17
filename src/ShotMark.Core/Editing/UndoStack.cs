namespace ShotMark.Core.Editing;

public sealed class UndoStack<T>(int limit = 200) where T : class
{
    private readonly List<T> _undo = [];
    private readonly List<T> _redo = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Record(T before)
    {
        _undo.Add(before);
        if (_undo.Count > limit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public T? Undo(T current)
    {
        if (_undo.Count == 0) return null;
        var state = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        return state;
    }

    public T? Redo(T current)
    {
        if (_redo.Count == 0) return null;
        var state = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        return state;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
