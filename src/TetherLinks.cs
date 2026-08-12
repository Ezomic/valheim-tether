using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Tether
{
    /// <summary>
    /// Which chest belongs to which bench.
    ///
    /// The link is stored on the *station's* ZDO, not the chest's, and that is what makes
    /// the one-chest-per-bench rule structural rather than something to enforce: a station
    /// holds one value, so tethering a second chest replaces the first. A chest is free to
    /// serve more than one bench, which is a different question and deliberately allowed.
    ///
    /// The chest is remembered by position rather than by ZDOID because a position survives
    /// being looked up from either side, and a container that has been torn down simply
    /// fails to resolve - which is the behaviour wanted anyway.
    /// </summary>
    internal static class TetherLinks
    {
        private const string ZChest = "tetherChest";
        private const string ZHasChest = "tetherHasChest";

        /// <summary>Distance a remembered position may be off before it stops resolving.</summary>
        private const float ChestSearchRadius = 2f;

        private static readonly Collider[] Hits = new Collider[64];

        // CraftingStation keeps its own registry; there is no public way to ask for every
        // station near a point, only for the closest one of a given name.
        private static readonly System.Reflection.FieldInfo AllStations =
            AccessTools.Field(typeof(CraftingStation), "m_allStations");

        private static readonly System.Reflection.FieldInfo PlayerHovering =
            AccessTools.Field(typeof(Player), "m_hovering");

        public static bool Verify()
        {
            var missing = new List<string>();
            if (AllStations == null) missing.Add("CraftingStation.m_allStations");
            if (PlayerHovering == null) missing.Add("Player.m_hovering");

            if (missing.Count == 0) return true;

            TetherPlugin.Log.LogError(
                "Game members this mod reflects on are missing - Tether is disabled: "
                + string.Join(", ", missing.ToArray()));
            return false;
        }

        public static bool Ready => AllStations != null && PlayerHovering != null;

        // ------------------------------------------------------------------ linking

        public static void Link(CraftingStation station, Container container)
        {
            var zdo = Zdo(station);
            if (zdo == null) return;

            zdo.Set(ZChest, container.transform.position);
            zdo.Set(ZHasChest, true);
        }

        public static void Unlink(CraftingStation station)
        {
            var zdo = Zdo(station);
            if (zdo == null) return;

            zdo.Set(ZHasChest, false);
        }

        public static bool TryGetChest(CraftingStation station, out Container container)
        {
            container = null;

            var zdo = Zdo(station);
            if (zdo == null || !zdo.GetBool(ZHasChest, false)) return false;

            container = FindContainer(zdo.GetVec3(ZChest, Vector3.zero));
            return container != null;
        }

        /// <summary>The station a tether would attach to: the nearest one you can build at.</summary>
        public static CraftingStation NearestStation(Vector3 point)
        {
            var stations = AllStations.GetValue(null) as List<CraftingStation>;
            if (stations == null) return null;

            CraftingStation best = null;
            var bestDistance = float.MaxValue;

            foreach (var station in stations)
            {
                if (station == null) continue;

                var distance = Vector3.Distance(station.transform.position, point);
                if (distance > station.m_rangeBuild || distance >= bestDistance) continue;

                best = station;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>
        /// Every tethered chest reachable from where the player is standing.
        ///
        /// Gathered per station rather than per player, so standing between a workbench and
        /// a forge gives you both their chests - each bench still has exactly one.
        /// </summary>
        public static void CollectReachable(Vector3 point, List<Container> into)
        {
            into.Clear();

            var stations = AllStations.GetValue(null) as List<CraftingStation>;
            if (stations == null) return;

            foreach (var station in stations)
            {
                if (station == null) continue;
                if (Vector3.Distance(station.transform.position, point) > station.m_rangeBuild) continue;
                if (!TryGetChest(station, out var container)) continue;
                if (!into.Contains(container)) into.Add(container);
            }
        }

        // ------------------------------------------------------------------ helpers

        public static GameObject Hovering(Player player)
        {
            return PlayerHovering.GetValue(player) as GameObject;
        }

        public static Container FindContainer(Vector3 position)
        {
            var count = Physics.OverlapSphereNonAlloc(position, ChestSearchRadius, Hits);

            for (var i = 0; i < count; i++)
            {
                var container = Hits[i].GetComponentInParent<Container>();
                if (container == null) continue;

                var nview = container.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                if (Vector3.Distance(container.transform.position, position) <= ChestSearchRadius)
                    return container;
            }

            return null;
        }

        private static ZDO Zdo(CraftingStation station)
        {
            if (station == null) return null;

            var nview = station.GetComponent<ZNetView>();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }
    }
}
