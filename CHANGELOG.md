# Changelog

## 0.1.0 - 2026-08-17

First release. One chest tied to one bench, and crafting at that bench can reach into it.

### Core is optional

Tether installs and runs on BepInEx alone, and installing it from Thunderstore does not pull
anything else in with it.

Nothing was lost in the split. Core was carrying exactly one call, and the reason usually given
for insisting on it does not apply to this mod: that reason is a mod registering a prefab or
altering item data, where a client without it discards ZDOs it cannot resolve and destroys what
is already standing in the world. Tether registers no prefabs and no items. A link is an
ordinary value on a vanilla station, which a vanilla client stores and ignores. So a party
where only some people have it is just a party where only some people have the feature.

The dependency is soft rather than hard, because a hard dependency that is absent does not
degrade gracefully, it stops the plugin loading at all. Core is looked up in the chainloader
and registered with only when it is actually there. The registering itself sits in its own
method that is never inlined: the JIT resolves the assemblies a method needs the first time it
compiles that method, so a Core call sitting directly in `Awake` would drag the assembly in
before the check could prevent it, and the missing-assembly error would land during plugin
load, which is the exact failure the arrangement exists to avoid.

### Requiring it on a server

With Core installed, `RequireOnClients` decides whether a player without Tether can join at
all. On, which is the default, the server refuses them at the door with a message naming the
mod, rather than letting them into a game that quietly works differently for them. Off lets
them in, and anyone who does have it is still checked for a version or build mismatch.

That is a per-server judgement rather than a per-mod one, which is why it is a line in a file.
A server where the mod is part of how the place plays wants it on. Someone who added Tether for
themselves and does not want friends turned away over a convenience feature wants it off.
Neither is a safety question here.

Only a server can refuse anybody, so a client's copy of the setting governs nothing more than
what its own log complains about. It is read once at startup, so changing it wants a server
restart rather than a reconnect.

### The link

Look at a chest and press Keypad Plus. From a chest that ties it to everything you could work
at in range, in one press, since a chest standing in a work area is meant to serve the work
area. From a single bench or smelter it takes the nearest chest and leaves its neighbours
alone. Pressing again releases, and from a chest that only counts as a release when everything
in range is already on it, so a press at a half-tethered cluster finishes it rather than
undoing half of it.

Either direction is one step, with no selecting a partner first, because the thing you are
standing at is never ambiguous in practice. It is the same press Thralls uses for a drop-off
chest.

The key is Keypad Plus and not Keypad 7. Thralls holds Keypad 0 through 6, and 7 turned out to
fire its time-of-day tool on the same press.

### The rule is the storage

The link lives on the station rather than the chest, and a station has one slot. So one chest
per station is the shape of what is stored rather than something the mod has to police, and
tethering a second chest replaces the first. A chest serving several stations is a different
question and is allowed.

The mod this replaces pulls from every container in range, which turns a base into a single
inventory and removes any reason to organise anything. This saves the walk without abolishing
storage as a concern.

### Feeding a smelter

A chest tethered to a smelter, charcoal kiln, blast furnace, windmill or spinning wheel
supplies it when you add fuel or ore. Empty handed it offers whatever the chest holds that the
station accepts.

Deliberately not automation. A hopper that feeds itself removes the fuel system from the game.
This removes the carrying and not the deciding: you still walk over and you still press the
button. One press draws three, matching the batch a hold-Use press works with, and anything
spare stays in your pack.

The whole of it is a prefix that moves items into your pack a moment before the game looks for
them. Everything after that is the game's own capacity check, messages and effects, which is
what should keep it in step through an update.

### Correctness

Every requirement check and every consumption in Valheim funnels through `Inventory.CountItems`
and `Inventory.RemoveItem`, so patching those two is the whole mod. They are also called
constantly by everything else, so they do nothing unless one of the three crafting entry points
has opened a scope. Without that, chest contents bleed into carry weight, item counts and the
hotbar.

Consumption trims what the game removes. The shortfall comes out of the chest first, and the
amount is then cut to what you are actually carrying. Without that trim `RemoveItem` stops when
it runs out and the recipe is built for less than it cost.

Counting the shortfall has to hold the counting patch off, because that sum runs inside an open
scope where the count already includes the chest. Asking there reports nothing missing and
takes nothing.

Ownership of a chest is claimed before it is edited, since writing to a shared object you do
not own is a change that may simply be discarded.

The two private fields the mod reflects on are checked at startup and named in an error if a
game update ever removes them, rather than surfacing later as an unexplained null reference.

It loads on a dedicated server and declares itself to Core's version gate.

### Known limits

Untested in a real session. It builds, loads and passes the gate, and that is all that has been
established. The feature is complete as described; what is missing is evidence that it behaves.

Recipes taking any one of several ingredients resolve through a path that hands back an item
rather than a count, and are not covered. The have and need numbers in the crafting panel are
drawn by the game's own interface and are not patched, so a recipe can craft while its
requirement line reads short. A link remembers a chest by position, so moving a chest more than
a couple of metres breaks it quietly. Output is not collected, on purpose.
