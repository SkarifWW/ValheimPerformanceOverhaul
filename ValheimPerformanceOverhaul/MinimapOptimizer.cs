using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ValheimPerformanceOverhaul;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch]
    public static class MinimapOptimizer
    {
        // Кэшируем поля через рефлексию, используя ПРАВИЛЬНЫЕ имена для новой версии игры
        private static FieldInfo _smallRootField = AccessTools.Field(typeof(Minimap), "m_smallRoot");
        private static FieldInfo _largeRootField = AccessTools.Field(typeof(Minimap), "m_largeRoot");

        // Флаг, чтобы не спамить ошибками, если поля снова изменят
        private static bool _fieldsFound = false;

        static MinimapOptimizer()
        {
            // Проверяем, нашли ли мы поля
            if (_smallRootField != null && _largeRootField != null)
            {
                _fieldsFound = true;
            }
            else
            {
                // Если не нашли новые имена, пробуем старые (на всякий случай для совместимости)
                if (_smallRootField == null) _smallRootField = AccessTools.Field(typeof(Minimap), "m_smallMapPanel");
                if (_largeRootField == null) _largeRootField = AccessTools.Field(typeof(Minimap), "m_largeMapPanel");

                _fieldsFound = (_smallRootField != null && _largeRootField != null);

                if (!_fieldsFound)
                {
                    Plugin.Log.LogWarning("[MinimapOptimizer] Could not find Minimap root fields. Optimization disabled.");
                }
            }
        }

        [HarmonyPatch(typeof(Minimap), "Update")]
        [HarmonyPrefix]
        private static bool Minimap_Update_Prefix(Minimap __instance)
        {
            // Если поля не найдены или оптимизация выключена в конфиге — выполняем оригинальный код
            if (!_fieldsFound) return true;

            // Логика оптимизации:
            // Если открыта большая карта, нет смысла обновлять маленькую и наоборот.
            // Но в оригинальном коде это уже частично учтено.

            // Мы можем добавить пропуск обновления, если игрок не двигается
            if (Player.m_localPlayer == null) return true;

            // Если карта открыта (режим Large), обновляем как обычно
            if (__instance.m_mode == Minimap.MapMode.Large) return true;

            // Если карта маленькая (Small), проверяем, нужно ли обновлять
            // Например, если мы стоим на месте и не вращаем камеру
            if (__instance.m_mode == Minimap.MapMode.Small)
            {
                // Получаем объекты UI
                GameObject smallRoot = (GameObject)_smallRootField.GetValue(__instance);

                // Если UI выключен (например, через Ctrl+F3), не тратим ресурсы
                if (smallRoot != null && !smallRoot.activeInHierarchy)
                {
                    return false; // Пропускаем Update
                }
            }

            return true;
        }

        // Патч для оптимизации отрисовки иконок карты (MapPin)
        // Вызывается часто, поэтому стоит проверить валидность
        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        [HarmonyPrefix]
        private static bool UpdateDynamicPins_Prefix(Minimap __instance)
        {
            // Если карта закрыта и миникарта выключена, не обновляем пины
            if (__instance.m_mode == Minimap.MapMode.None) return false;

            return true;
        }
    }
}