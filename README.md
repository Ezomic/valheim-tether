# Tether

One chest, one bench. Crafting at that bench can reach into that chest.

## Installing

Needs BepInEx and nothing else. By hand, put `Tether.dll` in `BepInEx/plugins/Tether/`.

[Longhouse Core](https://github.com/Ezomic/valheim-core) is optional. Installed, it is used;
absent, nothing here works any less well. It matters only on a server, and only for the one
thing described under Multiplayer.

Start the game once and quit if you want the config file to edit. It does not exist until the
mod has loaded once, which is the usual reason people think a setting is missing.

## Using it

Look at a chest and press Keypad Plus.

Which end you are looking at changes what the press means, because the useful answer is
different from each side. From a chest it ties that chest to everything you could work at
around it, every bench, forge, smelter and kiln in range, in one press. A chest standing in
the middle of a work area is usually meant to serve the work area. From a single bench or
smelter it ties that one station to the nearest chest and leaves its neighbours alone.

Press again to release. From a chest it only counts as a release when everything in range is
already on that chest, so walking up to a half-tethered cluster and pressing once finishes the
job rather than undoing half of it.

There is no selecting a partner first, in either direction. The thing you are standing at is
never ambiguous in practice. It is also the same press Thralls uses for setting a drop-off
chest, so there is nothing new to learn.

The key is Keypad Plus rather than a number because Thralls already holds Keypad 0 through 6,
and Keypad 7 fired its time-of-day tool on the same press. Change it in the config if you want
it somewhere else.

## What each station remembers

Each station holds exactly one chest. Tethering a second one replaces the first without
asking. That is not a rule the mod enforces, it is the shape of what it stores: the link lives
on the station, and a station has one slot.

A chest may serve any number of stations. Standing between a workbench and a forge gives you
both their chests at once, and each of them still has exactly one.

## Crafting

While you are crafting, upgrading or building, anything in a tethered chest counts as
something you are carrying. The requirement counts include it and building takes it out of the
chest. The stations consulted are the ones whose own build range covers where you are
standing, so it follows the same reach the game already uses to decide whether a bench is
close enough to work at.

Outside those moments nothing changes. A tethered chest adds nothing to your carry weight,
does not show up in your item counts and does not feed your hotbar. Most of the care in this
mod goes into when it is listening rather than what it does.

A cost split across both ends works. If a recipe wants twenty wood and you have eight, the
chest covers the other twelve and the total taken is twenty.

## Feeding a smelter

A chest tethered to a smelter, charcoal kiln, blast furnace, windmill or spinning wheel can
supply it when you add fuel or ore. Walk up and press Use as normal, and the coal comes out of
the chest rather than your pack. Empty handed, the station takes whatever the chest has that it
accepts.

This is deliberately not automation. A hopper that feeds itself removes the fuel system from
the game: you stop thinking about coal and the mid-game logistics problem quietly stops
existing. What this takes away is the carrying, not the deciding. You still walk over and you
still press the button.

One press draws up to three, which is the batch a hold-Use press uses, so there is something
for it to work with. Anything spare stays in your pack, which is where material you took out
of a chest belongs. `FeedSmelters = false` turns this off and leaves the crafting side alone.

## Why only one chest

The mod this replaces pulls from every container in range. That quietly turns a base into a
single inventory, and once it has, there is no reason to organise anything, because everything
is already everywhere.

Tether links one chest to one bench. Your forge gets its metal chest, your workbench gets its
wood and leather chest, and keeping them stocked is still something you do. It saves the walk.
It does not abolish storage as a concern.

## Settings

`BepInEx/config/ezomic.valheim.tether.cfg`

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off ignores every link without forgetting any of them |
| `KeyTether` | `KeypadPlus` | Tether or release what you are looking at |
| `FeedSmelters` | `true` | Let a tethered chest supply the smelter or kiln it is tied to |
| `SmelterRange` | `6` | How close a chest has to be to a smelter to tether to it |
| `PullAmount` | `3` | How much one press draws out of the chest |
| `Messages` | `true` | Corner messages on tether and release |
| `Verbose` | `false` | Log every withdrawal from a tethered chest |
| `RequireOnClients` | `true` | Refuse players without Tether. Server side, and needs Core |

Benches use their own build range rather than `SmelterRange`, because a crafting station
carries that number itself and it is the honest answer for anything you can work at. A smelter
has no such field, which is what the setting is for.

## What it does not cover

Recipes that take any one of several ingredients resolve through a different path in the game,
one that hands back an item rather than a count. Those few recipes still see only your own
inventory.

The have and need numbers in the crafting panel are drawn by the game's own interface, which
is not patched. A recipe can craft successfully while its requirement line still reads short.

A link remembers a chest by where it stands. Pick a chest up and put it down again more than a
couple of metres from where it was and the link stops resolving, quietly. Tether it again.

Output is not collected. A smelter's iron still drops on the ground and you still pick it up.
The tether runs one way on purpose. Returning the product as well would close the loop and
leave the smelter running itself, which is the thing the one-chest rule exists to avoid.

## Multiplayer

Install it on the server as well as on the clients. Links live on the station and are shared,
so a chest one player tethers is tethered for everyone. Ownership of a chest is claimed before
anything is taken out of it, because writing to a shared object you do not own is a change
that may simply be discarded.

A mixed party is fine. Nothing here can damage a world for someone who does not have the mod:
Tether adds no prefabs and no items, and a link is an ordinary value on a vanilla station that
a vanilla client stores and ignores. Players without it simply do not have the feature.

If you would rather they could not join at all, that is what Core is for. With Core installed
on the server, `RequireOnClients` turns Tether into a requirement and a player without it is
refused at the door with a message naming the mod, rather than let in to a game that quietly
works differently for them. It is on by default, and does nothing without Core, since standing
alone there is no handshake to refuse anyone with. Core also catches the case a version number
cannot: same version on both ends, different build.

Core also makes the host's settings the ones that count for as long as you are connected. A
server setting `Enabled = false` turns the mod off for everybody on it, and your own file is
untouched and comes back when you leave.

## Status

Version 0.1. It builds and loads, standalone and with Core. It has not been playtested, which
is the only reason it is not 1.0.

## Building

```bash
dotnet build
```

Deploys to `testprofile/` by default. `own-profile/build-all.ps1` builds every mod in the
suite into the shared play profile instead.

## Author

Tether is an original mod by Robbin Thijssen (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed, see `LICENSE`.
