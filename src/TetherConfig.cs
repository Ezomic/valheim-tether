using BepInEx.Configuration;
using UnityEngine;

namespace Tether
{
    internal static class TetherConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> KeyTether;
        public static ConfigEntry<bool> Messages;
        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Tether", "Enabled", true,
                "Let crafting draw from tethered chests. Turning this off leaves existing "
                + "links intact but ignores them.");

            // Numpad 0-6 are already spoken for by Thralls; 7 keeps the two mods from
            // fighting over the same press.
            KeyTether = config.Bind("Tether", "KeyTether",
                new KeyboardShortcut(KeyCode.Keypad7),
                "Look at a chest and press this to tether it to the nearest bench. Press "
                + "again on a tethered chest to release it.");

            Messages = config.Bind("Tether", "Messages", true,
                "Corner messages when a chest is tethered or released.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every withdrawal made from a tethered chest.");
        }
    }
}
