# AndThen

*When conditions match, apply settings and run commands. Built on Dalamud.*

---

> Early development.
> Version **0.0.1.0**. The window, rule list, chip editor, rising-edge apply, and a first set of conditions and THEN actions are in place. More conditions, graphics keys, and share polish still to land.

## What it does

Keep a folder of rules. Each rule is a set of IF / AND / OR / NOT chips plus a THEN stack. When the chips go from false to true, AndThen applies the stack once: game options, online status, and `/commands`. Rules do not revert. Write a second rule for the other state.

Open the window with `/andthen` or `/atn`.

## Features

* **Folders on the left** : rules sit in folders you name. Filter the list. Move a rule up or down to change who wins when two rules fire on the same edge.
* **Chip editor** : AND chips must all match. OR chips need one match if any are set. NOT chips must stay false.
* **Rising edge** : the THEN stack runs when a rule becomes true, not every frame it stays true.
* **THEN stack** : add as many rows as you need. Settings, status, `/command`, and Wait. Want a command twice? Add two command rows. Need a gap? Add Wait. Need a loop? Use a game macro.
* **No revert** : a rule only applies. Pair it with another rule that matches the other state if you want the previous settings back.
* **Share** : copy JSON or an `AT1.` code from the rule menu. Paste to import. Imported rules start disabled.
* **Pause** : `/atn pause` stops evaluation. `/atn resume` starts it again. Optional pause duration in seconds.

## How a rule is evaluated

1. Skip the rule if it is off.
2. Read the current place, job, duty, party, and condition flags.
3. AND chips must all match.
4. If any OR chips exist, at least one of them must match.
5. NOT chips must not match.
6. If the last tick was false and this tick is true, run the THEN stack from top to bottom.
7. If two matching rules set the same option on the same edge, the rule lower in the list wins. Use **Move down** to raise effective priority.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/ShadowstarIO/XIV/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **AndThen**, and install.

## Commands

| Command | Action |
| --- | --- |
| `/andthen` | Toggle the main window |
| `/atn` | Alias for `/andthen` |
| `/atn apply` | Run matching rules now |
| `/atn now` | Print matching rules without running them |
| `/atn pause [seconds]` | Pause evaluation |
| `/atn resume` | Resume evaluation |
| `/atn zone` | Print the current place and job |
| `/atn config` | Open settings |
| `/atn help` | List commands |

## Share format

From a rule: **Copy JSON** or **Copy AT1**. Paste either into the import box. Sample rules use generic labels only.

## More from this author

* [StatusShift](https://github.com/ShadowstarIO/StatusShift) — search comment and online status from activity, time, day, and place
* [LightsOn](https://github.com/XozaShadow/LightsOn) — venue occupancy listings

## License

MIT. See [LICENSE](LICENSE).
