namespace Whey.Tests.TestData;

/// <summary>
/// Helper class to create binary test files with specific magic bytes.
/// </summary>
public static class BinaryTestFiles
{
	// ELF magic bytes: 0x7F 'E' 'L' 'F'
	public static readonly byte[] ElfMagic = [0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

	// PE/EXE magic bytes: 'M' 'Z'
	public static readonly byte[] ExeMagic = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00];

	// Mach-O 64-bit magic bytes (little endian): 0xCF 0xFA 0xED 0xFE
	public static readonly byte[] MachoMagic64Le = [0xCF, 0xFA, 0xED, 0xFE, 0x07, 0x00, 0x00, 0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00];

	// Mach-O 64-bit magic bytes (big endian): 0xFE 0xED 0xFA 0xCF
	public static readonly byte[] MachoMagic64Be = [0xFE, 0xED, 0xFA, 0xCF, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x02];

	// Mach-O 32-bit magic bytes (little endian): 0xCE 0xFA 0xED 0xFE
	public static readonly byte[] MachoMagic32Le = [0xCE, 0xFA, 0xED, 0xFE, 0x07, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00];

	// Mach-O 32-bit magic bytes (big endian): 0xFE 0xED 0xFA 0xCE
	public static readonly byte[] MachoMagic32Be = [0xFE, 0xED, 0xFA, 0xCE, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x02];

	// Plain text file (not a binary)
	public static readonly byte[] TextFile = "Hello, World!\n"u8.ToArray();

	/// <summary>
	/// Creates a temporary file with the specified bytes and returns the path.
	/// </summary>
	public static string CreateTempFile(byte[] contents, string? extension = null)
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + (extension ?? ""));
		File.WriteAllBytes(path, contents);
		return path;
	}

	/// <summary>
	/// Creates a temporary directory with multiple binary files for testing FindBinaries.
	/// </summary>
	public static string CreateTempDirectoryWithBinaries()
	{
		var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(baseDir);

		// Create root level binaries
		File.WriteAllBytes(Path.Combine(baseDir, "app.exe"), ExeMagic);
		File.WriteAllBytes(Path.Combine(baseDir, "readme.txt"), TextFile);

		// Create subdirectory with binaries
		var subDir = Path.Combine(baseDir, "bin");
		Directory.CreateDirectory(subDir);
		File.WriteAllBytes(Path.Combine(subDir, "linux-app"), ElfMagic);
		File.WriteAllBytes(Path.Combine(subDir, "mac-app"), MachoMagic64Le);

		// Create another subdirectory with only text
		var docsDir = Path.Combine(baseDir, "docs");
		Directory.CreateDirectory(docsDir);
		File.WriteAllBytes(Path.Combine(docsDir, "manual.txt"), TextFile);

		return baseDir;
	}
}
