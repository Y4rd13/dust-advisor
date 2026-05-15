# Curated data

## `uncraftable.json`

Array of `{ "cardId": "...", "premium": "Regular|Golden|Signature|Diamond" }`. Each entry
marks a specific copy as locked and excludes it from disenchant recommendations.

Update on each Hearthstone patch by checking the in-game "uncraftable" filter or the
patch notes for new Tavern Pass / Rewards Track / Achievement / Bundle cards.

## `refund.json`

Array of `{ "cardId": "...", "expiresUtc": "YYYY-MM-DDTHH:MM:SSZ" }`. Each entry marks a
card that is currently in its 14-day post-balance-change full-refund window. Entries are
automatically ignored when expired.
