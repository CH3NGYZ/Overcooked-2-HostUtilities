using BepInEx.Configuration;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Connection;
using Team17.Online;
using UnityEngine;
using Team17.Online.Multiplayer;
using HarmonyLib;
using System;
using System.Reflection;
using System.Linq;
using static HostUtilities.KickUser;
using Steamworks;
using BitStream;
using System.Text;
using Team17.Online.Multiplayer.Messaging;
namespace HostUtilities
{
    public class UI_DisplayLatency
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static List<UserConnectionInfo> UserConnections { get; set; } = new List<UserConnectionInfo>();
        public static Harmony HarmonyInstance { get; set; }
        private static MyOnScreenDebugDisplay onScreenDebugDisplay;
        private static NetworkStateDebugDisplay NetworkDebugUI = null;
        public static ConfigEntry<bool> ShowEnabled;
        public static ConfigEntry<bool> ShowEnabledhost;
        public static ConfigEntry<bool> isShowDebugInfo;
        public static bool canAdd;
        public const MessageType UsersMessageType = (MessageType)69;
        private const float BroadcastIntervalSeconds = 1.0f; // 广播间隔时间，每隔1秒进行一次广播
        private static float broadcastTimer = 0.0f;
        private static string serverFriendsMessage = String.Empty;

        public static void Awake()
        {
            ShowEnabled = MODEntry.Instance.Config.Bind<bool>("00-UI", "03-01-屏幕右上角显示延迟", true);
            ShowEnabledhost = MODEntry.Instance.Config.Bind<bool>("00-UI", "03-02-显示主机视角延迟(主机开启才会显示)", false);
            isShowDebugInfo = MODEntry.Instance.Config.Bind<bool>("00-UI", "04-屏幕右上角增加显示调试信息", false);
            canAdd = false;
            onScreenDebugDisplay = new MyOnScreenDebugDisplay();
            onScreenDebugDisplay.Awake();
            HarmonyInstance = Harmony.CreateAndPatchAll(MethodBase.GetCurrentMethod().DeclaringType);
            MODEntry.AllHarmony[MethodBase.GetCurrentMethod().DeclaringType.Name] = HarmonyInstance;

        }

