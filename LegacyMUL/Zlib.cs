using System;
using System.Runtime.InteropServices;

// Stolen from Mythic Package Editor
namespace LegacyMUL
{
    /// <summary>
    /// Zlib compression quality.
    /// </summary>
    public enum ZLibQuality
    {
        /// <summary></summary>
        None = 0,

        /// <summary></summary>
        Speed = 1,

        /// <summary></summary>
        Best = 9,

        /// <summary></summary>
        Default = -1,
    }

    /// <summary>
    /// Zlib error.
    /// </summary>
    public enum ZLibError
    {
        /// <summary></summary>
        Okay = 0,

        /// <summary></summary>
        StreamEnd = 1,

        /// <summary></summary>
        NeedDictionary = 2,

        /// <summary></summary>
        FileError = -1,

        /// <summary></summary>
        StreamError = -2,

        /// <summary></summary>
        DataError = -3,

        /// <summary></summary>
        MemoryError = -4,

        /// <summary></summary>
        BufferError = -5,

        /// <summary></summary>
        VersionError = -6,
    }

    /// <summary>
    /// Compression library.
    /// </summary>
    public class Zlib
    {
        #region Version
        [DllImport("Zlib64", EntryPoint = "zlibVersion")]
        private static extern string zlibVersion64();

        [DllImport("Zlib32", EntryPoint = "zlibVersion")]
        private static extern string zlibVersion();

        /// <summary>
        /// Version of the library.
        /// </summary>
        public static string Version
        {
            get
            {
                if (Environment.Is64BitProcess)
                    return zlibVersion64();

                return zlibVersion();
            }
        }
        #endregion

        #region Decompress
        [DllImport("Zlib64", EntryPoint = "uncompress")]
        private static extern ZLibError uncompress64(byte[] dest, ref int destLen, byte[] source, int sourceLen);

        [DllImport("Zlib32", EntryPoint = "uncompress")]
        private static extern ZLibError uncompress(byte[] dest, ref int destLen, byte[] source, int sourceLen);

        /// <summary>
        /// Decompresses array of bytes.
        /// </summary>
        /// <param name="dest">Destination byte array.</param>
        /// <param name="destLength">Destination length (Sets it).</param>
        /// <param name="source">Source byte array.</param>
        /// <param name="sourceLength">Source length.</param>
        /// <returns>Error</returns>
        public static ZLibError Decompress(byte[] dest, ref int destLength, byte[] source, int sourceLength)
        {
            if (Environment.Is64BitProcess)
                return uncompress64(dest, ref destLength, source, sourceLength);

            return uncompress(dest, ref destLength, source, sourceLength);
        }
        #endregion

        #region Compress
        [DllImport("Zlib64", EntryPoint = "compress")]
        private static extern ZLibError compress64(byte[] dest, ref int destLen, byte[] source, int sourceLen);

        [DllImport("Zlib32", EntryPoint = "compress")]
        private static extern ZLibError compress(byte[] dest, ref int destLen, byte[] source, int sourceLen);

        /// <summary>
        /// Compresses array of bytes.
        /// </summary>
        /// <param name="dest">Destination byte array.</param>
        /// <param name="destLength">Destination length (Sets it).</param>
        /// <param name="source">Source byte array.</param>
        /// <param name="sourceLength">Source length.</param>
        /// <returns><see cref="ZLibError.Okay"/> if okay.</returns>
        public static ZLibError Compress(byte[] dest, ref int destLength, byte[] source, int sourceLength)
        {
            if (Environment.Is64BitProcess)
                return compress64(dest, ref destLength, source, sourceLength);

            return compress(dest, ref destLength, source, sourceLength);
        }

        [DllImport("Zlib64", EntryPoint = "compress2")]
        private static extern ZLibError compress64(byte[] dest, ref int destLen, byte[] source, int sourceLen, ZLibQuality quality);

        [DllImport("Zlib32", EntryPoint = "compress2")]
        private static extern ZLibError compress(byte[] dest, ref int destLen, byte[] source, int sourceLen, ZLibQuality quality);

        /// <summary>
        /// Compresses array of bytes.
        /// </summary>
        /// <param name="dest">Destination byte array.</param>
        /// <param name="destLength">Destination length (Sets it).</param>
        /// <param name="source">Source byte array.</param>
        /// <param name="sourceLength">Source length.</param>
        /// <param name="quality"><see cref="ZLibQuality"/> of compression.</param>
        /// <returns><see cref="ZLibError.Okay"/> if okay.</returns>
        public static ZLibError Compress(byte[] dest, ref int destLength, byte[] source, int sourceLength, ZLibQuality quality)
        {
            if (Environment.Is64BitProcess)
                return compress64(dest, ref destLength, source, sourceLength, quality);

            return compress(dest, ref destLength, source, sourceLength, quality);
        }
        #endregion
    }
}
