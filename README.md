# AndThen

*When conditions match, apply settings and run commands. Built on Dalamud.*

---

> Early development.
> Version **0.0.1.2** testing.

## What it does

Keep rules in folders. Each rule has a Name, a Note, IF chips, and a THEN stack. Mode is Off, Dialog, or Auto.

- **Off** — does nothing until you run `/atn Name` or Test.
- **Dialog** — when the rule becomes true, a popup lists matching Dialog rules. Click one to apply. Same match set does not ask again. `/atn ask` brings the list back.
- **Auto** — runs the THEN stack once when the rule becomes true. Does not run again until it goes false and true again.

Rules do not revert. Write a second rule for the other state.

## Features

* **Folders** : expandable groups. Drag a rule onto a folder. Mute a folder to silence it.
* **Live chips** : teal means that chip is true right now. Hover for true / false.
* **Conditions** : state, job, role, zone, world, DC, party size, duty type, group size, place, target, time, weather, nearby count.
* **THEN** : `/command`, Wait, online status, Notify, and any System or UI GameConfig option behind search.
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

## Chip notes

| Kind | Values |
| --- | --- |
| State | InDuty, InCombat, Cutscene, GPose, Mounted, Housing, Sitting, Event, … |
| Duty | any, none, solid, dungeon, trial, raid, alliance, pvp, deep, field |
| Place | Town, Overworld, Indoor, Housing, Inn, Sanctuary, PvP, GoldSaucer, DeepDungeon, Field |
| Time | day, night, dawn, dusk, `et>=18`, `lt>=22`, weekday |
| Nearby | empty, few, crowded, or `>=12` |
| Target | none, any, player, npc |
| Group | Solo, Light, Full, Alliance |

Battle effects, nameplates, camera, and sound are THEN Config rows (search the option name).

## Install

Testing build from [Releases](https://github.com/ShadowstarIO/AndThen/releases). Open `/andthen` after install.

Custom repo:

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

## Share format

Copy JSON or an `AT1.` code from the rule menu. Sample rules use generic labels only.

## More from this author

* [StatusShift](https://github.com/ShadowstarIO/StatusShift)
* [LightsOn](https://github.com/XozaShadow/LightsOn)

## License

MIT. See [LICENSE](LICENSE).
