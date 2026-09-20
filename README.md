# AndThen

*When conditions match, apply settings and run commands. Built on Dalamud.*

---

> Early development.
> Version **0.0.1.3** testing.

## What it does

Keep rules in a folder tree. Each rule has a Name, a Note, IF chips, and a THEN stack. Mode is Off, Dialog, or Auto.

- **Off** — does nothing until you run `/atn Name` or Test.
- **Dialog** — when the rule becomes true, a popup lists matching Dialog rules. Click one to apply. Same match set does not ask again. `/atn ask` brings the list back.
- **Auto** — runs the THEN stack once when the rule becomes true. Does not run again until it goes false and true again.

Rules do not revert. Write a second rule for the other state.

## Features

* **Folder tree** : left list like Glamourer / Penumbra. Folders expand. Drag a rule onto a folder. `+ Rule` / `+ Folder` / Paste / Delete at the bottom. Nested folders use `/`.
* **Pick, then place** : choose Kind and Value first, then Add to IF / OR / NOT. Same for THEN.
* **Live chips** : teal means that chip is true right now.
* **Share** : JSON or `AT1.` from the rule. Settings can copy every rule at once. Imports start Off.
* **Rising edge only** : a rule that stays true does not apply again.

## Install

Testing build from [Releases](https://github.com/ShadowstarIO/AndThen/releases). Open `/andthen` after install.

```
https://raw.githubusercontent.com/ShadowstarIO/AndThen/main/repo.json
```

## Commands

| Command | Action |
| --- | --- |
| `/andthen` `/atn` | Window |
| `/atn Name` | Run that rule now |
| `/atn ask` | Open the Dialog list |
| `/atn why Name` | Print whether each chip is true |
| `/atn apply` | Run matching rules now |
| `/atn now` | Print matches |
| `/atn dry Name` | Print that rule's THEN stack |
| `/atn pause [seconds]` | Pause |
| `/atn resume` | Resume |
| `/atn zone` | Print place, job, time, weather |
| `/atn config` | Settings |
| `/atn help` | List commands |

## License

MIT. See [LICENSE](LICENSE).
