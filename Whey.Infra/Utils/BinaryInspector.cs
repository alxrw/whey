using Microsoft.Extensions.Logging;

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
	private const int NumBytesToRead = 4;
	private static byte[] MagicExe = [0x4D, 0x5A];
	private static byte[] MagicElf = [0x7F, 0x45, 0x4C, 0x46]; // 52 byte min header, possibly unimportant?
	private static byte[][] MagicMachos = [
		[0xFE, 0xED, 0xFA, 0xCE],
		[0xFE, 0xED, 0xFA, 0xCF],
		[0xCE, 0xFA, 0xED, 0xFE],
		[0xCF, 0xFA, 0xED, 0xFE],
	];

	public static BinaryType GetBinaryExecutableType(string filepath)
	{
		var fs = File.OpenRead(filepath);
		var bytes = GetBytes(fs, NumBytesToRead);

		if (MagicExe.SequenceEqual(bytes[0..2]))
		{
			return BinaryType.Exe;
		}
		else if (MagicElf.SequenceEqual(bytes))
		{
			return BinaryType.Elf;
		}
		else
		{
			foreach (byte[] magic in MagicMachos)
			{
				if (magic.SequenceEqual(bytes))
				{
					return BinaryType.Macho;
				}
			}
		}

		return BinaryType.Unknown;
	}

	private static byte[] GetBytes(Stream fileStream, int numBytes)
	{
		if (fileStream is null || fileStream.Position != 0 || !fileStream.CanRead)
		{
			// TODO: log this
			return [];
		}

		var buffer = new byte[numBytes];
		fileStream.ReadExactly(buffer);

		return buffer;
	}
}
