using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ZstdSharp;

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
        private static Compressor _compressor;
        private static Decompressor _decompressor;
        private static byte[] _dictBytes;
        private static bool _isInitialized = false;

        // ✅ НОВОЕ: Настройки сжатия
        private static int _compressionThreshold = 128; // Минимальный размер для сжатия
        private static float _minCompressionRatio = 0.95f; // Минимальная эффективность сжатия

        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _compressor = new Compressor(1); // Уровень 1 - быстрый
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
                            _compressor.LoadDictionary(_dictBytes);
                            _decompressor.LoadDictionary(_dictBytes);
                            Plugin.Log.LogInfo("[Network] Zstd compressor initialized with dictionary.");
                        }
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[Network] Zstd dictionary not found. Compressor initialized without it.");
                    }
                }

                // Загружаем настройки из конфига
                _compressionThreshold = Plugin.NetworkCompressionThreshold?.Value ?? 128;

                _isInitialized = true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Failed to initialize Zstd compressor: {e}");
            }
        }

        public static byte[] Compress(byte[] data)
        {
            if (!_isInitialized || _compressor == null || data == null || data.Length == 0)
                return data;

            // ✅ ИСПРАВЛЕНО: Проверяем размер перед сжатием
            if (data.Length < _compressionThreshold)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[Network] Skipping compression for small packet ({data.Length} bytes)");
                return data;
            }

            try
            {
                byte[] compressed = _compressor.Wrap(new ReadOnlySpan<byte>(data)).ToArray();

                // ✅ ИСПРАВЛЕНО: Проверяем эффективность сжатия
                float compressionRatio = (float)compressed.Length / data.Length;

                if (compressionRatio >= _minCompressionRatio)
                {
                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Network] Compression ineffective ({compressionRatio:F2}), using original data");
                    return data;
                }

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[Network] Compressed {data.Length} → {compressed.Length} bytes (ratio: {compressionRatio:F2})");

                return compressed;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Compression failed: {e.Message}");
                return data;
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            if (!_isInitialized || _decompressor == null || data == null || data.Length < 4)
                return data;

            // Проверяем магическое число Zstd
            uint magic = (uint)((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
            if (magic != 0xFD2FB528)
                return data; // Не сжатые данные

            try
            {
                byte[] decompressed = _decompressor.Unwrap(new ReadOnlySpan<byte>(data)).ToArray();

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[Network] Decompressed {data.Length} → {decompressed.Length} bytes");

                return decompressed;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Decompression failed: {e.Message}");
                return data;
            }
        }
    }
}