using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch(typeof(Resources), nameof(Resources.UnloadUnusedAssets))]
    public static class GCPatches
    {
        // УДАЛЕНО: private delegate Vector3 GetMoveDirDelegate(Character character);
        // ОСТАВЛЯЕМ: Делегат для метода нужен, так как CreateDelegate возвращает общий Delegate
        private delegate bool InAttackDelegate(Player player);

        // ✅ ИСПРАВЛЕНО: Используем родной тип делегата Harmony для полей
        private static AccessTools.FieldRef<Character, Vector3> _getMoveDirFast;

        private static InAttackDelegate _inAttackFast;
        private static bool _delegatesInitialized = false;

        static GCPatches()
        {
            try
            {
                var moveDirField = AccessTools.Field(typeof(Character), "m_moveDir");
                var inAttackMethod = AccessTools.Method(typeof(Player), "InAttack");

                if (moveDirField != null && inAttackMethod != null)
                {
                    // ✅ ИСПРАВЛЕНО: Типы теперь совпадают
                    _getMoveDirFast = AccessTools.FieldRefAccess<Character, Vector3>(moveDirField);

                    // Для методов все остается как было (CreateDelegate требует явного приведения)
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
                if (!localPlayer.IsOnGround() ||
                    localPlayer.IsSwimming() ||
                    localPlayer.IsTeleporting())
                {
                    return false;
                }

                // Использование остается прежним - синтаксис вызова такой же
                Vector3 dir = _getMoveDirFast(localPlayer);
                if (dir.sqrMagnitude > 0.01f)
                    return false;

                bool attacking = _inAttackFast(localPlayer);
                if (attacking)
                    return false;
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning($"[GC] Error checking player state: {e.Message}");
                return true;
            }

            return true;
        }
    }
}