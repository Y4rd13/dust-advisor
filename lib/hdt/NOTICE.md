# HDT Reference Binaries

This directory vendors two binaries from
[Hearthstone Deck Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker)
as compile-time references for the Dust Advisor plugin. They are needed by
`DustAdvisor.Hdt` to resolve `IPlugin`, `HearthMirror`, and other HDT APIs at
build time. The files are NOT redistributed inside the release artifact —
end users must have HDT installed separately to use the plugin.

| File | Source |
|------|--------|
| `HearthstoneDeckTracker.exe` | HDT release v1.52.6 |
| `HearthMirror.dll`           | HDT release v1.52.6 |

HDT is released by HearthSim under the MIT License. The original license text
follows:

```
MIT License

Copyright (c) HearthSim

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Upgrading

When HDT releases a new version that this plugin needs to track:

1. Install the new HDT version locally.
2. Copy the new `HearthstoneDeckTracker.exe` and `HearthMirror.dll` into this
   directory, overwriting the previous files.
3. Commit with a message like `chore(deps): bump HDT reference to vX.Y.Z`.
