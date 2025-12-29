using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch(typeof(Resources), nameof(Resources.UnloadUnusedAssets))]
    public static class GCPatches
    {
        // ✅ ИСПРАВЛЕНО: Используем делегаты вместо Reflection
        private delegate Vector3 GetMoveDirDelegate(Character character);
        private delegate bool InAttackDelegate(Player player);

        private static GetMoveDirDelegate _getMoveDirFast;
        private static InAttackDelegate _inAttackFast;
        private static bool _delegatesInitialized = false;

        static GCPatches()
        {
            try
            {
                // Создаем быстрые делегаты через HarmonyLib
                var moveDirField = AccessTools.Field(typeof(Character), "m_moveDir");
                var inAttackMethod = AccessTools.Method(typeof(Player), "InAttack");

                if (moveDirField != null && inAttackMethod != null)
                {
                    // Создаем делегат для чтения поля (в 100+ раз быстрее Reflection)
                    _getMoveDirFast = AccessTools.FieldRefAccess<Character, Vector3>(moveDirField);

                    // Создаем делегат для метода
                    _inAttackFast = (InAttackDelegate)System.Delegate.CreateDelegate(
                        typeof(InAttackDelegate),
                        inAttackMethod
                    );

                    _delegatesInitialized = true;
                    Plugin.Log.LogInfo("[GC] Fast delegates initialized successfully.");
                }
                else
                {
                    Plugin.Log.LogWarning("[GC] Could not find private fields. GC optimization disabled.");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[GC] Failed to initialize delegates: {e.Message}");
                _delegatesInitialized = false;
            }
        }

        [HarmonyPrefix]
        private static bool PreventGCWhenPlayerIsBusy()
        {
            if (!Plugin.GcControlEnabled.Value || !_delegatesInitialized)
                return true;

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.IsDead())
                return true;

            try
            {
                // Проверяем публичные методы (безопасно)
                if (!localPlayer.IsOnGround() ||
                    localPlayer.IsSwimming() ||
                    localPlayer.IsTeleporting())
                {
                    return false; // Блокируем GC
                }

                // ✅ ИСПРАВЛЕНО: Используем быстрые делегаты вместо Reflection
                // Старый код: Vector3 dir = (Vector3)_moveDirField.GetValue(localPlayer); // МЕДЛЕННО!
                Vector3 dir = _getMoveDirFast(localPlayer); // В 100+ раз быстрее!
                if (dir.sqrMagnitude > 0.01f)
                    return false;

                // Старый код: bool attacking = (bool)_inAttackMethod.Invoke(localPlayer, null); // МЕДЛЕННО!
                bool attacking = _inAttackFast(localPlayer); // В 100+ раз быстрее!
                if (attacking)
                    return false;
            }
            catch (System.Exception e)
            {
                // При ошибке - разрешаем GC для стабильности
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning($"[GC] Error checking player state: {e.Message}");
                return true;
            }

            return true; // Игрок не занят - разрешаем GC
        }
    }
}