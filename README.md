# Tether

One chest, one bench. Crafting at that bench can reach into that chest.

Built against the installed game (0.221.12, Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## The constraint is the design

The mod this replaces pulls from *every* container in range, which quietly turns your whole
base into one inventory — you stop organising anything because there is no reason to.

Tether links **exactly one chest to one bench**. Your forge gets its metal chest, your
workbench gets its wood-and-leather chest, and keeping them stocked is still a thing you
do. It saves the walk back and forth; it does not abolish storage as a concern.

That rule is structural rather than enforced: the link is stored on the *station's* ZDO, so
a station holds one value and tethering a second chest simply replaces the first. A chest
serving more than one bench is a different question and is allowed.

## Using it

Look at a chest and press **Numpad 7**. It tethers to the nearest bench in range. Press
again on a tethered chest to release it.

One step rather than two — no selecting the bench first — because the bench is never
ambiguous in practice: you are standing at it. It is also the same verb Thralls already uses
for setting a thrall's drop-off chest, so there is nothing new to learn.

Standing between a workbench and a forge gives you both their chests. Each bench still has
exactly one.

## Status: v0.1 — untested

Built and compiling. Not yet playtested.

## Design notes

**Everything funnels through two methods.** Every requirement check and every consumption
in Valheim ends up at `Inventory.CountItems` and `Inventory.RemoveItem` on the player's own
inventory. Patching those is the whole mod.

**But they are called constantly by everything else**, so patching unconditionally would
have chest contents bleeding into inventory weight, item counts and the hotbar. Hence a
scope flag: `Player.HaveRequirementItems`, `Player.HaveRequirements(Piece, …)` and
`Player.ConsumeResources` bracket themselves, and the two `Inventory` patches do nothing
unless a bracket is open.

**Consumption has to trim what vanilla removes.** The prefix takes the shortfall out of the
chests, then sets `amount` to `carried + shortfall` so the game only removes what the
player actually has. Without that trim, `RemoveItem` would quietly stop when it ran out and
the recipe would be built for less than it cost.

**Counting the shortfall needs the patch held off.** That calculation runs inside an open
scope, where `CountItems` already answers with the chests included — asking there would
report nothing missing and take nothing from the chest. A suspend flag gives the true
carried count.

**No explicit container save.** `Inventory.RemoveItem` ends in `Changed()`, and `Container`
wires that to its own `OnContainerChanged`, which persists it. Ownership is claimed first,
since editing a shared object you do not own is a change that may just be overwritten.

**Reflection targets are checked at startup** — `CraftingStation.m_allStations` and
`Player.m_hovering` are both private, and `AccessTools` answers a name it cannot find with
`null`. They are verified in `Awake` and named in an error if missing, rather than surfacing
later as an unexplained `NullReferenceException`.

### Not covered yet

Recipes using `m_requireOnlyOneIngredient` resolve through
`Player.FindClosestRequirementItem`, which returns an actual item rather than a count. That
path is not patched, so those few recipes still only see your own inventory.

The crafting UI's have/need numbers come from `InventoryGui`, which is also unpatched — a
recipe may craft successfully while its requirement line still looks short. Worth fixing
once the core is confirmed working.

## Config

`BepInEx\config\robbin.valheim.tether.cfg`

| Key | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off ignores links without forgetting them |
| `KeyTether` | `Keypad7` | Tether or release the chest you are looking at |
| `Messages` | `true` | Corner messages on tether and release |
| `Verbose` | `false` | Log every withdrawal from a tethered chest |

Numpad 0–6 are already taken by Thralls, hence 7.

## Building

```bash
dotnet build
```

Or build every own-mod into the shared play profile with
`valheim-own-profile\build-all.ps1`.

## What to check

1. Tether a chest to a workbench, put wood in it, and build something with an empty pack.
2. Confirm the wood actually leaves the chest.
3. **Split a cost across both** — some in your pack, some in the chest — and confirm the
   total taken is right and nothing is duplicated or eaten twice.
4. Tether a second chest to the same bench; the first should be released.
5. Walk away from the bench and confirm the chest no longer counts.
6. Check that ordinary inventory weight and item counts are unaffected.

## Author

Tether is an original mod by **Robbin Thijssen** (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed — see `LICENSE`.
