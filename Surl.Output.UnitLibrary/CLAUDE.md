# Surl.Output.UnitLibrary

Phase 1.

What Surl writes about its exchanges: `-v` and `--trace` style logs of each conversation
from the server's side, a `-w` style line per exchange, and the listener status line.

Never write to the console directly: write to injected writers, so the tests need no
console.
