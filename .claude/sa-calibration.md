# Solution-architect calibration log

Each time the user overrides an answer `solution-architect` gave on their behalf, the main session adds one entry here in the same PR as the corrected document, newest first. `solution-architect` reads this file before answering and treats each lesson as a rule about the user's taste. Entries are never edited or removed; a later entry may supersede an earlier one by saying so.

Format:

```
### YYYY-MM-DD: <short topic>
- SA answered: <what the SA decided, with the question number or ID>
- User decided: <what the user chose instead>
- Lesson: <the general rule to apply next time, one sentence>
```

### 2026-10-23: allowed domains for the widget
- SA answered: PRD01 question 4: accept only `https` and domain names, with localhost as the only exception (tự tin thấp).
- User decided: accept `http` and `https`, and exact IPv4 addresses; internal names without a dot stay rejected, except localhost.
- Lesson: where the widget may run is the user's call about their own products, so when strictness only guards against a hypothetical case, allow the real deployment option (internal `http` apps, IP-addressed test servers) and record the caveat.
