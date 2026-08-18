using BepInEx.Configuration;
using UnityEngine;

namespace Tether
{
    internal static class TetherConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> KeyTether;
        public static ConfigEntry<bool> Messages;
        public static ConfigEntry<bool> FeedSmelters;
        public static ConfigEntry<float> SmelterRange;
        public static ConfigEntry<int> PullAmount;
        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Tether", "Enabled", true,
                "Let crafting draw from tethered chests. Turning this off leaves existing "
                + "links intact but ignores them.");

            // Not the numpad. Thralls owns the whole of it - 0, 1, 4, 7, 9 and minus - and
            // Keypad7 had this mod and Thralls' time-of-day tool firing on the same press.
            KeyTether = config.Bind("Tether", "KeyTether",
                new KeyboardShortcut(KeyCode.KeypadPlus),
                "Look at a chest and press this to tether it to the nearest bench. Press "
                + "again on a tethered chest to release it.");

            FeedSmelters = config.Bind("Tether", "FeedSmelters", true,
                "Let a tethered chest supply the smelter or kiln it is tied to when you "
                + "interact with it. Deliberately not automation: you still walk over and "
                + "still press the key, you just do not carry the coal there yourself.");

            SmelterRange = config.Bind("Tether", "SmelterRange", 6f,
                "How close a chest must be to a smelter to tether to it.");

            PullAmount = config.Bind("Tether", "PullAmount", 3,
                "How much a single press draws out of the chest. Matches the batch size so "
                + "a shift-press has something to work with; anything spare stays in your "
                + "pack, which is where material you took out of a chest belongs.");

            Messages = config.Bind("Tether", "Messages", true,
                "Corner messages when a chest is tethered or released.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every withdrawal made from a tethered chest.");
        }
    }
}
