using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Tether
{
    /// <summary>
    /// Which chest belongs to which station.
    ///
    /// The link is stored on the *station's* ZDO, not the chest's, and that is what makes
    /// the one-chest-per-station rule structural rather than something to enforce: a
    /// station holds one value, so tethering a second chest replaces the first. A chest is
    /// free to serve any number of stations, which is a different question and deliberately
    /// allowed - that is the one-to-many part.
    ///
    /// Targets are GameObjects rather than CraftingStations because a smelter is not a
    /// crafting station. Everything here needs from a target is a ZNetView and a position,
    /// both of which live on the GameObject, so widening the type was enough to let kilns
    /// and smelters be tethered on the same footing as a workbench.
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

        public static void Link(GameObject target, Container container)
        {
            var zdo = Zdo(target);
            if (zdo == null) return;

            zdo.Set(ZChest, container.transform.position);
            zdo.Set(ZHasChest, true);
        }

        public static void Unlink(GameObject target)
        {
            var zdo = Zdo(target);
            if (zdo == null) return;

            zdo.Set(ZHasChest, false);
        }

        public static bool TryGetChest(GameObject target, out Container container)
        {
            container = null;

            var zdo = Zdo(target);
            if (zdo == null || !zdo.GetBool(ZHasChest, false)) return false;

            container = FindContainer(zdo.GetVec3(ZChest, Vector3.zero));
            return container != null;
        }

        // ------------------------------------------------------------------ finding

        /// <summary>
        /// The target a tether would attach to: the nearest thing you could work at.
        ///
        /// Two different notions of range, because the game has two. A crafting station
        /// carries its own m_rangeBuild and that is the honest answer for benches. A smelter
        /// has no such field, so it gets a configured radius instead.
        /// </summary>
        public static GameObject NearestTarget(Vector3 point)
        {
            GameObject best = null;
            var bestDistance = float.MaxValue;

            var stations = AllStations.GetValue(null) as List<CraftingStation>;
            if (stations != null)
            {
                foreach (var station in stations)
                {
                    if (station == null) continue;

                    var distance = Vector3.Distance(station.transform.position, point);
                    if (distance > station.m_rangeBuild || distance >= bestDistance) continue;

                    best = station.gameObject;
                    bestDistance = distance;
                }
            }

            var range = TetherConfig.SmelterRange.Value;
            var count = Physics.OverlapSphereNonAlloc(point, range, Hits);

            for (var i = 0; i < count; i++)
            {
                var smelter = Hits[i].GetComponentInParent<Smelter>();
                if (smelter == null) continue;

                var distance = Vector3.Distance(smelter.transform.position, point);
                if (distance > range || distance >= bestDistance) continue;

                best = smelter.gameObject;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>Every target in reach, so one chest can be offered to all of them.</summary>
        public static void TargetsNear(Vector3 point, List<GameObject> into)
        {
            into.Clear();

            var stations = AllStations.GetValue(null) as List<CraftingStation>;
            if (stations != null)
            {
                foreach (var station in stations)
                {
                    if (station == null) continue;
                    if (Vector3.Distance(station.transform.position, point) > station.m_rangeBuild) continue;
                    if (!into.Contains(station.gameObject)) into.Add(station.gameObject);
                }
            }

            var range = TetherConfig.SmelterRange.Value;
            var count = Physics.OverlapSphereNonAlloc(point, range, Hits);

            for (var i = 0; i < count; i++)
            {
                var smelter = Hits[i].GetComponentInParent<Smelter>();
                if (smelter == null) continue;
                if (Vector3.Distance(smelter.transform.position, point) > range) continue;
                if (!into.Contains(smelter.gameObject)) into.Add(smelter.gameObject);
            }
        }

        public static Container NearestContainer(Vector3 point, float range)
        {
            var count = Physics.OverlapSphereNonAlloc(point, range, Hits);

            Container best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var container = Hits[i].GetComponentInParent<Container>();
                if (container == null) continue;

                var nview = container.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                var distance = Vector3.Distance(container.transform.position, point);
                if (distance >= bestDistance) continue;

                best = container;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>What to call a target in a message - its own name, not the prefab's.</summary>
        public static string NameOf(GameObject target)
        {
            if (target == null) return "";

            var station = target.GetComponent<CraftingStation>();
            if (station != null) return station.m_name;

            var smelter = target.GetComponent<Smelter>();
            if (smelter != null) return smelter.m_name;

            return target.name;
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
                if (!TryGetChest(station.gameObject, out var container)) continue;
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

        private static ZDO Zdo(GameObject target)
        {
            if (target == null) return null;

            var nview = target.GetComponent<ZNetView>();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }
    }
}
