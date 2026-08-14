using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Tether
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public class TetherPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.tether";
        public const string PluginName = "Tether";
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private bool _held;

        private void Awake()
        {
            Log = Logger;
            TetherConfig.Bind(Config);

            TetherLinks.Verify();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(CraftingPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
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
