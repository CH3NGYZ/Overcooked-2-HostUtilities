using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Team17.Online;
namespace HostUtilities
{
    public class ChangeDisplayName
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static Harmony HarmonyInstance { get; set; }
        public static ConfigEntry<string> playerName;
        public static string lastPlayerName;
        public static ConfigEntry<bool> isReplaceName;
        private static readonly string PromptWords = "";
        public static void Awake()
        {
            playerName = MODEntry.Instance.Config.Bind("00-UI", "01-替换名字(第一次使用请鼠标悬浮查看说明)", "<size=45><color=#00FFFF>MOD群 860480677</color></size>", "请提前删除所有的双引号. 主机修改立刻生效,客机也能看到生效的新名字,客机修改名字需要重新加入战局,或者重新进入街机. 请注意!!!客机名字长度有限制,主机没有长度限制,如果客机进入战局名字空白,请尝试改短一点名字!");
            isReplaceName = MODEntry.Instance.Config.Bind<bool>("00-UI", "00-替换名字开关(关闭须重启游戏)", false);
            HarmonyInstance = Harmony.CreateAndPatchAll(MethodBase.GetCurrentMethod().DeclaringType);
            MODEntry.AllHarmony[MethodBase.GetCurrentMethod().DeclaringType.Name] = HarmonyInstance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClientTime), "OnTimeSyncReceived")]
        public static void ClientTime_OnTimeSyncReceived_Patch()
        {
            try
            {
                if (isReplaceName.Value)
                {
                    if (lastPlayerName != playerName.Value)
                    {
                        FastList<User> serveruser = ServerUserSystem.m_Users;
                        for (int i = 0; i < serveruser.Count; i++)
                        {
                            if (serveruser._items[i].IsLocal)
                                serveruser._items[i].DisplayName =
                                    PromptWords +
                                    playerName.Value;
                        }
                        FastList<User> clientuser = ClientUserSystem.m_Users;
                        for (int i = 0; i < clientuser.Count; i++)
                        {
                            if (clientuser._items[i].IsLocal)
                                clientuser._items[i].DisplayName =
                                    PromptWords +
                                    playerName.Value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }


        [HarmonyPatch(typeof(User), "DisplayName", MethodType.Getter)]
        [HarmonyPostfix]
        public static void DisplayName_Getter_Prefix(ref string __result, User __instance)
        {
            try
            {
                if (isReplaceName.Value)
                {
                    if (__instance.IsLocal)
                    {
                        __result =
                            PromptWords +
                            playerName.Value;
                    }
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }

        [HarmonyPatch(typeof(User), "DisplayName", MethodType.Setter)]
        [HarmonyPrefix]
        public static void DisplayName_Setter_Prefix(ref string value, User __instance)
        {
            try
            {
                if (isReplaceName.Value)
                {
                    if (__instance.IsLocal)
                    {
                        value =
                            PromptWords +
                            playerName.Value;
                    }
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }

        [HarmonyPatch(typeof(JoinDataProvider), "BuildJoinRequestData")]
        [HarmonyPrefix]
        public static void JoinDataProvider_BuildJoinRequestData_Prefix(ref OnlineMultiplayerLocalUserId requestingLocalUser)
        {
            try
            {
                if (isReplaceName.Value)
                {
                    requestingLocalUser.m_userName =
                    PromptWords +
                    playerName.Value;
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }
    }
}
