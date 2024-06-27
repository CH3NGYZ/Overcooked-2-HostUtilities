using BepInEx.Configuration;
using Steamworks;
using Team17.Online;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Connection;
using static PlatformUtils;



namespace HostUtilities
{
    public class KickUser
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static Harmony HarmonyInstance { get; set; }
        public static List<string> banSteamIdList = new List<string>();
        public static List<string> savedSteamIdList = new List<string>();
        public static string banSteamIdListFilePath = "街机MOD-黑名单.txt";
        public static string savedSteamIdListFilePath = "街机MOD-手动保存的个人信息.txt";
        public static string autoSavedSteamIdListFilePath = "街机MOD-自动保存的个人信息.txt";
        public static ConfigEntry<bool> isAutoKickUser;
        public static ConfigEntry<KeyCode> saveAll;
        public static ConfigEntry<KeyCode> kick2;
        public static ConfigEntry<KeyCode> kick3;
        public static ConfigEntry<KeyCode> kick4;
        public static ConfigEntry<KeyCode> kickAndBan2;
        public static ConfigEntry<KeyCode> kickAndBan3;
        public static ConfigEntry<KeyCode> kickAndBan4;
        public static bool IsInLobby = false;
        public static Dictionary<CSteamID, SteamUserInfo> steamIDDictionary = new Dictionary<CSteamID, SteamUserInfo>();
        public class SteamUserInfo
        {
            public string SteamName { get; set; }
            public string Nickname { get; set; }

            public SteamUserInfo(string personaName, string nickname)
            {
                SteamName = personaName;
                Nickname = nickname;
            }
        }


