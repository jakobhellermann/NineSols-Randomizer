using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Randomizer;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Randomizer : BaseUnityPlugin {
    internal static Randomizer Instance = null!;
    private Harmony? harmony;

    internal Replacer Replacer = new();

    internal Dictionary<string, Dictionary<string, GameObject>>? MonsterPreloads;

    private void Awake() {
        Instance = this;
        try {
            Log.Init(Logger);
            harmony = Harmony.CreateAndPatchAll(typeof(Randomizer).Assembly);
            // KeybindManager.Add(this, () => Test(false), KeyCode.P);
            // KeybindManager.Add(this, () => Test(true), KeyCode.O);
            // KeybindManager.Add(this, () => i++, KeyCode.I);

            // StartCoroutine(LoadPreload(preloads => { MonsterPreloads = preloads; }));
        } catch (Exception e) {
            Log.Error(e);
        }


        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy() {
        harmony?.UnpatchSelf();
    }


    private int i = 48;
    private GameObject? prev;

    private void Test(bool rewind) {
        if (rewind) i -= 1;
        /*var sceneObjects = MonsterPreloads.Values.ElementAt(Random.Range(0, MonsterPreloads.Count));
        var obj = sceneObjects[Random.Range(0, sceneObjects.Length)];*/
        var obj = MonsterPreloads!.Values.SelectMany(x => x).ElementAt(i).Value;

        Player.i.health.BecomeInvincible(this);


        /*
         * - 6 explode egg
         *
         * - 47 MonsterNest_Virtual
         */

        var total = MonsterPreloads.Values.Sum(x => x.Count);
        if (prev) Destroy(prev);
        ToastManager.Toast($"{i}/{total - 1} {obj}");
        prev = Replacer.SpawnMonster(obj, null, Player.i.transform.position);

        i += 1;


        // for (var i = 0; i < SceneManager.sceneCount; i++) {
        //     var s = SceneManager.GetSceneAt(i);
        //     ToastManager.Toast(s.name);
        // }
    }


    private IEnumerator LoadPreload(Action<Dictionary<string, Dictionary<string, GameObject>>> completed) {
        var start = Stopwatch.StartNew();

        var bundle = AssemblyUtils.GetEmbeddedAssetBundle("Randomizer.monsters.bundle");
        if (bundle is null) yield break;

        Dictionary<string, Dictionary<string, GameObject>> preloads = new();
        // var allAlreadyLoaded = true;
        try {
            List<AsyncOperation> ops = [];

            var add = (string scenePath, Scene scene) => {
                var rootObjects = scene.GetRootGameObjects();
                var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                if (!preloads.TryGetValue(sceneName, out var scenePreloads)) {
                    scenePreloads = [];
                    preloads[sceneName] = scenePreloads;
                }

                foreach (var obj in rootObjects) {
                    scenePreloads[obj.name] = obj;
                    RCGLifeCycle.DontDestroyForever(obj);
                    // " (RCGLifeCycle)"
                }
            };

            foreach (var scenePath in bundle.GetAllScenePaths()) {
                var alreadyLoaded = SceneManager.GetSceneByPath(scenePath);
                if (alreadyLoaded.isLoaded) yield return SceneManager.UnloadSceneAsync(alreadyLoaded);
                /*if (!alreadyLoaded.isLoaded) allAlreadyLoaded = false;
                else {
                    add(scenePath, alreadyLoaded);
                    continue;
                }*/

                var op = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                if (op == null) continue;
                op.completed += _ => add(scenePath, SceneManager.GetSceneByPath(scenePath));
                ops.Add(op);
            }


            foreach (var op in ops) yield return op;
        } finally {
            bundle.Unload(false);
        }

        start.Stop();
        ToastManager.Toast($"load in {start.ElapsedMilliseconds}ms");

        // if (allAlreadyLoaded)
        // Log.Info("Preloads already loaded");
        // else
        completed(preloads);
    }
}