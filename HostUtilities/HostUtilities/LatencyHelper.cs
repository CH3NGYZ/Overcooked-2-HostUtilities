using BitStream;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Team17.Online.Multiplayer.Messaging;
using Team17.Online.Multiplayer;
using System.Reflection;
using BepInEx.Configuration;
using Steamworks;
using Team17.Online.Multiplayer.Connection;
using Team17.Online;
using UnityEngine;
using System.Text.RegularExpressions;


namespace HostUtilities
{
    public class LatencyHelper
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);

        public static Harmony HarmonyInstance { get; set; }
        public static List<UserConnectionInfo> UserConnections { get; set; } = new List<UserConnectionInfo>();
        public const MessageType UsersMessageType = (MessageType)69;
        private const float BroadcastIntervalSeconds = 0.1f;
        private static float broadcastTimer = 0.0f;

        public static string serverFriendsMessage = String.Empty;
        public static string clientFriendsMessage = String.Empty;
        public static Server localServer = null;
        public static bool usersChanged = false;
        public static void Awake()
        {
            HarmonyInstance = Harmony.CreateAndPatchAll(MethodBase.GetCurrentMethod().DeclaringType);
            MODEntry.AllHarmony[MethodBase.GetCurrentMethod().DeclaringType.Name] = HarmonyInstance;
        }

        public static void Update()
        {
            broadcastTimer += Time.deltaTime;
            if (broadcastTimer >= BroadcastIntervalSeconds)
            {
                if (MODEntry.isHost)
                {
                    try
                    {
                        MultiplayerController multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                        Server server = multiplayerController.m_LocalServer;
                        Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection> remoteClientConnectionsDict = server.m_RemoteClientConnections;

                        if (server != null)
                        {
                            foreach (User user in ServerUserSystem.m_Users._items.Skip(1))
                            {
                                foreach (var kvp in remoteClientConnectionsDict)
                                {
                                    IOnlineMultiplayerSessionUserId sessionUserId = kvp.Key;
                                    NetworkConnection connection = kvp.Value;
                                    if (user.m_PlatformID.m_steamId == sessionUserId.PlatformUserId.m_steamId)
                                    {
                                        float latency = connection.GetConnectionStats(bReliable: false).m_fLatency;
                                        UserConnections.Add(new UserConnectionInfo(user.DisplayName, latency, user.PlatformID.m_steamId));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
                try
                {
                    if (UserConnections.Count > 0)
                    {
                        //foreach (var info in UserConnections)
                        //{
                        //    LogE($"{info.DisplayName} - {info.SteamId} - {info.Latency}ms");
                        //}
                        localServer?.BroadcastMessageToAll(UsersMessageType, new UsersMessage(UserConnections));
                        UserConnections.Clear();
                    }
                }
                catch (Exception) { }
                UserConnections.Clear();
                broadcastTimer = 0.0f;
            }
        }

        public static void OnUsersMessage(UsersMessage message)
        {
            if (!MODEntry.isHost)
            {
                //foreach (var info in message.UserConnections)
                //{
                //    LogW($"OnUsersMessage  {info.SteamId}  {info.Latency}  {info.DisplayName}");
                //}
                //LogW($"------------------------------------------");

                serverFriendsMessage = string.Empty;
                FastList<User> clientUserSystem = ClientUserSystem.m_Users;
                for (int i = 0; i < clientUserSystem.Count; i++)
                {
                    User user = clientUserSystem._items[i];
                    CSteamID csteamID = user.PlatformID.m_steamId;

                    string isfriendMessage = string.Empty;
                    if (EFriendRelationship.k_EFriendRelationshipFriend == SteamFriends.GetFriendRelationship(csteamID))
                    {
                        // 朋友
                        string personaName = SteamFriends.GetFriendPersonaName(csteamID);
                        string nickname = SteamFriends.GetPlayerNickname(csteamID);
                        string nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" [{nickname}]";
                        isfriendMessage = $"(好友 {personaName}{nicknamePart})";
                    }

                    if (i == 0)
                    {
                        // 先显示主机
                        foreach (var info in message.UserConnections)
                        {
                            if (info.SteamId == (CSteamID)MODEntry.CurrentSteamID.m_SteamID)
                            {
                                serverFriendsMessage += $"{(UI_DisplayLatency.simplifyLatency.Value == true ? "" : user.DisplayName.RemoveAllTags())} {isfriendMessage} {i + 1}号位 {(info.Latency == 0 ? "获取错误" : (info.Latency * 1000 * 2).ToString("000"))} ms\n";
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 再显示非主机
                        foreach (var info in message.UserConnections)
                        {
                            if (info.SteamId == csteamID)
                            {
                                // 获取消息中的延迟并添加
                                if (info.SteamId == (CSteamID)MODEntry.CurrentSteamID.m_SteamID)
                                {
                                    //跳过自己
                                    continue;
                                }
                                else
                                {
                                    // 其他两位
                                    serverFriendsMessage += $"{(UI_DisplayLatency.simplifyLatency.Value == true ? "" : info.DisplayName.RemoveAllTags())} {isfriendMessage} {i + 1}号位 {(info.Latency == 0 ? "获取错误" : (info.Latency * 1000 * 2).ToString("000"))} ms\n";
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Message), "Deserialise")]
        public static bool Message_Deserialise_Patch(BitStreamReader reader, Message __instance, ref bool __result, ref bool __runOriginal)
        {
            if (!__runOriginal) return false;
            var messageType = (MessageType)reader.ReadByteAhead(8);
            if (messageType == UsersMessageType)
            {
                __instance.Type = (MessageType)reader.ReadByte(8);
                __instance.Payload = new UsersMessage(new List<UserConnectionInfo>());
                __instance.Payload.Deserialise(reader);
                __result = true;
                return false;
            }
            return true;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(ServerMessenger), "OnServerStarted")]
        public static void ServerMessenger_OnServerStarted_Postfix(Server server)
        {
            localServer = server;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ServerMessenger), "OnServerStopped")]
        public static void ServerMessenger_OnServerStopped_Postfix()
        {
            localServer = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mailbox), "OnMessageReceived")]
        public static bool Mailbox_OnMessageReceived_Prefix(MessageType type, Serialisable message)
        {
            if (type == UsersMessageType)
            {
                if (message is UsersMessage usersMessage)
                {
                    OnUsersMessage(usersMessage);
                    return false;
                }
            }
            return true;
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClientUserSystem), "AddUser")]
        public static void ClientUserSystem_OnUsersChanged_Postfix()
        {
            Log("ClientUserSystem_OnUsersChanged");
            clientFriendsMessage = string.Empty;
            FastList<User> clientUserSystem = ClientUserSystem.m_Users;
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
                    string isfriendMessage = $"(好友 {personaName}{nicknamePart})";
                    clientFriendsMessage += $"{(UI_DisplayLatency.simplifyLatency.Value == true ? "" : user.DisplayName.RemoveAllTags())} {isfriendMessage} {i + 1}号位\n";
                }
                //else
                //{
                //    Log($"{user.DisplayName} 不是好友");
                //}
            }
        }
    }
    public static class BitStreamReader_ReadByteAhead
    {
        public static byte ReadByteAhead(this BitStreamReader instance, int countOfBits)
        {
            if (instance.EndOfStream) return 0;
            if (countOfBits > 8 || countOfBits <= 0) return 0;
            if (countOfBits > instance._bufferLengthInBits) return 0;
            byte b;

            int cbitsInPartialByte = instance._cbitsInPartialByte;
            byte partialByte = instance._partialByte;
            if (cbitsInPartialByte >= countOfBits)
            {
                int num = 8 - countOfBits;
                b = (byte)(partialByte >> num);
            }
            else
            {
                byte[] byteArray = instance._byteArray;
                byte b2 = byteArray[instance._byteArrayIndex];
                int num2 = 8 - countOfBits;
                b = (byte)(partialByte >> num2);
                int num3 = num2 + cbitsInPartialByte;
                b |= (byte)(b2 >> num3);
            }
            return b;
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

    public static class StringExtensions
    {
        public static string RemoveAllTags(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            string pattern = "<.*?>";
            string replacement = "";
            string result = Regex.Replace(input, pattern, replacement);
            return result;
        }
    }
}