        public static void Update()
        {

            onScreenDebugDisplay.Update();

            if (NetworkDebugUI != null && !ShowEnabled.Value)
            {
                RemoveNetworkDebugUI();
            }
            else if (NetworkDebugUI == null && ShowEnabled.Value && canAdd)
            {
                AddNetworkDebugUI();
            }
            if (ShowEnabledhost.Value && !ShowEnabled.Value)
            {
                ShowEnabledhost.Value = false;
            }
            broadcastTimer += Time.deltaTime;
            if (broadcastTimer >= BroadcastIntervalSeconds)
            {
                if (MODEntry.isHost && ShowEnabledhost.Value)
                {
                    UserConnections.Add(new UserConnectionInfo("主机", 0, (CSteamID)MODEntry.CurrentSteamID.m_SteamID));
                    try
                    {
                        MultiplayerController multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                        Server server = multiplayerController.m_LocalServer;
                        Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection> remoteClientConnectionsDict = server.m_RemoteClientConnections;

                        if (server != null)
                        {
                            int index = 2;
                            foreach (User user in ServerUserSystem.m_Users._items.Skip(1))
                            {
                                foreach (var kvp in remoteClientConnectionsDict)
                                {
                                    IOnlineMultiplayerSessionUserId sessionUserId = kvp.Key;
                                    NetworkConnection connection = kvp.Value;
                                    if (user.DisplayName == sessionUserId.DisplayName)
                                    {
                                        float latency = connection.GetConnectionStats(bReliable: false).m_fLatency;
                                        UserConnections.Add(new UserConnectionInfo(user.DisplayName, latency, user.PlatformID.m_steamId));
                                        index++;
                                    }
                                }
                            }


                        }

                    }
                    catch (Exception) { }
                }
                try
                {
                    if (UserConnections.Count > 1)
                    {
                        //foreach (var info in UserConnections)
                        //{
                        //    Log($"{info.DisplayName} - {info.SteamId} - {info.Latency}ms");
                        //}
                        MODEntry.localServer?.BroadcastMessageToAll(UsersMessageType, new UsersMessage(UserConnections));
                        UserConnections.Clear();
                    }
                }
                catch (Exception) { }
                UserConnections.Clear();
                broadcastTimer = 0.0f;

            }

        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(MetaGameProgress), "ByteLoad")]
        public static void MetaGameProgressByteLoadPatch(MetaGameProgress __instance)
        {
            canAdd = true;
        }

        public static void OnGUI() => onScreenDebugDisplay.OnGUI();

        private static void AddNetworkDebugUI()
        {
            NetworkDebugUI = new NetworkStateDebugDisplay();
            onScreenDebugDisplay.AddDisplay(NetworkDebugUI);
            //NetworkDebugUI.init_m_Text();
        }

        private static void RemoveNetworkDebugUI()
        {
            onScreenDebugDisplay.RemoveDisplay(NetworkDebugUI);
            NetworkDebugUI.OnDestroy();
            NetworkDebugUI = null;
        }


        private class MyOnScreenDebugDisplay
        {
            private readonly List<DebugDisplay> m_Displays = new List<DebugDisplay>();
            private readonly GUIStyle m_GUIStyle = new GUIStyle();
            public void AddDisplay(DebugDisplay display)
            {
                if (display != null)
                {
                    display.OnSetUp();
                    m_Displays.Add(display);
                }
            }

            public void RemoveDisplay(DebugDisplay display) => m_Displays.Remove(display);

            public void Awake()
            {
                m_GUIStyle.alignment = TextAnchor.UpperRight;
                m_GUIStyle.fontSize = Mathf.RoundToInt(MODEntry.defaultFontSize.Value * MODEntry.dpiScaleFactor);
                this.m_GUIStyle.normal.textColor = MODEntry.defaultFontColor.Value;
                m_GUIStyle.richText = false;
            }

            public void Update()
            {
                for (int i = 0; i < m_Displays.Count; i++)
                    m_Displays[i].OnUpdate();
            }

            public void OnGUI()
            {
                m_GUIStyle.fontSize = Mathf.RoundToInt(MODEntry.defaultFontSize.Value * MODEntry.dpiScaleFactor);
                this.m_GUIStyle.normal.textColor = MODEntry.defaultFontColor.Value;

                Rect rect = new Rect(0f, 0f, Screen.width, m_GUIStyle.fontSize);
                for (int i = 0; i < m_Displays.Count; i++)
                    m_Displays[i].OnDraw(ref rect, m_GUIStyle);
            }
        }


        public class NetworkStateDebugDisplay : DebugDisplay
        {
            private FastList<User> clientUserSystem_catched = new FastList<User>();
            private string clientFriendsMessage = String.Empty;
            public override void OnSetUp()
            {
            }

            public override void OnUpdate()
            {
            }

            public override void OnDraw(ref Rect rect, GUIStyle style)
            {
                if (isShowDebugInfo.Value)
                {
                    string text = string.Empty;
                    string text2 = string.Empty;
                    if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
                    {
                        ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
                        text = ", visibility: " + serverOptions.visibility.ToString();
                        text2 = ", gameMode: " + serverOptions.gameMode.ToString();
                    }
                    else if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Matchmake)
                    {
                        MatchmakeData matchmakeData = (MatchmakeData)ConnectionModeSwitcher.GetAgentData();
                        if (ConnectionStatus.IsHost())
                        {
                            text = ",HostgameMode: " + OnlineMultiplayerSessionVisibility.eMatchmaking;
                        }
                        text2 = ",ClientgameMode: " + matchmakeData.gameMode.ToString();
                    }
                    DrawText(ref rect, style, string.Concat(new string[]
                    {
                    "RequestedConnectionState: ",
                    ConnectionModeSwitcher.GetRequestedConnectionState().ToString(),
                    text,
                    text2,
                    ",Progress: ",
                    ConnectionModeSwitcher.GetStatus().GetProgress().ToString(),
                    " Result: ",
                    ConnectionModeSwitcher.GetStatus().GetResult().ToString()
                    }));

                    //LobbyInfo
                    string Lobbymessage = "NotInLobby";
                    if (ClientLobbyFlowController.Instance != null)
                    {
                        Lobbymessage = ClientLobbyFlowController.Instance.m_state.ToString();
                    }
                    DrawText(ref rect, style, string.Concat(new string[]
                    {
                    "LobbyState: ",
                    Lobbymessage,
                    ",joinCode: ",
                    ForceHost.joinReturnCode
                    }));
                    DrawText(ref rect, style, ClientGameSetup.Mode + ", time: " + ClientTime.Time().ToString("00000.000"));
                }
                if (MODEntry.isHost)
                {
                    try
                    {
                        MultiplayerController multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                        Server server = multiplayerController.m_LocalServer;
                        Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection> remoteClientConnectionsDict = server.m_RemoteClientConnections;

                        if (server != null)
                        {
                            int index = 2;
                            foreach (User user in ServerUserSystem.m_Users._items.Skip(1))
                            {
                                foreach (var kvp in remoteClientConnectionsDict)
                                {
                                    IOnlineMultiplayerSessionUserId sessionUserId = kvp.Key;
                                    NetworkConnection connection = kvp.Value;
                                    if (user.DisplayName == sessionUserId.DisplayName)
                                    {
                                        float latency = connection.GetConnectionStats(bReliable: false).m_fLatency;
                                        if (KickUser.steamIDDictionary.ContainsKey(user.PlatformID.m_steamId))
                                        {
                                            if (KickUser.steamIDDictionary.TryGetValue(user.PlatformID.m_steamId, out SteamUserInfo userInfo))
                                            {
                                                string username = userInfo.SteamName;
                                                string nickname = userInfo.Nickname;
                                                string nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" [{nickname}]";
                                                DrawText(ref rect, style, $"{user.DisplayName} (好友 {username}{nicknamePart}) {index}号位 {(latency == 0 ? "获取错误" : (latency * 1000 * 2).ToString("000") + " ms")}");

                                            }
                                        }
                                        else
                                        {
                                            DrawText(ref rect, style, $"{user.DisplayName} {index}号位 {(latency == 0 ? "获取错误" : (latency * 1000 * 2).ToString("000") + " ms")}");

                                        }
                                        index++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
                else if (ConnectionStatus.IsInSession())
                {
                    try
                    {
                        MultiplayerController multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                        Client client = multiplayerController.m_LocalClient;
                        if (client != null)
                            if (ShowEnabledhost.Value)
                            {
                                if (!string.IsNullOrEmpty(serverFriendsMessage))
                                    DrawText(ref rect, style, serverFriendsMessage);

                            }
                            else
                            {
                                {
                                    ConnectionStats connectionStats = client.GetConnectionStats(bReliable: false);
                                    FastList<User> clientUserSystem = ClientUserSystem.m_Users;

                                    bool hasChanged = HasUserListChanged(clientUserSystem, clientUserSystem_catched);
                                    if (hasChanged)
                                    {
                                        clientUserSystem_catched = new FastList<User>();
                                        // 更新存储的用户列表
                                        clientUserSystem_catched.AddRange(clientUserSystem.ToArray());
                                        clientFriendsMessage = string.Empty;
                                        // 更新提示信息
                                        for (int i = 0; i < clientUserSystem.Count; i++)
                                        {
                                            User user = clientUserSystem._items[i];
                                            if (user.IsLocal)
                                            {
                                                continue;
                                            }
                                            CSteamID csteamID = user.PlatformID.m_steamId;
                                            if (EFriendRelationship.k_EFriendRelationshipFriend == SteamFriends.GetFriendRelationship(csteamID))
                                            {
                                                string personaName = SteamFriends.GetFriendPersonaName(csteamID);
                                                string nickname = SteamFriends.GetPlayerNickname(csteamID);
                                                string nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" [{nickname}]";
                                                clientFriendsMessage += $"{user.DisplayName} (好友 {personaName}{nickname}) {i + 1}号位\n";
                                            }
                                        }
                                    }

                                    string latencyText = connectionStats.m_fLatency == (float)0 ? "获取错误" : (connectionStats.m_fLatency * 1000 * 2).ToString("000") + " ms";
                                    DrawText(ref rect, style, $"{clientFriendsMessage}本机 {latencyText}");
                                }
                            }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            private bool HasUserListChanged(FastList<User> currentUserSystem, FastList<User> cachedUserSystem)
            {
                if (currentUserSystem.Count != cachedUserSystem.Count)
                {
                    //Log($"用户列表Count变化 current {currentUserSystem.Count} cached {cachedUserSystem.Count}");
                    return true;
                }

                for (int i = 0; i < currentUserSystem.Count; i++)
                {
                    if (!currentUserSystem._items[i].Equals(cachedUserSystem._items[i]))
                    {
                        //Log("用户列表变化");
                        return true;
                    }
                }
                return false;
            }
        }
        public static void OnUsersMessage(UsersMessage message)
        {

            if (!MODEntry.isHost && ShowEnabledhost.Value)
            {
                int i = 0;
                serverFriendsMessage = string.Empty;
                foreach (var info in message.UserConnections)
                {
                    //Log($"{info.DisplayName} - {info.SteamId} - {info.Latency}ms");
                    if (info.SteamId == (CSteamID)MODEntry.CurrentSteamID.m_SteamID)
                    {
                        serverFriendsMessage += $"本机 {i + 1}号位 {(info.Latency == 0 ? "获取错误" : (info.Latency * 1000 * 2).ToString("000") + " ms")}\n";
                    }
                    else if (EFriendRelationship.k_EFriendRelationshipFriend == SteamFriends.GetFriendRelationship(info.SteamId))
                    {
                        string personaName = SteamFriends.GetFriendPersonaName(info.SteamId);
                        string nickname = SteamFriends.GetPlayerNickname(info.SteamId);
                        string nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" [{nickname}]";
                        if (i != 0)
                        {
                            serverFriendsMessage += $"{info.DisplayName} (好友 {personaName}{nickname}) {i + 1}号位 {(info.Latency == 0 ? "获取错误" : (info.Latency * 1000 * 2).ToString("000") + " ms")}\n";
                        }
                        serverFriendsMessage += $"{info.DisplayName} (好友 {personaName}{nickname})\n";


                    }
                    else
                        serverFriendsMessage += $"{info.DisplayName} {i + 1}号位 {(info.Latency == 0 ? "获取错误" : (info.Latency * 1000 * 2).ToString("000") + " ms")}\n";
                    i++;
                }
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkMessageTracker), "TrackSentGlobalEvent")]
        public static bool NetworkMessageTrackerTrackSentGlobalEventPatch(MessageType type)
        {
            return type != UsersMessageType;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkMessageTracker), "TrackReceivedGlobalEvent")]
        public static bool NetworkMessageTrackerTrackReceivedGlobalEventPatch(MessageType type)
        {
            return type != UsersMessageType;
        }
    }

    public class UsersMessage : Serialisable
    {
        public List<UserConnectionInfo> UserConnections { get; private set; }

        public UsersMessage(List<UserConnectionInfo> userConnections)
        {
            UserConnections = userConnections;
        }

        public void Serialise(BitStreamWriter writer)
        {
            writer.Write((uint)UserConnections.Count, 4); // 使用 4 位来写入用户数量
            foreach (var userConnection in UserConnections)
            {
                writer.Write(userConnection.DisplayName, Encoding.UTF8);
                writer.Write(userConnection.Latency);
                writer.Write(userConnection.SteamId.m_SteamID, 64); ; // 写入 SteamID
            }
        }

        public bool Deserialise(BitStreamReader reader)
        {
            int count = (int)reader.ReadUInt32(4); // 使用 4 位来读取用户数量
            UserConnections = new List<UserConnectionInfo>();

            for (int i = 0; i < count; i++)
            {
                string displayName = reader.ReadString(Encoding.UTF8);
                float latency = reader.ReadFloat32();
                ulong steamId = (ulong)reader.ReadUInt64(64); // 读取 SteamID
                CSteamID cSteamId = new CSteamID(steamId);
                UserConnections.Add(new UserConnectionInfo(displayName, latency, cSteamId));
            }

            return true;
        }
    }


    public class UserConnectionInfo
    {
        public string DisplayName { get; private set; }
        public float Latency { get; private set; }
        public CSteamID SteamId { get; private set; }

        public UserConnectionInfo(string displayName, float latency, CSteamID steamId)
        {
            DisplayName = displayName;
            Latency = latency;
            SteamId = steamId;
        }
    }

}
