using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ZstdSharp;


using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ZstdSharp;
using System;

namespace ValheimPerformanceOverhaul.Network
{
    internal static class NetworkUtils
    {
        private static readonly FieldInfo _socketField = AccessTools.Field(typeof(ZRpc), "m_socket");

        public static ISocket GetSocket(ZRpc rpc)
        {
            if (rpc == null || _socketField == null) return null;
            return (ISocket)_socketField.GetValue(rpc);
        }
    }

    internal static class CompressionStatus
    {
        private static readonly Dictionary<ZRpc, bool> _compressionEnabledPeers = new Dictionary<ZRpc, bool>();

        public static void SetCompressionStatus(ZRpc rpc, bool enabled)
        {
            if (rpc == null) return;
            _compressionEnabledPeers[rpc] = enabled;
            if (Plugin.DebugLoggingEnabled.Value)
            {
                var socket = NetworkUtils.GetSocket(rpc);
                if (socket != null)
                {
                    Plugin.Log.LogInfo($"[Network] Compression for peer {socket.GetHostName()} set to: {enabled}");
                }
            }
        }

        public static bool IsCompressionEnabled(ZRpc rpc)
        {
            if (rpc == null) return false;
            return _compressionEnabledPeers.TryGetValue(rpc, out var enabled) && enabled;
        }

        public static void OnPeerDisconnected(ZRpc rpc)
        {
            if (rpc == null) return;
            _compressionEnabledPeers.Remove(rpc);
        }
    }

    internal static class CompressorManager
    {
        // --- ИСПРАВЛЕНИЕ 3: Возвращаемся к созданию экземпляров, но используем DictBase ---
        private static Compressor _compressor;
        private static Decompressor _decompressor;
        private static byte[] _dictBytes; // Храним байты словаря
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Создаем экземпляры без словаря сразу
                _compressor = new Compressor(1); // Уровень сжатия 1 - быстрый
                _decompressor = new Decompressor();

                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("ValheimPerformanceOverhaul.Resources.dict.small"))
                {
                    if (stream != null)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            _dictBytes = memoryStream.ToArray();
                            // Загружаем словарь в существующие экземпляры
                            _compressor.LoadDictionary(_dictBytes);
                            _decompressor.LoadDictionary(_dictBytes);
                            Plugin.Log.LogInfo("[Network] Zstd compressor initialized with dictionary.");
                        }
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[Network] Zstd dictionary not found. Compressor initialized without it (lower efficiency).");
                    }
                }
                _isInitialized = true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Failed to initialize Zstd compressor: {e}");
            }
        }

        public static byte[] Compress(byte[] data)
        {
            if (!_isInitialized || _compressor == null || data == null || data.Length == 0) return data;
            // Используем метод Wrap экземпляра
            return _compressor.Wrap(new ReadOnlySpan<byte>(data)).ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            if (!_isInitialized || _decompressor == null || data == null || data.Length < 4) return data;

            uint magic = (uint)((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
            if (magic != 0xFD2FB528) return data;

            // Используем метод Unwrap экземпляра
            return _decompressor.Unwrap(new ReadOnlySpan<byte>(data)).ToArray();
        }
    }
}
