using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace HostUtilities
{
    public class AddDirtyDishes
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static ConfigEntry<KeyCode> AddDirtyDishesKey;
        public static int startTime;
        public static bool cooling = false;
        public static int plateOrGlassNum = 0;
        public static ServerKitchenFlowControllerBase flowController;

        public static void Awake()
        {
            AddDirtyDishesKey = MODEntry.Instance.Config.Bind("02-按键绑定", "13-增加1个脏盘/杯(仅街机)", KeyCode.Alpha0);
        }

        private static void ScorePenalty(int penalty)
        {
            // Decompile the Randomizer dll from gua.
            flowController = Object.FindObjectOfType<ServerKitchenFlowControllerBase>();
            TeamID teamID = TeamID.One;
            ServerTeamMonitor monitorForTeam = flowController.GetMonitorForTeam(teamID);
            if (monitorForTeam.Score.TotalBaseScore >= penalty)
            {
                monitorForTeam.Score.TotalBaseScore -= penalty;
            }
            else
            {
                monitorForTeam.Score.TotalTimeExpireDeductions += penalty;
            }

            monitorForTeam.Score.TotalMultiplier = 0;
            monitorForTeam.Score.TotalCombo = 0;
            monitorForTeam.Score.ComboMaintained = false;

            KitchenFlowMessage kitchenFlowMessage = new KitchenFlowMessage();
            kitchenFlowMessage.Initialise_ScoreOnly(teamID);
            kitchenFlowMessage.SetScoreData(monitorForTeam.Score);
            flowController.SendServerEvent(kitchenFlowMessage);
            ScoreUIController scoreUIController = Object.FindObjectOfType<ScoreUIController>();
            scoreUIController?.ScoreUpdate(teamID, monitorForTeam.Score);

            ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.RecipeTimeOut, LayerMask.NameToLayer("Default"));
            GameUtils.TriggerAudio(GameOneShotAudioTag.RecipeTimeOut, LayerMask.NameToLayer("Default"));
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClientKitchenFlowControllerBase), "ApplyServerEvent")]
        public static void ClientKitchenFlowControllerBaseApplyServerEventPacth(ClientKitchenFlowControllerBase __instance, Team17.Online.Multiplayer.Messaging.Serialisable serialisable)
        {
            KitchenFlowMessage kitchenFlowMessage = (KitchenFlowMessage)serialisable;
            if (kitchenFlowMessage.m_msgType == KitchenFlowMessage.MsgType.ScoreOnly)
            {
                GameUtils.TriggerAudio(GameOneShotAudioTag.RecipeTimeOut, LayerMask.NameToLayer("Default"));
                __instance.UpdateScoreUI(kitchenFlowMessage.m_teamID);
            }
        }

        private static int GetScore()
        {
            // Decompile the Randomizer dll from gua.
            flowController = Object.FindObjectOfType<ServerKitchenFlowControllerBase>();
            if (flowController != null)
            {
                TeamID teamID = TeamID.One;
                ServerTeamMonitor monitorForTeam = flowController.GetMonitorForTeam(teamID);
                return monitorForTeam.Score.GetTotalScore();
            }
            else
            {
                return -9999999;
            }
        }

        public static void Update()
        {
            if (Input.GetKeyDown(AddDirtyDishesKey.Value))
            {
                if (!MODEntry.isHost)
                {
                    MODEntry.ShowWarningDialog("你不是主机，别点啦");
                    return;
                }
                if (!MODEntry.isInParty)
                {
                    MODEntry.ShowWarningDialog("请在街机中使用此功能。");
                    return;
                }

                int cost = 500;
                if (FixDoubleServing.levelName.Contains("Night_3_3"))
                {
                    Log("麻团3-3, 脏盘子不限制");
                    cost = 0;
                }

                int score = GetScore();
                if (score == -9999999)
                {
                    //MODEntry.ShowWarningDialog($"不在战局中, 无法获取金钱");
                    return;
                }
                if (score < cost)
                {
                    MODEntry.ShowWarningDialog($"钱不够, 现在一共{score}块钱, 请努力做菜, 添加一个脏盘子需花费500。");
                    return;
                }

                GameObject DirtyPlateStackObject = GameObject.Find("DirtyPlateStack");
                GameObject DirtyGlassStackObject = GameObject.Find("DirtyGlassStack");
                GameObject DLC_08DirtyTrayStackObject = GameObject.Find("DLC08_DirtyTrayStack");
                GameObject DirtyMugStackObject = GameObject.Find("DirtyMugStack");

                if (DirtyPlateStackObject == null && DirtyGlassStackObject == null && DirtyMugStackObject == null && DLC_08DirtyTrayStackObject == null)
                {
                    //MODEntry.ShowWarningDialog("请先上一个菜, 出“脏盘子/脏杯子/脏托盘/脏马克杯”后再按。无脏盘关无法使用。");
                    return;
                }


                ServerDirtyPlateStack serverDirtyPlateStack = DirtyPlateStackObject?.GetComponent<ServerDirtyPlateStack>();
                serverDirtyPlateStack?.AddToStack();
                ServerDirtyPlateStack serverDirtyGlassStack = DirtyGlassStackObject?.GetComponent<ServerDirtyPlateStack>();
                serverDirtyGlassStack?.AddToStack();
                ServerDirtyPlateStack serverDLC_08DirtyTrayStack = DLC_08DirtyTrayStackObject?.GetComponent<ServerDirtyPlateStack>();
                serverDLC_08DirtyTrayStack?.AddToStack();
                ServerDirtyPlateStack serverDirtyMugStack = DirtyMugStackObject?.GetComponent<ServerDirtyPlateStack>();
                serverDirtyMugStack?.AddToStack();
                ScorePenalty(cost);
            }
        }
    }
}
