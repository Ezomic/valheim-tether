# Changelog

Notable changes to Tether. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [0.1.0] — 2026-08-16

First release.

### The link

- **One chest, one bench.** Crafting at that bench can reach into that chest.
- Look at a chest and press **Numpad 7** to tether it to the nearest bench in range. Press
  again on a tethered chest to release it.
- One step rather than two, with no selecting the bench first, because the bench is never
  ambiguous in practice — you are standing at it.
- Standing between a workbench and a forge gives you both their chests. Each bench still has
  exactly one.

### The constraint is the design

The mod this replaces pulls from *every* container in range, which quietly turns a base into
one inventory and removes any reason to organise anything. Tether links exactly one chest to
one bench: your forge gets its metal chest, your workbench gets its wood-and-leather chest,
and keeping them stocked is still something you do. It saves the walk; it does not abolish
storage as a concern.

That rule is structural rather than enforced. The link lives on the **station's** ZDO, so a
station holds one value and tethering a second chest simply replaces the first. A chest
serving more than one bench is a different question, and is allowed.

### Correctness

- Every requirement check and every consumption in Valheim funnels through
  `Inventory.CountItems` and `Inventory.RemoveItem`, so patching those is the whole mod — but
  they are called constantly by everything else, so the patches do nothing unless one of the
  three crafting entry points has opened a scope. Without that, chest contents bleed into
  inventory weight, item counts and the hotbar.
- **Consumption trims what vanilla removes**, taking the shortfall from the chest and leaving
  the game to remove only what the player actually has. Without the trim, `RemoveItem` stops
  when it runs out and the recipe is built for less than it cost.
- Ownership is claimed before editing a chest, since writing to a shared object you do not
  own is a change that may simply be discarded.
- Loads on dedicated servers, and declares to Core's version gate.

### Known limits

- **Untested in a real session.** It builds and compiles; it has not been playtested, which
  is why this is 0.1 and not 1.0. The feature is complete as described above — what is
  missing is evidence that it behaves.
