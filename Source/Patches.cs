using HarmonyLib;

namespace Randomizer;

// ReSharper disable once InconsistentNaming
[HarmonyPatch]
public class Patches {
    private const bool EnemyRandomizer = false;


    [HarmonyPatch(typeof(MonsterBase), "Awake")]
    [HarmonyPrefix]
    private static void MonsterBaseAwake(ref MonsterBase __instance) {
        if (!EnemyRandomizer) return;

        if (__instance.GetComponent<IsReplaced>() != null) return;

        var randomizer = Randomizer.Instance;
        randomizer.Replacer.Replace(__instance);
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.EnterLevelAwake))]
    [HarmonyPrefix]
    private static bool MonsterBaseEnterLevelAwake(ref MonsterBase __instance) {
        var isReplaced = __instance.GetComponent<IsReplaced>() != null;
        return isReplaced;
    }
}