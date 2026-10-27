using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PcSpec.Core.Interop;

namespace PcSpec.Core.Smbios;

/// <summary>Uma estrutura (record) da tabela SMBIOS, com acesso seguro aos campos.</summary>
public sealed class SmbiosStructure
{
    private readonly byte[] _formatted;
    private readonly string[] _strings;

    internal SmbiosStructure(byte type, byte length, ushort handle, byte[] formatted, string[] strings)
    {
        Type = type;
        Length = length;
        Handle = handle;
        _formatted = formatted;
        _strings = strings;
    }

    public byte Type { get; }
    public byte Length { get; }
    public ushort Handle { get; }
    public IReadOnlyList<string> Strings => _strings;

    public byte Byte(int offset) => offset >= 0 && offset < _formatted.Length ? _formatted[offset] : (byte)0;

    public ushort Word(int offset) =>
        offset >= 0 && offset + 1 < _formatted.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(_formatted.AsSpan(offset))
            : (ushort)0;

    public uint Dword(int offset) =>
        offset >= 0 && offset + 3 < _formatted.Length
            ? BinaryPrimitives.ReadUInt32LittleEndian(_formatted.AsSpan(offset))
            : 0u;

    public ulong Qword(int offset) =>
        offset >= 0 && offset + 7 < _formatted.Length
            ? BinaryPrimitives.ReadUInt64LittleEndian(_formatted.AsSpan(offset))
            : 0ul;

    public bool Has(int offset) => offset >= 0 && offset < _formatted.Length;

    /// <summary>String referenciada por um campo de indice de string (0 = ausente).</summary>
    public string? String(int offset)
    {
        var index = Byte(offset);
        if (index == 0 || index > _strings.Length)
        {
            return null;
        }

        var value = _strings[index - 1];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? StringAt(int index) =>
        index >= 1 && index <= _strings.Length && !string.IsNullOrWhiteSpace(_strings[index - 1])
            ? _strings[index - 1].Trim()
            : null;

    public ReadOnlySpan<byte> Raw => _formatted;
}

/// <summary>
/// Tabela SMBIOS/DMI lida de <c>GetSystemFirmwareTable('RSMB')</c>.
/// E a mesma fonte que o CPU-Z usa para placa-mãe, BIOS e módulos de memória.
/// </summary>
public sealed class SmbiosTable
{
    private readonly List<SmbiosStructure> _structures;

    private SmbiosTable(byte major, byte minor, byte dmiRevision, List<SmbiosStructure> structures)
    {
        MajorVersion = major;
        MinorVersion = minor;
        DmiRevision = dmiRevision;
        _structures = structures;
    }

    public byte MajorVersion { get; }
    public byte MinorVersion { get; }
    public byte DmiRevision { get; }
    public string Version => $"{MajorVersion}.{MinorVersion}";
    public IReadOnlyList<SmbiosStructure> Structures => _structures;

    public IEnumerable<SmbiosStructure> OfType(byte type) => _structures.Where(s => s.Type == type);

    public SmbiosStructure? First(byte type) => _structures.FirstOrDefault(s => s.Type == type);

    /// <summary>Le a tabela do firmware. Retorna <c>null</c> se o firmware não expuser SMBIOS.</summary>
    public static SmbiosTable? Read()
    {
        uint size = NativeMethods.GetSystemFirmwareTable(NativeMethods.RsmbProvider, NativeMethods.FirmwareTableId, IntPtr.Zero, 0);
        if (size == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var written = NativeMethods.GetSystemFirmwareTable(NativeMethods.RsmbProvider, NativeMethods.FirmwareTableId, buffer, size);
            if (written == 0)
            {
                return null;
            }

            var raw = new byte[written];
            Marshal.Copy(buffer, raw, 0, (int)written);
            return Parse(raw);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Aceita tanto o <c>RawSMBIOSData</c> (cabecalho de 8 bytes) quanto a tabela crua.
    /// O Windows devolve o cabecalho, mas a detecção evita depender de um único formato.
    /// </summary>
    public static SmbiosTable Parse(ReadOnlySpan<byte> raw)
    {
        byte major = 0;
        byte minor = 0;
        byte dmi = 0;
        var table = raw;

        if (raw.Length >= 8 && raw[0] == 0 && raw[1] is >= 2 and <= 3)
        {
            var declared = BinaryPrimitives.ReadUInt32LittleEndian(raw[4..8]);
            if (declared > 0 && declared <= raw.Length - 8)
            {
                major = raw[1];
                minor = raw[2];
                dmi = raw[3];
                table = raw.Slice(8, (int)declared);
            }
        }

        var structures = new List<SmbiosStructure>();
        var position = 0;

        while (position + 4 <= table.Length)
        {
            var type = table[position];
            var length = table[position + 1];

            if (length < 4 || position + length > table.Length)
            {
                break;
            }

            var handle = BinaryPrimitives.ReadUInt16LittleEndian(table[(position + 2)..]);
            var formatted = table.Slice(position, length).ToArray();

            // A seção de strings termina em NUL duplo. A varredura correta e procurar
            // os dois NULs consecutivos a partir do inicio da seção (algoritmo do
            // dmidecode): consumir string a string erra quando a última string e
            // seguida imediatamente pelo terminador.
            var stringStart = position + length;
            var scan = stringStart;
            while (scan + 1 < table.Length && !(table[scan] == 0 && table[scan + 1] == 0))
            {
                scan++;
            }

            var strings = new List<string>();
            if (scan > stringStart)
            {
                // scan aponta para o primeiro NUL do par terminador, que também e o
                // terminador da última string: incluir esse byte na região garante
                // que a última string não seja descartada.
                var region = table.Slice(stringStart, Math.Min(scan + 1, table.Length) - stringStart);
                var start = 0;
                for (var i = 0; i < region.Length; i++)
                {
                    if (region[i] != 0)
                    {
                        continue;
                    }

                    if (i > start)
                    {
                        strings.Add(NativeMethods.SmbiosEncoding.GetString(region.Slice(start, i - start)));
                    }

                    start = i + 1;
                }

                if (start < region.Length)
                {
                    strings.Add(NativeMethods.SmbiosEncoding.GetString(region[start..]));
                }
            }

            var cursor = Math.Min(scan + 2, table.Length);

            structures.Add(new SmbiosStructure(type, length, handle, formatted, strings.ToArray()));

            if (type == 127)
            {
                break;
            }

            position = cursor;
        }

        return new SmbiosTable(major, minor, dmi, structures);
    }
}
