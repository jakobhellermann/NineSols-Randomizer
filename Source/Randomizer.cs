using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ExampleMod;
using HarmonyLib;
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
    private Harmony harmony = null!;


    private void Awake() {
        Log.Init(Logger);
        RCGLifeCycle.DontDestroyForever(gameObject);
        harmony = Harmony.CreateAndPatchAll(typeof(Randomizer).Assembly);

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        List<GameFlagDescriptable> items = [];
        foreach (var flag in GameConfig.Instance.allGameFlags.Flags)
            if (flag is ReceiveItemData item) {
                // ToastManager.Toast(item);
                items.Add(item);
            }

        // ToastManager.Toast(items.Count);

        KeybindManager.Add(this, () => GameCore.Instance.DiscardUnsavedFlagsAndReset(), KeyCode.I);

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