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

Wait on a rule is how long the condition must stay true before Auto or Dialog fires. Rules do not revert. Write a second rule for the other state.

## Features

* **Folder tree** : left list like Glamourer / Penumbra. Folders expand. Drag a rule onto a folder. Icons at the bottom of the list (new rule, new folder, paste, delete). Nested folders use `/`.
* **Pick, then place** : choose Kind and Value first, then Add to IF / OR / NOT. Same for THEN. Long lists open a Browse window. `current: [Name +]` uses what is true right now.
* **Live chips** : teal means that chip is true right now.
* **Quiet** : Settings can block Auto and Dialog during cutscenes, combat, duty, and similar states. `/atn Name` and Test still run.
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
