<p align="center">
  <img src="images/icon.png" width="160" alt="AndThen icon: three amber rules flowing through a teal arrow into a stack of actions" />
</p>

<h1 align="center">AndThen</h1>

<p align="center">
  <a href="https://github.com/ShadowstarIO/AndThen/releases"><img alt="Release" src="https://img.shields.io/github/v/release/ShadowstarIO/AndThen?include_prereleases&style=flat-square&color=blue"></a>
  <a href="https://github.com/ShadowstarIO/AndThen/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/ShadowstarIO/AndThen/total?style=flat-square&color=blue&cacheSeconds=300"></a>
  <a href="https://github.com/ShadowstarIO/AndThen/actions/workflows/build.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/ShadowstarIO/AndThen/build.yml?style=flat-square"></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-MIT-blue?style=flat-square"></a>
  <img alt="Status: replaced by Starflow" src="https://img.shields.io/badge/status-replaced%20by%20Starflow-7a7a7a?style=flat-square">
</p>

> [!IMPORTANT]
> **AndThen is no longer supported. It has been replaced by [Starflow](https://github.com/ShadowstarIO/Starflow).**
> Starflow does what AndThen does, plus more. Install it from the [Shadowstar catalog](https://github.com/ShadowstarIO/XIV).
>
> **Bring your rules over:** in AndThen, open Settings and copy every rule (or right-click one rule and pick **Copy AT1**). In Starflow, click **Import**, pick **AndThen**, and paste. Rules come in off.
>
> AndThen stays here for reference. It is not updated and is no longer listed in the Shadowstar catalog.

<p align="center">
  <em>When conditions match, apply settings and run commands. Built on Dalamud.</em>
</p>

<p align="center">
  <a href="https://github.com/ShadowstarIO/Starflow"><b>Use Starflow instead</b></a> ·
  <a href="#what-it-does">What it does</a> ·
  <a href="#features">Features</a>
</p>

---

> Early development.
> Version **0.0.1.6** testing.

## What it does

Keep rules in a folder tree. Each rule has a Name, a Note, IF chips, and a THEN stack. Mode is Off, Dialog, or Auto.

- **Off** — does nothing until you run `/atn Name` or Test.
- **Dialog** — when the rule becomes true, a popup lists matching Dialog rules. Click one to apply. Same match set does not ask again. `/atn ask` brings the list back.
- **Auto** — runs the THEN stack once when the rule becomes true. Does not run again until it goes false and true again.

Each THEN row has its own wait in milliseconds, then the action. Settings default to a short wait. Log Out and Close Game cannot go below their minimum wait. Rules do not revert. Write a second rule for the other state.

## Features

* **Folder tree** : left list like Glamourer / Penumbra. Folders expand. Drag a rule onto a folder. Icons at the bottom of the list. Nested folders use `/`.
* **Conditions** : state, job, role, zone, world, data center, party, duty, group, time, weather, nearby, place, target, online status, account (home or visiting), mount, level, and housing. A housing chip is a district, ward (1–30), plot, subdivision, and apartment or chamber room. Subdivision has to match on its own. Apartment rooms are not plots.
* **Live chips** : teal means that chip is true right now. Click a chip to remove it.
* **THEN rows** : run in order. Click a row to remove it. Each row can wait before it runs.
* **Game settings** : grouped the way the configuration menus are. Labels and choices follow the options players see, including battle effects, nameplates, sound, and graphics. A graphics option writes both the standard value and the DirectX 11 value. Unknown words are not written as zero.
* **Penumbra** : search installed mods, store the current collection settings, apply them as temporary settings on you (or permanently into the collection). Reset clears AndThen temporary settings.
* **Quiet** : Settings can block Auto and Dialog during cutscenes, combat, duty, and similar states. `/atn Name` and Test still run.
* **Share** : JSON or `AT1.` from the rule. Settings can copy every rule at once. Imports start Off.
* **Rising edge only** : a rule that stays true does not apply again.

## Install

For new setups, install [Starflow](https://github.com/ShadowstarIO/Starflow) instead.

The last AndThen build is still in [Releases](https://github.com/ShadowstarIO/AndThen/releases), and this repo's own link below still works. Open `/andthen` after install.

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
