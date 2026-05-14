using System;
using System.Collections.Generic;

namespace DustAdvisor.Ui.Export
{
    public sealed class SessionLedgerEntry
    {
        public DateTimeOffset At { get; }
        public int CardCount { get; }
        public int Dust { get; }
        public IReadOnlyList<string> CardIds { get; }

        public SessionLedgerEntry(DateTimeOffset at, int cardCount, int dust, IReadOnlyList<string> cardIds)
        {
            At = at;
            CardCount = cardCount;
            Dust = dust;
            CardIds = cardIds;
        }
    }

    public sealed class SessionLedger
    {
        private readonly List<SessionLedgerEntry> _entries = new List<SessionLedgerEntry>();
        public IReadOnlyList<SessionLedgerEntry> Entries => _entries;
        public int TotalDust { get; private set; }

        public void RecordConfirm(int dust, IReadOnlyList<string> cardIds)
        {
            _entries.Add(new SessionLedgerEntry(DateTimeOffset.UtcNow, cardIds.Count, dust, cardIds));
            TotalDust += dust;
        }

        public void Undo()
        {
            if (_entries.Count == 0) return;
            var last = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
            TotalDust -= last.Dust;
        }
    }
}
