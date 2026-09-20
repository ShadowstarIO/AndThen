# AndThen

*When conditions match, apply settings and run commands. Built on Dalamud.*

---

> Early development.
> Version **0.0.1.4** testing.

## What it does

Keep rules in a folder tree. Each rule has a Name, a Note, IF chips, and a THEN stack. Mode is Off, Dialog, or Auto.

- **Off** — does nothing until you run `/atn Name` or Test.
- **Dialog** — when the rule becomes true, a popup lists matching Dialog rules. Click one to apply. Same match set does not ask again. `/atn ask` brings the list back.
- **Auto** — runs the THEN stack once when the rule becomes true. Does not run again until it goes false and true again.

Each THEN row has its own wait in milliseconds, then the action. Settings default to a short wait. Log Out and Close Game cannot go below their minimum wait. Rules do not revert. Write a second rule for the other state.

## Features

* **Folder tree** : left list like Glamourer / Penumbra. Folders expand. Drag a rule onto a folder. Icons at the bottom of the list. Nested folders use `/`.
* **Conditions** : pick a menu (You / Place / Party / Time / Target / Account) then the option. Housing expands to Ward / Plot / Room. World lists filter by Data Center.
* **Live chips** : teal means that chip is true right now. Click a chip to edit it.
* **THEN rows** : wait ms, action, options, up/down or drag to reorder. Editable in place.
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
