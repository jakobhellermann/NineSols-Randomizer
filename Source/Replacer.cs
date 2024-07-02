using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NineSolsAPI.Utils;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;
using URandom = UnityEngine.Random;

namespace Randomizer;

internal interface IEnemyReplacementLogic {
    GameObject? Replace(MonsterBase monsterBase, Dictionary<string, Dictionary<string, GameObject>> preloads);
}

internal class RandomOnceEnemyReplacementLogic : IEnemyReplacementLogic {
    private Dictionary<string, (string, string)> replacements;

    public RandomOnceEnemyReplacementLogic(EnemyDatabase enemyDatabase) {
        var groups = enemyDatabase.Monsters
            .GroupBy(group => (group.Boss, group.Flying), x => x);

        replacements = new Dictionary<string, (string scene, string name)>();


        foreach (var group in groups) {
            var list = group.ToList();
            var shuffled = new List<MonsterData>(enemyDatabase.Monsters);
            shuffled.Shuffle(1234);

            foreach (var (from, to) in list.Zip(shuffled, (a, b) => (a, b)))
                replacements.Add(from.Name, (to.Scene, to.Name));
        }
    }

    public GameObject? Replace(MonsterBase monsterBase, Dictionary<string, Dictionary<string, GameObject>> preloads) {
        var (replacementScene, replacementName) = replacements[Replacer.MonsterName(monsterBase.name).ToString()];
        return preloads[replacementScene][replacementName];
    }
}

internal class RandomEachEnemyReplacementLogic : IEnemyReplacementLogic {
    public GameObject Replace(MonsterBase monsterBase, Dictionary<string, Dictionary<string, GameObject>> preloads) {
        var sceneObjects = preloads.Values.ElementAt(URandom.Range(0, preloads.Count));
        return sceneObjects.ElementAt(URandom.Range(0, sceneObjects.Count)).Value;
    }
}

public class Replacer {
    private EnemyDatabase enemyDatabase = new();


    public static ReadOnlySpan<char> MonsterName(string objectName) {
        var baseName = objectName.TrimEndMatches("(Clone)");

        var idx = baseName.LastIndexOf(" (");
        return idx == -1 ? baseName : baseName[..idx];
    }

    public void Replace(MonsterBase insteadOf) {
        var preloads = Randomizer.Instance.MonsterPreloads;
        if (preloads is null) {
            Log.Warning("Preloads not loaded, enemies are not replaced");
            return;
        }

        try {
            var newObject = enemyReplacementLogic.Replace(insteadOf, preloads);
            if (newObject is null) return;
            SpawnMonster(newObject, insteadOf.gameObject);
            Object.Destroy(insteadOf.gameObject);
        } catch (Exception e) {
            Log.Error($"error while replacing {MonsterName(insteadOf.gameObject.name).ToString()}: {e}");
        }
    }

    private AccessTools.FieldRef<GuidComponent, Guid> guidComponentGuid =
        AccessTools.FieldRefAccess<GuidComponent, Guid>("guid");

    private AccessTools.FieldRef<GuidComponent, byte[]?> guidComponentSerializedGuid =
        AccessTools.FieldRefAccess<GuidComponent, byte[]?>("serializedGuid");


    private IEnemyReplacementLogic enemyReplacementLogic;

    public Replacer() {
        enemyReplacementLogic = new RandomOnceEnemyReplacementLogic(enemyDatabase);
    }


    private void SpawnMonster(GameObject preload, GameObject insteadOf) {
        SpawnMonster(preload, insteadOf.transform.parent, insteadOf.transform.position);
    }

    public GameObject SpawnMonster(GameObject preload, Transform? parent, Vector3 position) {
        preload.AddComponent<IsReplaced>();
        var copy = ObjectUtils.InstantiateAutoReference(preload, parent)
            .GetComponent<MonsterBase>();
        copy.gameObject.SetActive(true);
        copy.transform.position = position;

        // new guid
        var guidComponent = copy.GetComponent<GuidComponent>();
        var g = Guid.NewGuid();
        guidComponentGuid.Invoke(guidComponent) = g;
        guidComponentSerializedGuid.Invoke(guidComponent) = g.ToByteArray();

        // initialize
        copy.EnterLevelAwake();
        var list = copy.GetComponentsInChildren<IResetter>(true);
        foreach (var resetter in list.Reverse()) resetter.EnterLevelReset();

        return copy.gameObject;
    }
}

internal class IsReplaced : MonoBehaviour {
}

internal static class Extensions {
    public static void Shuffle<T>(this IList<T> list, int seed) {
        var rng = new Random(seed);
        var n = list.Count;

        while (n > 1) {
            n--;
            var k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static ReadOnlySpan<char> TrimEndMatches(this string str, ReadOnlySpan<char> substr) {
        var span = str.AsSpan();
        var idx = span.LastIndexOf(substr);
        return idx == -1 ? span : span[..idx];
    }
}