using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;
using UnityEngine;

namespace Tether
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Tether installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. The link is stored on the station's own ZDO, which the server owns
    // whenever no client is near it, so the server needs to know the mod exists even though
    // every decision here is made client-side.
    public class TetherPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.tether";
        public const string PluginName = "Tether";
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private bool _held;

        private void Awake()
        {
            Log = Logger;
            TetherConfig.Bind(Config);
            TryRegisterWithCore();

            TetherLinks.Verify();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(CraftingPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Tether is worth installing on its own, and a hard dependency that is absent does
        /// not degrade gracefully - the plugin never loads at all. So the reference is
        /// compile-time only and the call is made behind a check.
        ///
        /// What is given up standing alone is the gate, not the mod.
        /// Nothing refuses a client that lacks Tether, so two ends can disagree about what is in
        /// reach with nothing to say so.
        /// </summary>
        private void TryRegisterWithCore()
        {
            if (!Chainloader.PluginInfos.ContainsKey(CoreGuid))
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a method
        /// needs when it first compiles that method, so a Suite call sitting directly in Awake
        /// would drag Ezomic.Core in before the check above could prevent it - and the
        /// missing-assembly exception would land during plugin load, which is the failure this
        /// whole arrangement exists to avoid. Isolating it means the type is only ever resolved
        /// on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // Which of the two the gate gets is the server operator's call, not this mod's,
            // because the honest answer differs per server rather than per mod.
            //
            // Everyone refuses anybody who does not have Tether. Nothing here needs that for
            // safety - the failure mode Everyone exists for is a mod registering a prefab or
            // altering item data, where a client without it discards ZDOs it cannot resolve
            // and destroys what is standing in the world. Tether registers neither, and its
            // links are ordinary ZDO values on vanilla stations that a vanilla client stores
            // and ignores. So a mixed party is merely a party where some people do not have
            // the feature.
            //
            // It is still the right setting for a server where this is part of how the place
            // plays, which is the case it defaults to. What it buys is that nobody is quietly
            // playing a different game: the host's settings are in force for everyone, and a
            // build mismatch is caught rather than surfacing as one player's chest not
            // working. What it costs is a friend turned away over a convenience feature,
            // which is why it is a line in a file rather than a decision made here.
            //
            // Reading the config here rather than in Awake is deliberate: this method is the
            // one that may never run, and naming a Core type in Awake would resolve the
            // assembly before the installed check could prevent it.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config,
                TetherConfig.RequireOnClients.Value
                    ? Requirement.Everyone
                    : Requirement.HostOnly);
        }


        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            var player = Player.m_localPlayer;
            if (player == null || !TetherLinks.Ready) return;

            // Edge-triggered by hand: a held key would otherwise tether and release the
            // same chest several times a second.
            var down = TetherConfig.KeyTether.Value.IsDown();
            if (!down) { _held = false; return; }
            if (_held) return;
            _held = true;

            // Player.TakeInput is protected, so the two windows that actually matter are
            // checked directly: typing in a chest or the menu should not retether anything.
            if (InventoryGui.IsVisible() || Menu.IsVisible()) return;

            ToggleTether(player);
        }

        /// <summary>
        /// Press the key at either end of the link and the right thing happens.
        ///
        /// Which end you are looking at changes what the press means, because the useful
        /// answer is different from each side. From a chest you almost always want it
        /// serving everything around you, so that ties it to every station in range at once
        /// - the one-to-many case. From a single station you want just that one, so it takes
        /// the nearest chest and leaves its neighbours alone.
        ///
        /// One step rather than two either way - no selecting a partner first - because the
        /// thing you are standing at is never ambiguous in practice. It is also the same
        /// verb Thralls already uses for setting a drop-off chest, so it needs no learning.
        /// </summary>
        private static void ToggleTether(Player player)
        {
            var hovering = TetherLinks.Hovering(player);
            if (hovering == null) return;

            var container = hovering.GetComponentInParent<Container>();
            if (container != null) { ToggleFromChest(container); return; }

            // A smelter is not a CraftingStation, so both have to be asked for by name.
            var station = hovering.GetComponentInParent<CraftingStation>();
            var smelter = hovering.GetComponentInParent<Smelter>();

            var target = station != null ? station.gameObject
                       : smelter != null ? smelter.gameObject
                       : null;

            if (target != null) ToggleFromStation(target);
        }

        /// <summary>
        /// Ties one chest to everything in reach, or releases the lot.
        ///
        /// It only counts as a release when *every* target in range is already on this
        /// chest. Otherwise the press links them all - so walking up to a half-tethered
        /// cluster and pressing once finishes the job rather than undoing half of it.
        /// </summary>
        private static void ToggleFromChest(Container container)
        {
            var targets = new List<GameObject>();
            TetherLinks.TargetsNear(container.transform.position, targets);

            if (targets.Count == 0)
            {
                Announce("Nothing in range of that chest to tether it to.");
                return;
            }

            var allLinked = true;
            foreach (var target in targets)
            {
                if (TetherLinks.TryGetChest(target, out var current) && current == container) continue;

                allLinked = false;
                break;
            }

            foreach (var target in targets)
            {
                if (allLinked) TetherLinks.Unlink(target);
                else TetherLinks.Link(target, container);
            }

            Announce(allLinked
                ? targets.Count + " released from " + container.m_name + "."
                : targets.Count + " tethered to " + container.m_name + ".");
        }

        /// <summary>The other direction: this one station, and the chest nearest to it.</summary>
        private static void ToggleFromStation(GameObject target)
        {
            var name = TetherLinks.NameOf(target);

            if (TetherLinks.TryGetChest(target, out var current))
            {
                TetherLinks.Unlink(target);
                Announce(name + " released from " + current.m_name + ".");
                return;
            }

            var container = TetherLinks.NearestContainer(target.transform.position,
                                                         TetherConfig.SmelterRange.Value);
            if (container == null)
            {
                Announce("No chest in range of " + name + ".");
                return;
            }

            TetherLinks.Link(target, container);
            Announce(name + " tethered to " + container.m_name + ".");
        }

        private static void Announce(string message)
        {
            if (!TetherConfig.Messages.Value || Player.m_localPlayer == null) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(message), 0, null);
        }
    }
}
