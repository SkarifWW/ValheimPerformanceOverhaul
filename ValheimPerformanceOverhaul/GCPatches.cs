using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch(typeof(Resources), nameof(Resources.UnloadUnusedAssets))]
    public static class GCPatches
    {
        private static FieldInfo _moveDirField;
        private static MethodInfo _inAttackMethod;
        private static bool _fieldsFound = false;

        static GCPatches()
        {
            try
            {
                _moveDirField = AccessTools.Field(typeof(Character), "m_moveDir");
                _inAttackMethod = AccessTools.Method(typeof(Player), "InAttack");
                _fieldsFound = (_moveDirField != null && _inAttackMethod != null);

                if (!_fieldsFound)
                    Plugin.Log.LogWarning("[GC] Could not find private fields. GC optimization disabled safely.");
            }
            catch
            {
                _fieldsFound = false;
            }
        }

        [HarmonyPrefix]
        private static bool PreventGCWhenPlayerIsBusy()
        {
            if (!Plugin.GcControlEnabled.Value || !_fieldsFound) return true;

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.IsDead()) return true;

            // Проверяем публичные методы (безопасно)
            if (!localPlayer.IsOnGround() || localPlayer.IsSwimming() || localPlayer.IsTeleporting())
                return false;

            // Проверяем приватные через рефлексию (быстро, т.к. поля закэшированы)
            try
            {
                Vector3 dir = (Vector3)_moveDirField.GetValue(localPlayer);
                if (dir.sqrMagnitude > 0.01f) return false;

                bool attacking = (bool)_inAttackMethod.Invoke(localPlayer, null);
                if (attacking) return false;
            }
            catch
            {
                // При ошибке - разрешаем GC
                return true;
            }

            return true;
        }
    }
}