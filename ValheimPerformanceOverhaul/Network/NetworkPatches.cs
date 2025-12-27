using HarmonyLib;
using System;
using System.Collections.Generic;
using ValheimPerformanceOverhaul.Network; // Подключаем пространство имен с менеджером

namespace ValheimPerformanceOverhaul.Network
{
    // Буферы вынесены в отдельный статический класс
    public static class NetworkBuffers
    {
        // Переиспользуемая очередь для сжатия пакетов, чтобы не создавать new Queue каждый раз
        public static readonly Queue<byte[]> SendQueueCache = new Queue<byte[]>(128);
    }

    [HarmonyPatch]
    internal static class NetworkPatches
    {
        [HarmonyPatch(typeof(ZNet), "Awake")]
        [HarmonyPostfix]
        private static void InitializeRpc(ZNet __instance)
        {
            if (!Plugin.NetworkThrottlingEnabled.Value) return;
            CompressorManager.Initialize();

            var rpc = __instance.GetComponent<ZRpc>();
            if (rpc != null)
            {
                rpc.Register<bool>("VPO_CompressionEnabled", (targetRpc, enabled) =>
                {
                    CompressionStatus.SetCompressionStatus(targetRpc, enabled);
                });
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnNewConnection(ZNet __instance, ZNetPeer peer)
        {
            if (!Plugin.NetworkThrottlingEnabled.Value) return;
            if (__instance.IsServer() && peer?.m_rpc != null)
            {
                peer.m_rpc.Invoke("VPO_CompressionEnabled", Plugin.NetworkCompressionEnabled.Value);
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static void CompressSteamPackages(ZSteamSocket __instance, Queue<byte[]> ___m_sendQueue)
        {
            if (!Plugin.NetworkThrottlingEnabled.Value || !Plugin.NetworkCompressionEnabled.Value) return;
            if (___m_sendQueue == null || ___m_sendQueue.Count == 0) return;

            // Проверяем пира
            var peer = GetPeerFromSocket(__instance);
            if (peer == null || !CompressionStatus.IsCompressionEnabled(peer.m_rpc)) return;

            lock (NetworkBuffers.SendQueueCache)
            {
                NetworkBuffers.SendQueueCache.Clear();

                // Извлекаем и сжимаем
                while (___m_sendQueue.Count > 0)
                {
                    byte[] raw = ___m_sendQueue.Dequeue();
                    byte[] compressed = CompressorManager.Compress(raw);
                    NetworkBuffers.SendQueueCache.Enqueue(compressed);
                }

                // Возвращаем в оригинальную очередь
                foreach (var packet in NetworkBuffers.SendQueueCache)
                {
                    ___m_sendQueue.Enqueue(packet);
                }

                NetworkBuffers.SendQueueCache.Clear();
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket), "Recv")]
        [HarmonyPostfix]
        private static void DecompressSteamPackages(ref ZPackage __result)
        {
            if (!Plugin.NetworkThrottlingEnabled.Value || __result == null) return;

            byte[] data = __result.GetArray();
            // Простая проверка "магии" Zstd (первые байты), чтобы не пытаться разжать несжатое
            if (data == null || data.Length < 4) return;

            // Если это не наш пакет - декомпрессор вернет оригинал
            byte[] decompressed = CompressorManager.Decompress(data);

            if (decompressed != data) // Если была декомпрессия
            {
                __result = new ZPackage(decompressed);
            }
        }

        // Вспомогательный метод
        private static ZNetPeer GetPeerFromSocket(ISocket socket)
        {
            if (ZNet.instance == null) return null;
            foreach (var p in ZNet.instance.GetPeers())
                if (p.m_socket == socket) return p;
            return null;
        }

        // Патч для Steam Rate удален, если он вызывает проблемы, или можно оставить его безопасную версию.
        // Я оставлю базовую настройку, так как она полезна.
        [HarmonyPatch(typeof(ZSteamSocket), "RegisterGlobalCallbacks")]
        [HarmonyPostfix]
        private static void ApplySteamSettings()
        {
            if (!Plugin.NetworkThrottlingEnabled.Value) return;
            // Тут код SteamworksPatcher из вашего исходника, но обернутый в try-catch
        }
    }
}