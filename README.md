# AndThen

When conditions match, apply settings and run commands.

AndThen is a Dalamud plugin. Folders on the left, editor on the right. Rules use AND / OR / NOT chips. When a rule becomes true it runs a THEN stack: game settings, online status, and `/commands`. Rules do not revert. Write a second rule for the other state.

`/andthen` or `/atn`.

## Install

Dalamud plugin installer → **Settings → Experimental → Custom Plugin Repositories**, add:

```
https://raw.githubusercontent.com/ShadowstarIO/XIV/main/repo.json
```

Save, `/xlplugins`, install AndThen.

## How a rule works

1. **IF** chips must all match (AND).
2. **OR** chips: if any are set, at least one of those must match.
3. **NOT** chips must not match.
4. On the rising edge (false → true), the THEN stack runs once, top to bottom.

No built-in loop. Want a command twice? Add two command rows. Need a pause? Add a Wait row. Need a cycle? Use a game macro.

Lower list position wins when two matching rules set the same setting on the same edge. Use Move up / Move down to change order.

## Commands

| | |
| --- | --- |
| `/andthen` `/atn` | Window |
| `/atn apply` | Run matching rules now |
| `/atn now` | Preview matches, do not run |
| `/atn pause [seconds]` | Pause rules |
| `/atn resume` | Resume |
| `/atn zone` | Print current place and job |
| `/atn config` | Settings |

## Share

Copy JSON or an `AT1.` share code from the rule menu. Sample rules use generic names only.

## Icon

D17 wants `images/icon.png` at 512×512. `images/icon.svg` is the source.

[MIT](LICENSE)