        public static void Awake()
        {
            isAutoKickUser = MODEntry.Instance.Config.Bind<bool>("01-功能开关", "04-自动踢黑名单里的用户", true, "自动踢出在ban列表中的用户");

            kick2 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "04-仅踢出2号位", KeyCode.Alpha2, "按键踢出2号玩家");
            kick3 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "05-仅踢出3号位", KeyCode.Alpha3, "按键踢出3号玩家");
            kick4 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "06-仅踢出4号位", KeyCode.Alpha4, "按键踢出4号玩家");
            kickAndBan2 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "01-拉黑2号位(并踢出)", KeyCode.F2, "拉黑并踢出2号玩家");
            kickAndBan3 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "02-拉黑3号位(并踢出)", KeyCode.F3, "拉黑并踢出3号玩家");
            kickAndBan4 = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "03-拉黑4号位(并踢出)", KeyCode.F4, "拉黑并踢出4号玩家");

            saveAll = MODEntry.Instance.Config.Bind<KeyCode>("02-按键绑定", "07-保存当前房间除自己外所有人的主页链接", KeyCode.Alpha5, "保存当前房间除自己外的所有用户主页链接");
            LoadBannedSteamIdList();
            HarmonyInstance = Harmony.CreateAndPatchAll(MethodBase.GetCurrentMethod().DeclaringType);
            MODEntry.AllHarmony[MethodBase.GetCurrentMethod().DeclaringType.Name] = HarmonyInstance;
        }

        public static void Update()
        {

            if (Input.GetKeyDown(kick2.Value))
            {
                TryKickUser(1, kick2);
                //Log$"按下{kick2.Value}");
            }

            else if (Input.GetKeyDown(kick3.Value))
            {
                TryKickUser(2, kick3);
                //Log$"按下{kick3.Value}");
            }
            else if (Input.GetKeyDown(kick4.Value))
            {
                TryKickUser(3, kick4);
                //Log$"按下{kick4.Value}");
            }
            else if (Input.GetKeyDown(kickAndBan2.Value))
            {
                TryKickUserAndBan(1, kickAndBan2);
                //Log$"按下{kickAndBan2.Value}");
            }
            else if (Input.GetKeyDown(kickAndBan3.Value))
            {
                TryKickUserAndBan(2, kickAndBan3);
                //Log$"按下{kickAndBan3.Value}");
            }
            else if (Input.GetKeyDown(kickAndBan4.Value))
            {
                TryKickUserAndBan(3, kickAndBan4);
                //Log$"按下{kickAndBan4.Value}");
            }
            else if (Input.GetKeyDown(saveAll.Value))
            {
                TrySaveUsersProfileClient();
                MODEntry.ShowWarningDialog($"主页已保存至 {savedSteamIdListFilePath}");
            }
        }



        static void ShowAllPlayersInfo()
        {
            Log("--------------------------------------");
            for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
            {
                User user = ServerUserSystem.m_Users._items[i];
                OnlineUserPlatformId platformID = user.PlatformID;
                bool m_bIsLocal = user.IsLocal;
                Log($"玩家{i} 昵称:{user.DisplayName.RemoveAllTags()} 是否本地:{m_bIsLocal} steamid:{platformID.m_steamId} 主页:https://steamcommunity.com/profiles/{platformID.m_steamId}\n\n\n--------------------------------------");
            }
            Log("--------------------------------------");
        }

        public static bool KickBanListUser(User user)
        {
            OnlineUserPlatformId platformID = user.PlatformID;
            string steamIdString = platformID.m_steamId.ToString();
            string steamCommunityUrl = $"https://steamcommunity.com/profiles/{steamIdString}";
            string steamCommunityUrlWithSplash = $"https://steamcommunity.com/profiles/{steamIdString}/";
            var processedList = banSteamIdList.Select(id => id.Split(',')[0]).ToList();
            if (processedList.Contains(steamIdString) ||
                processedList.Contains(steamCommunityUrl) ||
                processedList.Contains(steamCommunityUrlWithSplash
                ))
            {
                Log($"自动移除  主页: {steamCommunityUrl}  昵称: {user.DisplayName.RemoveAllTags()}");
                UI_DisplayKickedUser.Add_m_Text($"自动移除  {user.DisplayName.RemoveAllTags()}");
                ServerUserSystem.RemoveUser(user, true);
                return true;
            }

            return false;
        }

        public static void TryKickUser(int index, ConfigEntry<KeyCode> kickKey)
        {
            if (!MODEntry.isHost)
            {
                MODEntry.ShowWarningDialog("你不是主机，别点啦");
                return;
            }
            if (ServerUserSystem.m_Users.Count > index)
            {
                User user = ServerUserSystem.m_Users._items[index];
                bool m_bIsLocal = user.IsLocal;
                Log($"尝试移除{index + 1}号:{user.DisplayName.RemoveAllTags()}");
                if (!m_bIsLocal)
                {
                    OnlineUserPlatformId platformID = user.PlatformID;
                    //SteamNetworking.CloseP2PSessionWithUser(platformID.m_steamId);
                    ServerUserSystem.RemoveUser(user, true);
                    Log($"{index + 1}号移除成功:{user.DisplayName.RemoveAllTags()}, Steamid:{platformID.m_steamId}");
                }
                else
                {
                    Log($"{index + 1}号移除失败:{user.DisplayName.RemoveAllTags()}, 本地玩家");
                }
            }
        }
        public static void TryKickUserAndBan(int index, ConfigEntry<KeyCode> kickKey)
        {
            if (!MODEntry.isHost)
            {
                MODEntry.ShowWarningDialog("你不是主机，别点啦");
                return;
            }
            if (ServerUserSystem.m_Users.Count > index)
            {
                User user = ServerUserSystem.m_Users._items[index];
                bool m_bIsLocal = user.IsLocal;
                Log($"尝试移除{index + 1}号:{user.DisplayName.RemoveAllTags()}");
                if (!m_bIsLocal)
                {
                    Log($"{index + 1} 号移除成功: {user.DisplayName.RemoveAllTags()} 并拉黑");

                    OnlineUserPlatformId platformID = user.PlatformID;
                    ServerUserSystem.RemoveUser(user, true);
                    string steamIdString = platformID.m_steamId.ToString();
                    string steamCommunityUrl = $"https://steamcommunity.com/profiles/{steamIdString},{user.DisplayName}";
                    banSteamIdList.Add(steamCommunityUrl);
                    SaveBannedSteamIdList();
                    LoadBannedSteamIdList();
                }
                else
                {
                    Log($"{index + 1}号移除失败:{user.DisplayName.RemoveAllTags()}, 本地玩家");
                }
            }
        }

        public static void TrySaveUsersProfile()
        {
            if (ServerUserSystem.m_Users.Count > 1)
            {
                DateTime currentTime = DateTime.Now;
                string formattedTime = currentTime.ToString("yyyy-MM-dd HH:mm:ss");
                savedSteamIdList.Add($"-----------{formattedTime}----------");
                for (var index = 1; index < ServerUserSystem.m_Users.Count; index++)
                {
                    User user = ServerUserSystem.m_Users._items[index];
                    Log($"保存:{user.DisplayName.RemoveAllTags()}");
                    OnlineUserPlatformId platformID = user.PlatformID;
                    CSteamID? csteamID = (platformID != null) ? new CSteamID?(platformID.m_steamId) : null;
                    string steamIdString = csteamID.ToString();
                    string steamCommunityUrl = $"steam主页链接: https://steamcommunity.com/profiles/{steamIdString} ,昵称: {user.DisplayName}  ";
                    savedSteamIdList.Add(steamCommunityUrl);
                }
                savedSteamIdList.Add("---------------------------------------------");
                SaveSavedSteamIdList(savedSteamIdList.ToArray());
                savedSteamIdList.Clear();
            }

        }
        public static void TrySaveUsersProfileClient()
        {
            if (ClientUserSystem.m_Users.Count > 1)
            {
                DateTime currentTime = DateTime.Now;
                string formattedTime = currentTime.ToString("yyyy-MM-dd HH:mm:ss");
                savedSteamIdList.Add($"------------{formattedTime}-----------");
                for (var index = 0; index < ClientUserSystem.m_Users.Count; index++)
                {
                    User user = ClientUserSystem.m_Users._items[index];
                    if (user.IsLocal != true)
                    {

                        Log($"保存:{user.DisplayName.RemoveAllTags()}");
                        OnlineUserPlatformId platformID = user.PlatformID;
                        CSteamID? csteamID = (platformID != null) ? new CSteamID?(platformID.m_steamId) : null;
                        string steamIdString = csteamID.ToString();
                        string steamCommunityUrl = $"steam主页链接: https://steamcommunity.com/profiles/{steamIdString} ,昵称: {user.DisplayName}  ";
                        savedSteamIdList.Add(steamCommunityUrl);
                    }
                }
                savedSteamIdList.Add("---------------------------------------------");
                SaveSavedSteamIdList(savedSteamIdList.ToArray());
                savedSteamIdList.Clear();
            }

        }

        public static void LoadBannedSteamIdList()
        {
            if (File.Exists(banSteamIdListFilePath))
            {
                string[] lines = File.ReadAllLines(banSteamIdListFilePath);
                banSteamIdList = new List<string>(lines);
            }
        }


        public static void SaveBannedSteamIdList()
        {
            File.WriteAllLines(banSteamIdListFilePath, banSteamIdList.ToArray());
        }


        public static void SaveSavedSteamIdList(string[] manualSavedSteamIdList)
        {
            string contentToAppend = string.Join(Environment.NewLine, manualSavedSteamIdList);
            File.AppendAllText(savedSteamIdListFilePath, contentToAppend + Environment.NewLine);
        }


        public static void SaveAutoSavedSteamIdList(string[] autoSavedSteamIdList)
        {
            string contentToAppend = string.Join(Environment.NewLine, autoSavedSteamIdList);
            File.AppendAllText(autoSavedSteamIdListFilePath, contentToAppend + Environment.NewLine);
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(ServerUserSystem), "AddUser")]
        public static void ServerUserSystem_AddUser_Patch(User.MachineID machine, EngagementSlot engagement)
        {
            try
            {
                if (MODEntry.isHost)
                {
                    FastList<User> users = ServerUserSystem.m_Users;
                    User user = UserSystemUtils.FindUser(users, null, machine, engagement, TeamID.Count, User.SplitStatus.Count);
                    if (!user.IsLocal)
                    {
                        CSteamID csteamID = user.m_PlatformID.m_steamId;
                        if (EFriendRelationship.k_EFriendRelationshipFriend == SteamFriends.GetFriendRelationship(csteamID))
                        {
                            Log($"{user.DisplayName.RemoveAllTags()} 为好友, 跳过保存和踢出");
                            string personaName = SteamFriends.GetFriendPersonaName(csteamID);
                            string nickname = SteamFriends.GetPlayerNickname(csteamID);
                            steamIDDictionary[csteamID] = new SteamUserInfo(personaName, nickname);
                            return;
                        }
                        if (isAutoKickUser.Value)
                        {
                            if (MODEntry.isInLobby)
                            {
                                bool isKicked = KickBanListUser(user);
                                if (isKicked)
                                {
                                    return;
                                }
                            }
                        }


                        Log($"保存:{user.DisplayName.RemoveAllTags()}");
                        OnlineUserPlatformId platformID = user.PlatformID;

                        string steamIdString = csteamID.ToString();
                        string steamCommunityUrl = $"steam主页链接: https://steamcommunity.com/profiles/{steamIdString}";

                        DateTime currentTime = DateTime.Now;
                        string formattedTime = currentTime.ToString("yyyy-MM-dd HH:mm:ss");
                        string[] autoSavedSteamIdList = new string[]
                        {
                            $"------------{formattedTime}-----------",
                            $"游戏昵称: {user.DisplayName.RemoveAllTags()}",steamCommunityUrl,
                            "---------------------------------------------"
                        };
                        SaveAutoSavedSteamIdList(autoSavedSteamIdList);
                    }
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIPlayerMenuBehaviour), "UpdateMenuStructure")]
        public static bool UIPlayerMenuBehaviour_UpdateMenuStructure_Prefix(UIPlayerMenuBehaviour __instance)
        {
            try
            {
                if (!MODEntry.isHost)
                {
                    return true;
                }

                List<UIPlayerMenuBehaviour.MenuOption> menuOptions = __instance.m_menuOptions;

                User m_User = __instance.m_User;
                for (int i = 0; i < menuOptions.Count; i++)
                {
                    bool flag = true;
                    if (m_User == null)
                    {
                        flag = false;
                    }
                    UIPlayerMenuBehaviour.UIPlayerMenuOptions type = menuOptions[i].m_type;
                    if (type == UIPlayerMenuBehaviour.UIPlayerMenuOptions.Mute || type == UIPlayerMenuBehaviour.UIPlayerMenuOptions.Unmute)
                    {
                        flag = false;
                    }
                    if (type == UIPlayerMenuBehaviour.UIPlayerMenuOptions.Kick && m_User.IsLocal)
                    {
                        flag = false;
                    }
                    menuOptions[i].m_button.gameObject.SetActive(flag);
                }
                MODEntry.LogWarning("已patch  UpdateMenuStructure");
                __instance.UpdateNavigation();
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
            return false;
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIPlayerMenuBehaviour), "KickUser")]
        public static bool UIPlayerMenuBehaviour_KickUser_Prefix(UIPlayerMenuBehaviour __instance)
        {
            try
            {
                HandleKickUserAsync(__instance);
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
            return false;
        }


        public static void HandleKickUserAsync(UIPlayerMenuBehaviour __instance)
        {
            try
            {
                MultiplayerController multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                if (multiplayerController == null)
                {
                    Log("实例不存在");
                    return;
                }
                // 寻找user
                User m_User = __instance.m_User;
                int num = ClientUserSystem.m_Users.FindIndex((User x) => x == m_User);
                User user = ServerUserSystem.m_Users._items[num];
                //OnlineUserPlatformId platformID = user.PlatformID;

                //Server LocalServer = m_LocalServer.GetValue(multiplayerController) as Server;
                //Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection> RemoteClientConnectionsDict = m_RemoteClientConnections.GetValue(LocalServer) as Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection>;
                //SteamNetworking.CloseP2PSessionWithUser(platformID.m_steamId);

                //{
                //    IOnlineMultiplayerSessionUserId sessionId = user.SessionId;

                //    if (sessionId != null && RemoteClientConnectionsDict.ContainsKey(sessionId))
                //    {
                //        NetworkConnection networkConnection = RemoteClientConnectionsDict[sessionId];
                //        LocalServer.HandleDisconnectMessage(networkConnection);
                //        //        object[] parameters = new object[] { sessionId, networkConnection };
                //        //        RemoveConnection.Invoke(LocalServer, parameters);
                //    }
                //}

                ServerUserSystem.RemoveUser(user, true);
                //OnUserRemoved.Invoke(LocalServer, new object[] { user });
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }

    }
}
