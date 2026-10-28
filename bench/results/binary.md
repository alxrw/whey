## Binary analysis

### Classification accuracy vs `file(1)` (3,935 real files)

| Class | Files (truth) | Correct | False pos. | False neg. | Precision | Recall |
|---|---|---|---|---|---|---|
| Elf | 983 | 983 | 0 | 0 | 100.0 % | 100.0 % |
| Exe | 759 | 759 | 0 | 0 | 100.0 % | 100.0 % |
| Macho | 3 | 3 | 0 | 0 | 100.0 % | 100.0 % |

Non-binary files in corpus: 2,190. Files on which `GetBinaryExecutableType` **threw** instead of classifying: **0**.


### Edge cases (synthetic headers)

| Case | Expected | Actual |  |
|---|---|---|---|
| Mach-O 64 LE (thin) | Macho | Macho | pass |
| Mach-O universal / fat (0xCAFEBABE) | Macho | Macho | pass |
| Mach-O universal / fat64 (0xCAFEBABF) | Macho | Macho | pass |
| Plain text starting with "MZ" | Unknown | Unknown | pass |
| Java .class (0xCAFEBABE) | Unknown | Unknown | pass |
| Empty file (0 bytes) | Unknown | Unknown | pass |
| 3-byte file | Unknown | Unknown | pass |
| `FindBinaries` on a directory with one ELF and one empty `.keep` file | 1 binary found | 1 binary found | pass |

### `FindBinaries` throughput and file-handle behaviour

| Files in tree | Time (ms) | Files / s | Binaries found | Open FDs left (before GC) | (after GC) | Error |
|---|---|---|---|---|---|---|
| 1,000 | 7 | 137,722 | 50 | 0 | 0 |  |
| 10,000 | 90 | 111,565 | 500 | 0 | 0 |  |
| 50,000 | 422 | 118,408 | 2,500 | 0 | 0 |  |

`FindBinaries` over a 5,000-file tree with `ulimit -n 1024` (the common default): **ok, 250 binaries found**

Descriptors are released as each file is classified, so the count stays at zero regardless of the `ulimit -n` of the host (this machine: 1048576).

### Dependency extraction (`DependencyFinderService.GetLibsLinux`)

| ELF files | NEEDED set == readelf | With ≥1 dep | Latency p50 (ms) | p95 (ms) | Regex parse only (µs) | Process spawn share |
|---|---|---|---|---|---|---|
| 120 | 100% | 116 | 12.5 | 15.2 | 2.5 | 100% |

Each binary costs one `objdump` process spawn; the parse itself is noise by comparison. Agreement is against `readelf -d` (same binutils family), so it validates the parser, not objdump.

