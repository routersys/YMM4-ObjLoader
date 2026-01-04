namespace ObjLoader.Services
{
    internal class UndoStack<T>
    {
        private readonly Stack<T> _undo = new();
        private readonly Stack<T> _redo = new();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Push(T state)
        {
            _undo.Push(state);
            _redo.Clear();
        }

        public bool TryUndo(T currentState, out T result)
        {
            if (_undo.Count == 0)
            {
                result = default!;
                return false;
            }
            _redo.Push(currentState);
            result = _undo.Pop();
            return true;
        }

        public bool TryRedo(T currentState, out T result)
        {
            if (_redo.Count == 0)
            {
                result = default!;
                return false;
            }
            _undo.Push(currentState);
            result = _redo.Pop();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}