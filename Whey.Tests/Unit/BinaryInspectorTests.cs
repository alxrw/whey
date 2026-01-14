using AwesomeAssertions;
using Whey.Infra.Utils;
using Whey.Tests.TestData;

namespace Whey.Tests.Unit;

public class BinaryInspectorTests : IDisposable
{
	private readonly List<string> _tempFiles = [];
	private readonly List<string> _tempDirs = [];

	public void Dispose()
	{
		foreach (var file in _tempFiles)
		{
			if (File.Exists(file))
				File.Delete(file);
		}
		foreach (var dir in _tempDirs)
		{
			if (Directory.Exists(dir))
				Directory.Delete(dir, recursive: true);
		}
	}

	[Fact]
	public void GetBinaryExecutableType_ElfBinary_ReturnsElf()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.ElfMagic);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Elf);
	}

	[Fact]
	public void GetBinaryExecutableType_ExeBinary_ReturnsExe()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.ExeMagic);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Exe);
	}

	[Fact]
	public void GetBinaryExecutableType_MachoLittleEndian64_ReturnsMacho()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.MachoMagic64Le);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Macho);
	}

	[Fact]
	public void GetBinaryExecutableType_MachoBigEndian64_ReturnsMacho()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.MachoMagic64Be);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Macho);
	}

	[Fact]
	public void GetBinaryExecutableType_MachoLittleEndian32_ReturnsMacho()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.MachoMagic32Le);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Macho);
	}

	[Fact]
	public void GetBinaryExecutableType_MachoBigEndian32_ReturnsMacho()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.MachoMagic32Be);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Macho);
	}

	[Fact]
	public void GetBinaryExecutableType_TextFile_ReturnsUnknown()
	{
		var path = BinaryTestFiles.CreateTempFile(BinaryTestFiles.TextFile);
		_tempFiles.Add(path);

		var result = BinaryInspector.GetBinaryExecutableType(path);

		result.Should().Be(BinaryType.Unknown);
	}

	[Fact]
	public void FindBinaries_DirectoryWithMixedFiles_FindsOnlyBinaries()
	{
		var dir = BinaryTestFiles.CreateTempDirectoryWithBinaries();
		_tempDirs.Add(dir);

		var result = BinaryInspector.FindBinaries(dir);

		result.Should().HaveCount(3); // app.exe, linux-app, mac-app
		result.Should().Contain(p => p.EndsWith("app.exe"));
		result.Should().Contain(p => p.EndsWith("linux-app"));
		result.Should().Contain(p => p.EndsWith("mac-app"));
	}

	[Fact]
	public void FindBinaries_EmptyDirectory_ReturnsEmpty()
	{
		var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(dir);
		_tempDirs.Add(dir);

		var result = BinaryInspector.FindBinaries(dir);

		result.Should().BeEmpty();
	}

	[Fact]
	public void FindBinaries_DirectoryWithOnlyTextFiles_ReturnsEmpty()
	{
		var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(dir);
		_tempDirs.Add(dir);

		File.WriteAllBytes(Path.Combine(dir, "readme.txt"), BinaryTestFiles.TextFile);
		File.WriteAllBytes(Path.Combine(dir, "notes.md"), BinaryTestFiles.TextFile);

		var result = BinaryInspector.FindBinaries(dir);

		result.Should().BeEmpty();
	}

	[Fact]
	public void FindBinaries_NestedDirectories_FindsBinariesRecursively()
	{
		var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(baseDir);
		_tempDirs.Add(baseDir);

		// Create nested structure
		var level1 = Path.Combine(baseDir, "level1");
		var level2 = Path.Combine(level1, "level2");
		var level3 = Path.Combine(level2, "level3");
		Directory.CreateDirectory(level3);

		File.WriteAllBytes(Path.Combine(baseDir, "root.exe"), BinaryTestFiles.ExeMagic);
		File.WriteAllBytes(Path.Combine(level1, "l1.elf"), BinaryTestFiles.ElfMagic);
		File.WriteAllBytes(Path.Combine(level3, "deep.macho"), BinaryTestFiles.MachoMagic64Le);

		var result = BinaryInspector.FindBinaries(baseDir);

		result.Should().HaveCount(3);
	}
}
