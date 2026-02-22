/*
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

                private const byte HEADER_UNCOMPRESSED = 0x00;
        private const byte HEADER_COMPRESSED = 0x01;
        private const byte HEADER_LEGACY = 0xFF; 
        private static int _compressionThreshold = 128;
        private static float _minCompressionRatio = 0.95f;

        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _compressor = new Compressor(1);                 _decompressor = new Decompressor();

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

                        if (data.Length < _compressionThreshold)
            {
                                var uncompressed = new byte[data.Length + 1];
                uncompressed[0] = HEADER_UNCOMPRESSED;
                Buffer.BlockCopy(data, 0, uncompressed, 1, data.Length);

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[Network] Skipping compression for small packet ({data.Length} bytes)");

                return uncompressed;
            }

            try
            {
                byte[] compressed = _compressor.Wrap(new ReadOnlySpan<byte>(data)).ToArray();

                                float compressionRatio = (float)(compressed.Length + 1) / data.Length; 
                if (compressionRatio >= _minCompressionRatio)
                {
                                        var uncompressed = new byte[data.Length + 1];
                    uncompressed[0] = HEADER_UNCOMPRESSED;
                    Buffer.BlockCopy(data, 0, uncompressed, 1, data.Length);

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Network] Compression ineffective ({compressionRatio:F2}), using original data");

                    return uncompressed;
                }

                                var result = new byte[compressed.Length + 1];
                result[0] = HEADER_COMPRESSED;
                Buffer.BlockCopy(compressed, 0, result, 1, compressed.Length);

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[Network] Compressed {data.Length} → {result.Length} bytes (ratio: {compressionRatio:F2})");

                return result;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Compression failed: {e.Message}");

                                var fallback = new byte[data.Length + 1];
                fallback[0] = HEADER_UNCOMPRESSED;
                Buffer.BlockCopy(data, 0, fallback, 1, data.Length);
                return fallback;
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            if (!_isInitialized || _decompressor == null || data == null || data.Length < 1)
                return data;

            try
            {
                byte header = data[0];

                                if (header == HEADER_UNCOMPRESSED)
                {
                                        var result = new byte[data.Length - 1];
                    Buffer.BlockCopy(data, 1, result, 0, result.Length);

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Network] Uncompressed packet received: {result.Length} bytes");

                    return result;
                }
                else if (header == HEADER_COMPRESSED)
                {
                                        var compressed = new byte[data.Length - 1];
                    Buffer.BlockCopy(data, 1, compressed, 0, compressed.Length);

                    byte[] decompressed = _decompressor.Unwrap(new ReadOnlySpan<byte>(compressed)).ToArray();

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Network] Decompressed {data.Length} → {decompressed.Length} bytes");

                    return decompressed;
                }
                else
                {
                                                            if (data.Length >= 4)
                    {
                        uint magic = (uint)((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);

                        if (magic == 0xFD2FB528)                         {
                            byte[] decompressed = _decompressor.Unwrap(new ReadOnlySpan<byte>(data)).ToArray();

                            if (Plugin.DebugLoggingEnabled.Value)
                                Plugin.Log.LogInfo($"[Network] Legacy compressed packet: {data.Length} → {decompressed.Length} bytes");

                            return decompressed;
                        }
                    }

                                        if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogWarning($"[Network] Unknown packet format, header: 0x{header:X2}");

                    return data;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Network] Decompression failed: {e.Message}");
                return data;             }
        }

                private static long _totalBytesIn = 0;
        private static long _totalBytesOut = 0;
        private static long _totalPackets = 0;

        public static void LogCompressionStats()
        {
            if (!Plugin.DebugLoggingEnabled.Value || _totalPackets == 0) return;

            float ratio = (float)_totalBytesOut / _totalBytesIn;
            Plugin.Log.LogInfo($"[Network] Compression Stats: {_totalPackets} packets, {_totalBytesIn} → {_totalBytesOut} bytes (ratio: {ratio:F2})");
        }

        public static void ResetStats()
        {
            _totalBytesIn = 0;
            _totalBytesOut = 0;
            _totalPackets = 0;
        }
    }
}*/