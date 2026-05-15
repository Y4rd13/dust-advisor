using System;
using System.Collections.Generic;

namespace DustAdvisor.Ui.Export
{
    public sealed class UndoStack
    {
        private sealed class Entry
        {
            public string Label;
            public Action Undo;
            public Action Redo;
        }

        private readonly LinkedList<Entry> _undo = new LinkedList<Entry>();
        private readonly LinkedList<Entry> _redo = new LinkedList<Entry>();
        private readonly int _maxDepth;

        public UndoStack(int maxDepth)
        {
            _maxDepth = maxDepth;
        }

        public void Push(string label, Action undo, Action redo = null)
        {
            _undo.AddLast(new Entry { Label = label, Undo = undo, Redo = redo });
            while (_undo.Count > _maxDepth) _undo.RemoveFirst();
            _redo.Clear();
        }

        public string Undo()
        {
            if (_undo.Count == 0) return null;
            var entry = _undo.Last.Value;
            _undo.RemoveLast();
            entry.Undo?.Invoke();
            _redo.AddLast(entry);
            return entry.Label;
        }

        public string Redo()
        {
            if (_redo.Count == 0) return null;
            var entry = _redo.Last.Value;
            _redo.RemoveLast();
            entry.Redo?.Invoke();
            _undo.AddLast(entry);
            return entry.Label;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
    }
}
