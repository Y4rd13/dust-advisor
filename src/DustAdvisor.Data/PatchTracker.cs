using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class PatchUpdateResult
    {
        public bool PatchDetected { get; }
        public int ChangedCardCount { get; }
        public string Note { get; }

        public PatchUpdateResult(bool patchDetected, int changedCardCount, string note)
        {
            PatchDetected = patchDetected;
            ChangedCardCount = changedCardCount;
            Note = note;
        }
    }

    public sealed class PatchTracker
    {
        private sealed class State
        {
            [JsonProperty("lastModified")] public string LastModified { get; set; }
            [JsonProperty("lastCheckedAt")] public DateTimeOffset LastCheckedAt { get; set; }
        }

        private readonly IPatchHttpFetcher _http;
        private readonly RefundAutoRepository _refundRepo;
        private readonly string _url;
        private readonly string _previousCachePath;
        private readonly string _statePath;
        private readonly string _refundAutoPath;
        private readonly TimeSpan _refundWindow;

        public PatchTracker(
            IPatchHttpFetcher http,
            RefundAutoRepository refundRepo,
            string url,
            string previousCachePath,
            string statePath,
            string refundAutoPath,
            TimeSpan refundWindow)
        {
            _http = http;
            _refundRepo = refundRepo;
            _url = url;
            _previousCachePath = previousCachePath;
            _statePath = statePath;
            _refundAutoPath = refundAutoPath;
            _refundWindow = refundWindow;
        }

        public async Task<PatchUpdateResult> UpdateAsync(DateTimeOffset now, CancellationToken ct)
        {
            var currentLm = await _http.HeadLastModifiedAsync(_url, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(currentLm))
                return new PatchUpdateResult(false, 0, "no Last-Modified header on upstream");

            // First run (or recovery from missing files): seed the cache without diffing.
            if (!File.Exists(_statePath) || !File.Exists(_previousCachePath))
            {
                var firstFetch = await _http.GetWithLastModifiedAsync(_url, ct).ConfigureAwait(false);
                EnsureDir(_previousCachePath);
                File.WriteAllText(_previousCachePath, firstFetch.Body);
                SaveState(firstFetch.LastModified ?? currentLm, now);
                return new PatchUpdateResult(false, 0, "first run; cached current build, will diff next patch");
            }

            var state = LoadState();
            if (state != null && string.Equals(state.LastModified, currentLm, StringComparison.Ordinal))
            {
                SaveState(currentLm, now);
                return new PatchUpdateResult(false, 0, "no patch since last check");
            }

            // New build → fetch full body, diff against previous cache, merge into refund_auto.json.
            var fresh = await _http.GetWithLastModifiedAsync(_url, ct).ConfigureAwait(false);
            var prevSnaps = ParseSnapshots(File.ReadAllText(_previousCachePath));
            var currSnaps = ParseSnapshots(fresh.Body);
            var changes = PatchDiffDetector.Diff(prevSnaps, currSnaps);

            DateTimeOffset expiresUtc;
            if (!DateTimeOffset.TryParse(fresh.LastModified ?? currentLm, out var parsed))
                parsed = now;
            expiresUtc = parsed + _refundWindow;

            var existing = _refundRepo.Load(_refundAutoPath, now);
            var merged = RefundAutoRepository.MergeWithNewPatch(existing, changes, expiresUtc, detectedAt: now, now: now);
            _refundRepo.Save(_refundAutoPath, merged);

            File.WriteAllText(_previousCachePath, fresh.Body);
            SaveState(fresh.LastModified ?? currentLm, now);

            int changedCardCount = changes.Select(c => c.CardId).Distinct().Count();
            return new PatchUpdateResult(true, changedCardCount, $"detected patch with {changedCardCount} card change(s)");
        }

        private State LoadState()
        {
            if (!File.Exists(_statePath)) return null;
            try { return JsonConvert.DeserializeObject<State>(File.ReadAllText(_statePath)); }
            catch { return null; }
        }

        private void SaveState(string lastModified, DateTimeOffset now)
        {
            var state = new State { LastModified = lastModified, LastCheckedAt = now };
            EnsureDir(_statePath);
            File.WriteAllText(_statePath, JsonConvert.SerializeObject(state, Formatting.Indented));
        }

        private static void EnsureDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        private static List<CardSnapshot> ParseSnapshots(string json)
        {
            var dtos = JsonConvert.DeserializeObject<List<HearthstoneJsonCardDto>>(json) ?? new List<HearthstoneJsonCardDto>();
            var result = new List<CardSnapshot>(dtos.Count);
            foreach (var d in dtos)
            {
                if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                if (!d.Collectible) continue;
                result.Add(new CardSnapshot(d.Id, d.Name ?? d.Id, d.Cost, d.Attack, d.Health, d.Durability, d.Armor, d.Text));
            }
            return result;
        }
    }
}
