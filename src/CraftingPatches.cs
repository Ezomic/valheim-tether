using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Tether
{
    /// <summary>
    /// Makes a tethered chest count as part of your inventory, but only while the game is
    /// asking a crafting question.
    ///
    /// Every requirement check and every consumption in Valheim funnels through
    /// Inventory.CountItems and Inventory.RemoveItem on the player's own inventory. Those
    /// are also called constantly by everything else, so patching them unconditionally
    /// would have chests bleeding into inventory weight, item counts and the hotbar.
    ///
    /// Hence the scope flag: the requirement and consume methods bracket themselves, and
    /// the two Inventory patches do nothing unless a bracket is open.
    /// </summary>
    internal static class CraftingPatches
    {
        private static int _depth;
        private static bool _suspend;

        private static readonly List<Container> Reachable = new List<Container>();

        private static bool InScope => _depth > 0 && !_suspend && TetherConfig.Enabled.Value;

        private static void Open() { _depth++; }
        private static void Close() { if (_depth > 0) _depth--; }

        private static bool IsPlayerInventory(Inventory inventory)
        {
            var player = Player.m_localPlayer;
            return player != null && ReferenceEquals(player.GetInventory(), inventory);
        }

        // ------------------------------------------------------------------ scope brackets

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static void ScopeRecipeStart() { Open(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static void ScopeRecipeEnd() { Close(); }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        private static void ScopePieceStart() { Open(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        private static void ScopePieceEnd() { Close(); }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static void ScopeConsumeStart() { Open(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static void ScopeConsumeEnd() { Close(); }

        // ------------------------------------------------------------------ counting

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        private static void CountTetheredItems(Inventory __instance, ref int __result,
            string name, int quality)
        {
            if (!InScope || !TetherLinks.Ready || !IsPlayerInventory(__instance)) return;

            var player = Player.m_localPlayer;
            TetherLinks.CollectReachable(player.transform.position, Reachable);

            foreach (var container in Reachable)
            {
                var inventory = container.GetInventory();
                // Not the player's inventory, so this does not re-enter the patch.
                if (inventory != null) __result += inventory.CountItems(name, quality);
            }
        }

        // ------------------------------------------------------------------ consuming

        /// <summary>
        /// Takes the shortfall out of the tethered chests before the game removes what it
        /// can from the player, then trims the amount so vanilla only removes what is
        /// actually there. Without the trim, RemoveItem would quietly stop when it ran out
        /// and the recipe would be built for less than it cost.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem),
            typeof(string), typeof(int), typeof(int), typeof(bool))]
        private static void TakeFromTetheredChests(Inventory __instance, string name,
            ref int amount, int itemQuality)
        {
            if (!InScope || !TetherLinks.Ready || !IsPlayerInventory(__instance)) return;
            if (amount <= 0) return;

            var carried = PlayerOnlyCount(__instance, name, itemQuality);
            var shortfall = amount - carried;
            if (shortfall <= 0) return;

            var player = Player.m_localPlayer;
            TetherLinks.CollectReachable(player.transform.position, Reachable);

            foreach (var container in Reachable)
            {
                if (shortfall <= 0) break;

                var inventory = container.GetInventory();
                if (inventory == null) continue;

                var available = inventory.CountItems(name, itemQuality);
                if (available <= 0) continue;

                var take = Mathf.Min(available, shortfall);

                // Chests are shared objects; editing one you do not own is a change that
                // may simply be overwritten by whoever does.
                var nview = container.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid()) nview.ClaimOwnership();

                // No explicit save: RemoveItem ends in Changed(), and Container wires that
                // to its own OnContainerChanged, which persists the inventory.
                inventory.RemoveItem(name, take, itemQuality);

                shortfall -= take;

                if (TetherConfig.Verbose.Value)
                    TetherPlugin.Log.LogInfo("Took " + take + " " + name + " from a tethered chest.");
            }

            // What vanilla should still take is everything the chests did not cover, which
            // works out to the carried stock plus any shortfall they could not meet. When
            // the chests covered it all that is exactly the carried stock; when they had
            // nothing it is the original amount, unchanged.
            amount = carried + shortfall;
        }

        /// <summary>
        /// The genuine carried count, with the counting patch held off.
        ///
        /// Needed because the shortfall calculation runs inside an open scope, where
        /// CountItems already answers with the chests included - asking there would report
        /// that nothing is missing and take nothing from the chest.
        /// </summary>
        private static int PlayerOnlyCount(Inventory inventory, string name, int quality)
        {
            _suspend = true;
            try { return inventory.CountItems(name, quality); }
            finally { _suspend = false; }
        }
    }
}
