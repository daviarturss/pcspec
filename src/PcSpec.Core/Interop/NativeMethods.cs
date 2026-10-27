using System.Runtime.InteropServices;
using System.Text;

namespace PcSpec.Core.Interop;

internal static class NativeMethods
{
    public const uint RsmbProvider = 0x52534D42; // 'RSMB'
    public const uint FirmwareTableId = 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetSystemFirmwareTable(uint firmwareTableProviderSignature, uint firmwareTableId, IntPtr buffer, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint EnumSystemFirmwareTables(uint firmwareTableProviderSignature, IntPtr buffer, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ulong GetActiveProcessorCount(ushort groupNumber);

    public const ushort AllProcessorGroups = 0xFFFF;

    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern int CallNtPowerInformation(int informationLevel, IntPtr inputBuffer, uint inputBufferSize, IntPtr outputBuffer, uint outputBufferSize);

    public const int ProcessorInformation = 11;

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetThreadPriority(IntPtr thread, int priority);

    public const int ThreadPriorityHighest = 2;

    /// <summary>Tipo de firmware ativo (0 = BIOS legado, 1 = UEFI).</summary>
    [DllImport("kernel32.dll")]
    public static extern bool GetFirmwareType(out uint firmwareType);

    public const int RelationProcessorCore = 0;
    public const int RelationCache = 2;
    public const int RelationProcessorPackage = 3;

    public static string DecodeAnsi(IntPtr buffer, int length) =>
        Marshal.PtrToStringAnsi(buffer, length) ?? string.Empty;

    public static string DecodeUtf16(IntPtr buffer, int length) =>
        Marshal.PtrToStringUni(buffer, length) ?? string.Empty;

    internal static Encoding SmbiosEncoding => Encoding.Latin1;
}
