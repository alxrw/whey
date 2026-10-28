using System.Buffers.Binary;

namespace Whey.Infra.Utils;

public enum BinaryType
{
    Unknown,
    Exe,
    Elf,
    Macho,
}

public static class BinaryInspector
{
    private const int HeaderSize = 64; // DOS header size; also covers every other format's magic + fat header count
    private const int PeSignatureOffsetField = 0x3C;
    private static readonly byte[] MagicElf = [0x7F, 0x45, 0x4C, 0x46];
    private static readonly byte[] PeSignature = [(byte)'P', (byte)'E', 0, 0];

    // Thin Mach-O, both endiannesses, 32 and 64 bit.
    private static readonly uint[] MachoThinMagics = [0xFEEDFACE, 0xFEEDFACF, 0xCEFAEDFE, 0xCFFAEDFE];
    private const uint FatMagic = 0xCAFEBABE; // shared with Java class files
    private const uint Fat64Magic = 0xCAFEBABF;
    // A Java class file stores (minor << 16 | major) here, and major >= 45; a fat header stores the architecture count.
    private const uint MaxFatArchCount = 44;

    public static BinaryType GetBinaryExecutableType(string filepath)
    {
        using var fs = File.OpenRead(filepath);
        Span<byte> header = stackalloc byte[HeaderSize];
        int read = fs.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);
        header = header[..read];

        if (header.Length < 4)
        {
            return BinaryType.Unknown;
        }

        if (header[..4].SequenceEqual(MagicElf))
        {
            return BinaryType.Elf;
        }

        uint magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (Array.IndexOf(MachoThinMagics, magic) >= 0 || magic == Fat64Magic)
        {
            return BinaryType.Macho;
        }
        if (magic == FatMagic && header.Length >= 8)
        {
            uint archs = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
            return archs is >= 1 and <= MaxFatArchCount ? BinaryType.Macho : BinaryType.Unknown;
        }

        if (header[0] == 'M' && header[1] == 'Z' && IsPortableExecutable(fs, header))
        {
            return BinaryType.Exe;
        }

        return BinaryType.Unknown;
    }

    // "MZ" alone is two printable characters; require the PE signature the DOS header points at.
    private static bool IsPortableExecutable(FileStream fs, ReadOnlySpan<byte> header)
    {
        if (header.Length < PeSignatureOffsetField + 4)
        {
            return false;
        }

        uint peOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[PeSignatureOffsetField..]);
        if (peOffset > fs.Length - PeSignature.Length)
        {
            return false;
        }

        Span<byte> sig = stackalloc byte[4];
        fs.Seek(peOffset, SeekOrigin.Begin);
        return fs.ReadAtLeast(sig, sig.Length, throwOnEndOfStream: false) == sig.Length && sig.SequenceEqual(PeSignature);
    }

    // Finds all binaries under topDir. Symlinks are skipped: they would otherwise let an archive
    // point the walk outside the extraction directory, or into a cycle.
    public static List<string> FindBinaries(string topDir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };

        var res = new List<string>();
        foreach (string path in Directory.EnumerateFiles(topDir, "*", options))
        {
            try
            {
                if (GetBinaryExecutableType(path) != BinaryType.Unknown)
                {
                    res.Add(path);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // unreadable file: can't be analysed, don't fail the whole release over it
            }
        }

        return res;
    }
}
