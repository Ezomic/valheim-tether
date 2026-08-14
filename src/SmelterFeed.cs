using HarmonyLib;
using UnityEngine;

namespace Tether
{
    /// <summary>
    /// Lets a tethered chest supply the smelter it is tied to - but only when you are
    /// standing there pressing the button.
    ///
    /// This is deliberately not automation. A hopper that feeds itself removes the fuel
    /// system from the game: you stop thinking about coal, and the mid-game logistics
    /// problem quietly stops existing. What this removes instead is the *hauling* - you
    /// still walk to the smelter and you still press the key, you just do not have to
    /// carry the coal there from the chest six feet away.
    ///
    /// The whole implementation is a prefix that moves items chest to pack a moment before
    /// the game looks for them. Everything after that is vanilla: vanilla's capacity check,
    /// vanilla's messages, vanilla's effects. Nothing here reimplements adding fuel, which
    /// is what keeps it from drifting out of step with the game.
    /// </summary>
    internal static class SmelterFeed
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        private static void FeedFuel(Smelter __instance, Humanoid user)
        {
            if (__instance == null || __instance.m_fuelItem == null) return;
            TopUp(__instance, user, __instance.m_fuelItem.m_itemData.m_shared.m_name);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        private static void FeedOre(Smelter __instance, Humanoid user, ItemDrop.ItemData item)
        {
            if (__instance == null) return;

            // With an item in hand the game is being told exactly what to add, so only that
            // is worth fetching. Empty-handed it picks for itself, and the chest is asked
            // for anything the station will accept.
            if (item != null)
            {
                TopUp(__instance, user, item.m_shared.m_name);
                return;
            }

            foreach (var allowed in __instance.m_conversion)
            {
                if (allowed == null || allowed.m_from == null) continue;
                if (TopUp(__instance, user, allowed.m_from.m_itemData.m_shared.m_name)) return;
            }
        }

        /// <summary>
        /// Moves enough of one item from the tethered chest into the player's pack for the
        /// press about to happen. Returns true if anything moved.
        /// </summary>
        private static bool TopUp(Smelter smelter, Humanoid user, string itemName)
        {
            if (!TetherConfig.Enabled.Value || !TetherConfig.FeedSmelters.Value) return false;
            if (user == null || string.IsNullOrEmpty(itemName)) return false;
            if (!TetherLinks.Ready) return false;

            var inventory = user.GetInventory();
            if (inventory == null) return false;

            var wanted = Mathf.Max(1, TetherConfig.PullAmount.Value);
            var carried = inventory.CountItems(itemName);
            if (carried >= wanted) return false;

            Container chest;
            if (!TetherLinks.TryGetChest(smelter.gameObject, out chest)) return false;

            var store = chest.GetInventory();
            if (store == null) return false;

            var available = store.CountItems(itemName);
            if (available <= 0) return false;

            var take = Mathf.Min(available, wanted - carried);

            // Ownership first. Editing a shared container you do not own is a change that
            // may simply be overwritten by whoever does, which would duplicate the items.
            var nview = chest.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid()) nview.ClaimOwnership();

            var moved = 0;
            for (var i = 0; i < take; i++)
            {
                var stack = store.GetItem(itemName);
                if (stack == null) break;

                // One at a time through the game's own add, so a full pack stops the
                // transfer rather than deleting what would not fit.
                if (inventory.AddItem(stack.m_dropPrefab != null ? stack.m_dropPrefab.name : itemName,
                                      1, stack.m_quality, stack.m_variant,
                                      stack.m_crafterID, stack.m_crafterName) == null)
                    break;

                store.RemoveItem(itemName, 1);
                moved++;
            }

            if (moved == 0) return false;

            if (TetherConfig.Verbose.Value)
                TetherPlugin.Log.LogInfo(
                    "Drew " + moved + " " + itemName + " from the tethered chest.");

            return true;
        }
    }
}
