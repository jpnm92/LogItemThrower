using HarmonyLib;
using UnityEngine;

namespace LogItemThrower
{
    public class LogHoverText : MonoBehaviour, Hoverable
    {
        public string GetHoverText()
        {
            if (Player.m_localPlayer == null) return "";
            float dist = Vector3.Distance(Player.m_localPlayer.transform.position, transform.position);
            if (dist > LogItemThrower.GrabRange.Value) return "";
            string key = LogItemThrower.LaunchHotkey.Value.MainKey.ToString();
            return $"Log\n[<color=yellow>{key}</color>] Grab";
        }

        public string GetHoverName() => "Log";
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    public static class ZNetSceneAwakePatch
    {
        static void Postfix(ZNetScene __instance)
        {
            foreach (GameObject prefab in __instance.m_prefabs)
            {
                if (prefab.GetComponent<TreeLog>() != null &&
                    prefab.GetComponent<LogHoverText>() == null)
                    prefab.AddComponent<LogHoverText>();
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText))]
    public static class ItemDropHoverPatch
    {
        static void Postfix(ItemDrop __instance, ref string __result)
        {
            if (Player.m_localPlayer == null) return;
            Rigidbody rb = __instance.GetComponent<Rigidbody>();
            if (rb == null) return;
            float dist = Vector3.Distance(Player.m_localPlayer.transform.position, rb.position);
            if (dist > LogItemThrower.GrabRange.Value) return;
            string key = LogItemThrower.LaunchHotkey.Value.MainKey.ToString();
            __result += $"\n[{key}] Grab";
        }
    }
}