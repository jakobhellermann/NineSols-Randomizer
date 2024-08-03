using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using ExampleMod;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NineSolsAPI;
using NineSolsAPI.Utils;
using RCGFSM.Items;
using RCGMaker.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Randomizer;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[HarmonyPatch]
public class Randomizer : BaseUnityPlugin {
    [HarmonyPatch(typeof(GeneralInteraction), "InteractedImplementation")]
    [HarmonyPrefix]
    private static bool I(ref GeneralInteraction __instance) {
        try {
            ToastManager.Toast($"interacted {__instance.OnInteracted.m_Calls.m_RuntimeCalls.Count}");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }

        return true;
    }


    private Harmony harmony = null!;


    public class IgnoreTypeContractResolver(string[] typesToIgnore, string[] fieldsToIgnore) : DefaultContractResolver {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            var property = base.CreateProperty(member, memberSerialization);

            if (Array.Exists(typesToIgnore, t => property.PropertyType?.Name == t))
                property.ShouldSerialize = _ => false;
            return property;
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
            // Get all the fields of the specified type
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var jsonProperties = new List<JsonProperty>();
            foreach (var field in fields) {
                if (fieldsToIgnore.Contains(field.Name)) continue;


                var jsonProperty = base.CreateProperty(field, memberSerialization);
                jsonProperty.Readable = true;
                jsonProperty.Writable = true;
                jsonProperties.Add(jsonProperty);
            }

            return jsonProperties;
        }
    }

    private class FlagFieldConverter<T> : JsonConverter<FlagField<T>> {
        public override void WriteJson(JsonWriter writer, FlagField<T>? value, JsonSerializer serializer) {
            serializer.Serialize(writer, value != null ? value.CurrentValue : null);
        }

        public override FlagField<T>? ReadJson(JsonReader reader, Type objectType, FlagField<T>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer) =>
            throw new NotImplementedException();
    }

    private void Awake() {
        Log.Init(Logger);
        RCGLifeCycle.DontDestroyForever(gameObject);
        harmony = Harmony.CreateAndPatchAll(typeof(Randomizer).Assembly);

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        var allFlags = GameConfig.Instance.allGameFlags;

        try {
            ToastManager.Toast(" doing");
            var json = JsonConvert.SerializeObject(allFlags.flagDict, Formatting.Indented, new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver =
                    new IgnoreTypeContractResolver(
                        ["AsyncOperationHandle"],
                        ["onChangeActionDict", "propertyCache"]
                    ),
                Converters = [
                    // new FlagFieldConverter<FlagField<bool>>(),
                    // new FlagFieldConverter<FlagField<int>>(),
                ],
            });
            ToastManager.Toast("writing");
            File.WriteAllText("/home/jakob/allFlags.json", json);
            ToastManager.Toast("gc");
            GC.Collect();
            ToastManager.Toast("done");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }

        List<ItemData> items = [];
        foreach (var flag in GameConfig.Instance.allGameFlags.Flags)
            if (flag is ItemData item)
                items.Add(item);

        ToastManager.Toast(items.Count);


        ToastManager.Toast(DropItemCollection.Instance.healthDropPrefabs.Count);

        KeybindManager.Add(this, () => { GameCore.Instance.DiscardUnsavedFlagsAndReset(); }, KeyCode.I);

        KeybindManager.Add(this, () => {
            const string path =
                "A2_S1/Room/Prefab/寶箱 Chests 左/LootProvider 多功能工具組/0_DropPickable Bag FSM";
            var obj = ObjectUtils.LookupPath(path)!;

            try {
                var copy = ObjectUtils.InstantiateInit(obj);
                var fsm = copy.GetComponentInChildren<StateMachineOwner>().FsmContext;
                fsm.fsm.ChangeStateByName("Idle");

                var getItemAction = fsm.fsm.GetStateByName("Picking").GetActionByName<PickItemAction>("GetItem");
                getItemAction.pickItemData = items[Random.Range(0, items.Count)];
                // ToastManager.Toast(getItemAction.pickItemData);
                var provider = AccessTools.FieldRefAccess<PickItemAction, ItemProvider>("itemProvider")
                    .Invoke(getItemAction);
                provider.item = getItemAction.pickItemData;

                /*copy.GetComponentInChildren<GeneralInteraction>(true).OnInteracted.AddListener(() => {
                    fsm.fsm.ChangeStateByName("Picking");
                });*/

                copy.SetActive(true);
                copy.transform.position = Player.i.transform.position;
            } catch (Exception e) {
                ToastManager.Toast(e);
            }
        }, KeyCode.Q);
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
    }
}

internal static class FsmExtensions {
    public static T GetStateByName<T>(this StateMachine<T> fsm, string stateName) where T : class {
        foreach (var state in fsm._stateMapping.getAllStates) {
            var name = state.stateBehavior.gameObject.name.TrimStartMatches("[State] ")
                .TrimEndMatches(" (GeneralState)");
            if (name.Equals(stateName.AsSpan(), StringComparison.InvariantCulture)) return state.state;
        }

        throw new Exception(
            $"state '{stateName}' not found in fsm {fsm}, found {fsm._stateMapping.getAllStates.Select(state => state.stateBehavior.gameObject.name.TrimStartMatches("[State] ").TrimEndMatches(" (GeneralState)").ToString()).Join()}");
    }

    public static AbstractStateAction GetActionByName(this GeneralState fsm, string actionName) {
        foreach (var action in fsm.Actions) {
            var name = action.gameObject.name.TrimStartMatches("[Action] ");
            if (name.Equals(actionName.AsSpan(), StringComparison.InvariantCulture)) return action;
        }

        throw new Exception(
            $"state '{actionName}' not found in fsm {fsm}, found {fsm.Actions.Select(action => action.gameObject.name.TrimStartMatches("[Action] ").ToString()).Join()}");
    }

    public static T GetActionByName<T>(this GeneralState fsm, string actionName) {
        var action = fsm.GetActionByName(actionName);
        if (action is T a) return a;
        else
            throw new Exception(
                $"action {actionName} is not of type {typeof(T).Name}, but {action.GetType().Name}");
    }

    public static void ChangeStateByName<T>(this StateMachine<T> fsm, string stateName,
        StateTransition transition = StateTransition.Safe, bool forceSameState = false)
        where T : class {
        var state = fsm.GetStateByName(stateName);
        fsm.ChangeState(state, transition, forceSameState);
    }
}