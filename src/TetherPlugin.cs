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
        public const string PluginGuid = "robbin.valheim.tether";
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
        /// Look at a chest, press the key, and it belongs to the nearest bench. Press again
        /// and it does not.
        ///
        /// One step rather than two - no selecting a bench first - because the bench is
        /// never ambiguous in practice: you are standing at it. It is also the same verb
        /// Thralls already uses for setting a drop-off chest, so it needs no learning.
        /// </summary>
        private static void ToggleTether(Player player)
        {
            var hovering = TetherLinks.Hovering(player);
            if (hovering == null) return;

            var container = hovering.GetComponentInParent<Container>();
            if (container == null) return;

            var station = TetherLinks.NearestStation(container.transform.position);
            if (station == null)
            {
                Announce("No bench in range of that chest.");
                return;
            }

            if (TetherLinks.TryGetChest(station, out var current) && current == container)
            {
                TetherLinks.Unlink(station);
                Announce(container.m_name + " released from " + station.m_name + ".");
                return;
            }

            TetherLinks.Link(station, container);
            Announce(container.m_name + " tethered to " + station.m_name + ".");
        }

        private static void Announce(string message)
        {
            if (!TetherConfig.Messages.Value || Player.m_localPlayer == null) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(message), 0, null);
        }
    }
}
