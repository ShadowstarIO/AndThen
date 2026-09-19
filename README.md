# AndThen

*When conditions match, apply settings and run commands. Built on Dalamud.*

---

> Early development.
> Version **0.0.1.1** testing. Folders, Off / Dialog / Auto, searchable config actions, and a first full chip set are in place.

## What it does

Keep rules in folders. Each rule has a Name, a Note, IF chips, and a THEN stack. Mode is Off, Dialog, or Auto.

- **Off** — does nothing until you run `/atn Name` or Test.
- **Dialog** — when the rule becomes true, a popup lists matching Dialog rules. Click one to apply. Same match set does not ask again. `/atn ask` brings the list back.
- **Auto** — runs the THEN stack once when the rule becomes true. Does not run again until it goes false and true again.

Rules do not revert. Write a second rule for the other state.

## Features

* **Folders** : expandable groups. Drag a rule onto a folder. Mute a folder to silence it.
* **Chips wrap** : IF / OR / NOT and THEN rows wrap instead of running off the window.
* **Conditions** : state, job, role, zone, world, DC, party size, duty, group size, time, weather, nearby count.
* **THEN** : `/command`, Wait, online status, and any System or UI GameConfig option behind search.
* **Pacing** : check interval is seconds. Config writes are spaced so a long stack does not hitch.
* **Rising edge only** : a rule that stays true does not apply again.
* **Share** : JSON or `AT1.` from the rule menu. Imports start Off.

## How a rule is evaluated

1. Skip if the rule or its folder is muted / off.
2. AND chips must all match.
3. If any OR chips exist, one of them must match.
4. NOT chips must not match.
5. False → true is the only Auto / Dialog trigger.
6. `/atn Name` always runs that rule, chips or not.
7. Two Autos on the same edge: list order, last write wins.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/ShadowstarIO/XIV/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **AndThen**, and install.

## Commands

| Command | Action |
| --- | --- |
| `/andthen` `/atn` | Window |
| `/atn Name` | Run that rule now |
| `/atn ask` | Open the Dialog list |
| `/atn apply` | Run matching rules now |
| `/atn now` | Print matches |
| `/atn dry Name` | Print that rule's THEN stack |
| `/atn pause [seconds]` | Pause |
| `/atn resume` | Resume |
| `/atn zone` | Print place, job, time, weather |
| `/atn config` | Settings |
| `/atn help` | List commands |

## Share format

Copy JSON or an `AT1.` code from the rule menu. Sample rules use generic labels only.

## More from this author

* [StatusShift](https://github.com/ShadowstarIO/StatusShift)
* [LightsOn](https://github.com/XozaShadow/LightsOn)

## License

MIT. See [LICENSE](LICENSE).
