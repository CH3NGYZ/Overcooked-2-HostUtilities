﻿using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static ClientPortalMapNode;

namespace HostUtilities
{
    public class UI_DisplayKickedUser
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);

        private static MyOnScreenDebugDisplayKickedUser onScreenDebugDisplayKickedUser;
        private static MyKickedUserCounter kickedUserCounter = null;
        public static ConfigEntry<bool> ShowKickedUserEnabled;

        public static void Add_m_Text(string str) => kickedUserCounter?.Add_m_Text(str);

        public static void Awake()
        {
            ShowKickedUserEnabled = MODEntry.Instance.Config.Bind<bool>("00-UI", "02-街机大厅内显示自动踢的黑名单玩家", true);
            onScreenDebugDisplayKickedUser = new MyOnScreenDebugDisplayKickedUser();
            onScreenDebugDisplayKickedUser.Awake();
        }


        public static void Update()
        {
            onScreenDebugDisplayKickedUser.Update();

            if ((!MODEntry.isInLobby || !MODEntry.isHost || Input.GetKeyDown(KeyCode.End)) && kickedUserCounter != null)
                RemoveKickedUserCounter();
            else if (((MODEntry.isInLobby && MODEntry.isHost && ShowKickedUserEnabled.Value) || Input.GetKeyDown(KeyCode.Home)) && kickedUserCounter == null)
                AddKickedUserCounter();
        }

        public static void OnGUI() => onScreenDebugDisplayKickedUser.OnGUI();

        private static void AddKickedUserCounter()
        {
            kickedUserCounter = new MyKickedUserCounter();
            onScreenDebugDisplayKickedUser.AddDisplay(kickedUserCounter);
            kickedUserCounter.Init_m_Text();
        }

        private static void RemoveKickedUserCounter()
        {
            onScreenDebugDisplayKickedUser.RemoveDisplay(kickedUserCounter);
            kickedUserCounter.OnDestroy();
            kickedUserCounter = null;
        }

        public class MyKickedUserCounter : DebugDisplay
        {
            private bool replaced = false;

            public void Init_m_Text() => m_Text = "暂时没有自动移除的账号";

            public void Add_m_Text(string str)
            {
                if (replaced) m_Text += "\n" + str;
                else { m_Text = str; replaced = true; }
            }

            public override void OnSetUp() { }

            public override void OnUpdate() { }

            public override void OnDraw(ref Rect rect, GUIStyle style) => base.DrawText(ref rect, style, m_Text);

            private static string m_Text = string.Empty;
        }

        private class MyOnScreenDebugDisplayKickedUser
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
                m_GUIStyle.alignment = TextAnchor.UpperLeft;
                m_GUIStyle.fontSize = MODEntry.defaultFontSize.Value;
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
        private static Color HexToColor(string hex)
        {
            Color color = new Color();
            ColorUtility.TryParseHtmlString(hex, out color);
            return color;
        }
    }
}
