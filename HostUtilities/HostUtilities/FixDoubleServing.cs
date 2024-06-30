using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
namespace HostUtilities
{
    public class FixDoubleServing
    {
        public static void Log(string mes) => MODEntry.LogInfo(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogE(string mes) => MODEntry.LogError(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static void LogW(string mes) => MODEntry.LogWarning(MethodBase.GetCurrentMethod().DeclaringType.Name, mes);
        public static Harmony HarmonyInstance { get; set; }
        public static ConfigEntry<bool> isDoubleServingBanned;
        public static ConfigEntry<bool> isOneRecipeDoubleServingAllowed;
        public static bool isOnlyOneRecipe = false;
        public static string levelName = string.Empty;
        public static void Awake()
        {
            isDoubleServingBanned = MODEntry.Instance.Config.Bind("01-功能开关", "01-00-禁止卡盘", true, "禁止卡盘子");
            isOneRecipeDoubleServingAllowed = MODEntry.Instance.Config.Bind("01-功能开关", "01-01-只有一个食谱时允许卡盘", false, "只有一个食谱时允许卡盘子");
            HarmonyInstance = Harmony.CreateAndPatchAll(MethodBase.GetCurrentMethod().DeclaringType);
            MODEntry.AllHarmony[MethodBase.GetCurrentMethod().DeclaringType.Name] = HarmonyInstance;
        }

        private static bool skipNext = false;


        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerPlateStation), "DeliverCurrentPlate")]
        private static void DeliverCurrentPlate(ref ServerPlateStation __instance, ref ServerPlate ___m_plate, ref IKitchenOrderHandler ___m_orderHandler)
        {
            try
            {
                if (isDoubleServingBanned.Value && ___m_plate.IsReserved())
                {
                    skipNext = true;
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
            }
        }

        [HarmonyPatch(typeof(ServerKitchenFlowControllerBase), "FoodDelivered")]
        [HarmonyPrefix]
        static bool FoodDelivered(ref AssembledDefinitionNode _definition, ref PlatingStepData _plateType, ref ServerPlateStation _station)
        {
            try
            {
                if (skipNext)
                {
                    skipNext = false;
                    if (isOneRecipeDoubleServingAllowed.Value && isOnlyOneRecipe)
                    {
                        Log($"拦截到卡盘子, 但 单一菜单关 或 主线1-1 允许卡盘");
                        return true;
                    }
                    Log($"拦截到卡盘子!");
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
                LogE($"Stack trace: \n{e.StackTrace}");
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundData), "InitialiseRound")]
        static void ScriptedRoundData_InitialiseRound_Postfix(ref RoundData __instance)
        {
            try
            {
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name == "FlowManager")
                    {
                        ServerCampaignFlowController controller = obj.GetComponent<ServerCampaignFlowController>();
                        if (controller != null)
                        {
                            LevelConfigBase levelConfig = controller.GetLevelConfig();
                            if (levelConfig != null)
                            {
                                Log($"本关关卡名称: {levelConfig.name}");
                                levelName = levelConfig.name;
                            }
                        }
                    }
                }
                if (__instance.m_recipes.m_recipes.Length > 1)
                {
                    Log($"本关菜单种类有 {__instance.m_recipes.m_recipes.Length} 种");
                    isOnlyOneRecipe = false;
                }
                else
                {
                    Log("本关菜单种类有 1 种");
                    isOnlyOneRecipe = true;
                }
                for (int i = 0; i < __instance.m_recipes.m_recipes.Length; i++)
                {
                    var item = __instance.m_recipes.m_recipes[i];
                    Log($"本关菜单索引: {i}, 菜单名: {item.m_order.name}");
                }
                if (levelName.Contains("Sushi_1_1"))
                {
                    isOnlyOneRecipe = true;
                }
            }
            catch (Exception e)
            {
                LogE($"An error occurred: \n{e.Message}");
            }
        }
    }
}
