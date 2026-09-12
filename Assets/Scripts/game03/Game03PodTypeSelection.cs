using System.Collections.Generic;
using UnityEngine;

/// <summary>仲間 POD（武器02〜07）の抽選。フィールドアイテム取得とデバッグ POD で共通。</summary>
public static class Game03PodTypeSelection
{
    public static void CollectDeployedSubPodTypes(Game03WeaponManager weaponManager, RectTransform[] subUnitSlots, HashSet<int> output)
    {
        output.Clear();
        if (weaponManager == null || subUnitSlots == null)
        {
            return;
        }

        for (int si = 0; si < subUnitSlots.Length && si < 4; si++)
        {
            RectTransform rt = subUnitSlots[si];
            if (rt == null || !rt.gameObject.activeSelf)
            {
                continue;
            }

            if (weaponManager.TryGetEquippedWeaponForSlot(si + 1, out int w, out _) && w >= 2 && w <= 7)
            {
                output.Add(w);
            }
        }
    }

    public static bool TryDrawEligiblePodType(HashSet<int> deployed, out int podType)
    {
        podType = -1;
        var eligible = new List<int>(6);
        for (int p = 2; p <= 7; p++)
        {
            if (!deployed.Contains(p))
            {
                eligible.Add(p);
            }
        }

        if (eligible.Count == 0)
        {
            return false;
        }

        podType = eligible[Random.Range(0, eligible.Count)];
        return true;
    }
}
