using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KSP.UI.Screens;
using UnityEngine;

namespace KSPMenuCleaner
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class MenuCleanerBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[CleanKSPMenu] Bootstrapping Harmony patch...");
            var harmony = new Harmony("com.custom.kspmenucleaner");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Start")]
    public static class Patch_MainMenu_Start
    {
        // Exact GameObject names of the main menu (stage 1) items to hide,
        // as confirmed by the hierarchy dump.
        private static readonly string[] namesToHide =
        {
            "Community",
            "Addons",
            "Credits",
            "Merch"
        };

        // Shifts the whole menu group down, in multiples of one row height
        // (0.5 = half a row down, 1 = a full row, negative = up)
        private static readonly float groupOffsetRows = 2f;

        [HarmonyPostfix]
        public static void Postfix(MainMenu __instance)
        {
            Transform stage1 = __instance.transform.Find("stage 1");
            if (stage1 == null)
            {
                Debug.LogWarning("[CleanKSPMenu] 'stage 1' not found, aborting cleanup.");
                return;
            }

            // Hide unwanted items by exact name
            foreach (string name in namesToHide)
            {
                Transform item = stage1.Find(name);
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    Debug.Log($"[CleanKSPMenu] Hid menu item: {name}");
                }
            }

            ReflowMenu(stage1);
        }

        // Re-spaces the remaining visible items. The top item (Start Game)
        // keeps its original position; items below move up to close the gaps.
        private static void ReflowMenu(Transform stage1)
        {
            // Menu items are direct children of stage 1 that carry a text
            // label (TMP or legacy Text). Non-item children (logo, cameras)
            // have none. Inactive items are included here because we need
            // the original positions to compute the spacing.
            var items = stage1.Cast<Transform>()
                .Select(c => c as RectTransform)
                .Where(rt => rt != null &&
                             (rt.GetComponent<TMPro.TMP_Text>() != null ||
                              rt.GetComponent<UnityEngine.UI.Text>() != null))
                .OrderByDescending(rt => rt.anchoredPosition.y)
                .ToList();

            if (items.Count < 2)
            {
                Debug.LogWarning($"[CleanKSPMenu] Reflow skipped: found only {items.Count} item(s) under 'stage 1'.");
                return;
            }

            // Original uniform spacing = smallest gap between adjacent items
            float spacing = float.MaxValue;
            for (int i = 0; i < items.Count - 1; i++)
            {
                float gap = items[i].anchoredPosition.y - items[i + 1].anchoredPosition.y;
                if (gap > 0.01f && gap < spacing)
                    spacing = gap;
            }
            if (spacing == float.MaxValue)
                return;

            // Anchor on the top-most visible item and stack downward
            var visible = items.Where(rt => rt.gameObject.activeSelf).ToList();

            float topY = visible[0].anchoredPosition.y - spacing * groupOffsetRows;
            for (int i = 0; i < visible.Count; i++)
            {
                Vector2 pos = visible[i].anchoredPosition;
                pos.y = topY - spacing * i;
                visible[i].anchoredPosition = pos;
            }

            Debug.Log($"[CleanKSPMenu] Reflowed {visible.Count} menu items (spacing {spacing}).");
        }
    }
}
