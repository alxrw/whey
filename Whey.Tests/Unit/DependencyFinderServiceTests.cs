using AwesomeAssertions;
using Whey.Infra.Services;

namespace Whey.Tests.Unit;

public class DependencyFinderServiceTests
{
	[Fact]
	public void ParseObjdumpOutput_SingleNeededEntry_ParsesCorrectly()
	{
		var output = """
			Dynamic Section:
			  NEEDED               libpthread.so.0
			""";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().ContainSingle();
		result[0].Should().Be("libpthread.so.0");
	}

	[Fact]
	public void ParseObjdumpOutput_MultipleNeededEntries_ParsesAll()
	{
		var output = """
			Dynamic Section:
			  NEEDED               libpthread.so.0
			  NEEDED               libc.so.6
			  NEEDED               libm.so.6
			  NEEDED               libdl.so.2
			""";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().HaveCount(4);
		result.Should().Contain("libpthread.so.0");
		result.Should().Contain("libc.so.6");
		result.Should().Contain("libm.so.6");
		result.Should().Contain("libdl.so.2");
	}

	[Fact]
	public void ParseObjdumpOutput_EmptyOutput_ReturnsEmpty()
	{
		var output = "";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().BeEmpty();
	}

	[Fact]
	public void ParseObjdumpOutput_NoNeededEntries_ReturnsEmpty()
	{
		var output = """
			Dynamic Section:
			  SONAME               libfoo.so.1
			  RPATH                /usr/lib
			""";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().BeEmpty();
	}

	[Fact]
	public void ParseObjdumpOutput_MixedEntries_OnlyParsesNeeded()
	{
		var output = """
			file format elf64-x86-64
			
			Dynamic Section:
			  NEEDED               libpthread.so.0
			  SONAME               mylib.so.1
			  NEEDED               libc.so.6
			  RPATH                /opt/lib
			  INIT                 0x0000000000001000
			  FINI                 0x0000000000002000
			""";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().HaveCount(2);
		result.Should().Contain("libpthread.so.0");
		result.Should().Contain("libc.so.6");
	}

	[Fact]
	public void ParseObjdumpOutput_TrimsWhitespace()
	{
		var output = "  NEEDED               libfoo.so.1   ";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().ContainSingle();
		result[0].Should().Be("libfoo.so.1");
	}

	[Fact]
	public void ParseObjdumpOutput_RealWorldExample_ParsesCorrectly()
	{
		// Simulated output from `objdump -p /bin/ls`
		var output = """
			/bin/ls:     file format elf64-x86-64
			
			Program Header:
			    PHDR off    0x0000000000000040 vaddr 0x0000000000000040 paddr 0x0000000000000040 align 2**3
			         filesz 0x00000000000002d8 memsz 0x00000000000002d8 flags r--
			
			Dynamic Section:
			  NEEDED               libselinux.so.1
			  NEEDED               libc.so.6
			  INIT                 0x0000000000004000
			  FINI                 0x0000000000016994
			  INIT_ARRAY           0x000000000001e390
			  INIT_ARRAYSZ         0x0000000000000008
			  FINI_ARRAY           0x000000000001e398
			  FINI_ARRAYSZ         0x0000000000000008
			  GNU_HASH             0x0000000000000318
			  STRTAB               0x0000000000001108
			  SYMTAB               0x0000000000000370
			  STRSZ                0x0000000000000618
			  SYMENT               0x0000000000000018
			  DEBUG                0x0000000000000000
			  PLTGOT               0x000000000001f000
			  PLTRELSZ             0x0000000000000ae0
			  PLTREL               0x0000000000000007
			  JMPREL               0x0000000000002b18
			  RELA                 0x0000000000001d48
			  RELASZ               0x0000000000000dd0
			  RELAENT              0x0000000000000018
			  FLAGS                0x0000000000000008
			  FLAGS_1              0x0000000008000001
			  VERNEED              0x0000000000001cd8
			  VERNEEDNUM           0x0000000000000001
			  VERSYM               0x0000000000001720
			  RELACOUNT            0x00000000000000c3
			
			Version References:
			  required from libc.so.6:
			""";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().HaveCount(2);
		result.Should().Contain("libselinux.so.1");
		result.Should().Contain("libc.so.6");
	}

	[Fact]
	public void ParseObjdumpOutput_TabSeparated_ParsesCorrectly()
	{
		// Some objdump versions use tabs instead of spaces
		var output = "\tNEEDED\t\t\tlibfoo.so.1";

		var result = DependencyFinderService.ParseObjdumpOutput(output);

		result.Should().ContainSingle();
		result[0].Should().Be("libfoo.so.1");
	}

	[Fact]
	public void ObjdumpParsePattern_MatchesCorrectFormat()
	{
		var regex = DependencyFinderService.ObjdumpParsePattern();

		var match = regex.Match("  NEEDED               libtest.so.1");

		match.Success.Should().BeTrue();
		match.Groups[1].Value.Trim().Should().Be("libtest.so.1");
	}

	[Fact]
	public void ObjdumpParsePattern_DoesNotMatchInvalidFormats()
	{
		var regex = DependencyFinderService.ObjdumpParsePattern();

		regex.IsMatch("SONAME libtest.so.1").Should().BeFalse();
		regex.IsMatch("libtest.so.1 NEEDED").Should().BeFalse();
		regex.IsMatch("NEED libtest.so.1").Should().BeFalse();
	}
}
