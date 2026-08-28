# GraalVM Native Image Guide for `secure-memory`

This document describes how to consume `secure-memory`'s FFM-based protected memory
implementation from a [GraalVM Native Image](https://www.graalvm.org/latest/reference-manual/native-image/)
build, and the trade-offs involved.

## TL;DR

- Build `secure-memory` with the **`ffm-native`** Maven profile (flat JAR, no multi-release).
- Consume the resulting JAR from your application.
- Run `native-image` with `--enable-native-access=ALL-UNNAMED` and
  `-H:+ForeignAPISupport` enabled.
- Add the foreign reachability metadata shown below to your application's
  `META-INF/native-image/.../reachability-metadata.json`.

## Why two builds?

`secure-memory` ships in two shapes from the same source tree:

| Build              | Java baseline | JAR shape           | FFM impl included | JNA fallback |
| ------------------ | ------------- | ------------------- | ----------------- | ------------ |
| Default (`mvn install`) | Java 17 | Multi-release JAR (FFM in `META-INF/versions/22`) | Java 22+ at runtime | Yes |
| `ffm-native` (`mvn -Pffm-native install`) | Java 22+ (default 25) | Flat JAR (FFM at the root) | Always | Still present, but unused |

The default build is what you want for ordinary deployments — it auto-detects FFM at runtime
and falls back to the JNA-based `ProtectedMemorySecretFactory` on older JVMs.

The `ffm-native` build is what you want when feeding bytecode into `native-image`. Multi-release
JARs interact poorly with native-image's class-path indexing, so this profile flattens the
sources and produces a plain Java 25 JAR. It still contains the unused JNA fallback for
binary compatibility, but at runtime under `native-image` the FFM path is always taken.

## Class initialization safety (the important part)

Native-image performs aggressive ahead-of-time analysis: it tries to initialize as many
classes as possible at *build time*. That is fatal for code that calls
`Linker.nativeLinker()` in a `static {}` block, because the build-time JVM is not the runtime
JVM and the resulting `MethodHandle` cannot be embedded into the image.

`secure-memory` avoids this with the **Initialization-on-Demand Holder** idiom: every FFM
allocator class keeps its `MethodHandle` fields inside a private `static final class Handles`,
which is only loaded the first time one of the public allocator methods is called. The
shared `Linker` and `SymbolLookup` live in `NativeLibc.Holder` for the same reason.

The JLS guarantees that a nested class is initialized exactly once, on first reference, with
full thread-safety — no `volatile` reads, no double-checked locking. This is faster than the
DCL pattern *and* native-image friendly, with no special build flags required for the
allocator itself.

You should still tell native-image to initialize the allocator types **at run time** as a
defense-in-depth measure if your image inadvertently triggers one of them at build time:

```text
--initialize-at-run-time=com.godaddy.asherah.securememory.ffmimpl.NativeLibc$Holder
--initialize-at-run-time=com.godaddy.asherah.securememory.ffmimpl.FfmProtectedMemoryAllocator$Handles
--initialize-at-run-time=com.godaddy.asherah.securememory.ffmimpl.LinuxFfmProtectedMemoryAllocator$Handles
--initialize-at-run-time=com.godaddy.asherah.securememory.ffmimpl.MacOSFfmProtectedMemoryAllocator$Handles
```

## Native-image build flags

A typical `<buildArgs>` block for `org.graalvm.buildtools:native-maven-plugin`:

```xml
<buildArgs>
  <buildArg>--no-fallback</buildArg>
  <buildArg>-H:+ReportExceptionStackTraces</buildArg>
  <buildArg>-H:+UnlockExperimentalVMOptions</buildArg>

  <!-- Required for FFM downcalls into libc (mmap, mprotect, etc.) -->
  <buildArg>--enable-native-access=ALL-UNNAMED</buildArg>
  <buildArg>-H:+ForeignAPISupport</buildArg>
</buildArgs>
```

`-H:+ForeignAPISupport` is required so the image emits the trampolines for FFM downcalls.
`--enable-native-access=ALL-UNNAMED` is the runtime opt-in for the FFM module; without it
every downcall throws at runtime.

## Foreign reachability metadata

Native-image needs an explicit declaration of every FFM downcall signature you use. Drop the
following into your application's
`src/main/resources/META-INF/native-image/<group>/<artifact>/reachability-metadata.json`
(merge with anything already there). This is the **GraalVM 24+ schema**: the top level is a
map, `foreign` is a map containing `downcalls` (and optionally `upcalls`), and each downcall
is described with `returnType`/`parameterTypes` using FFM/Java type names (`long`, `int`,
`void`, `float`, `double`, `boolean`).

```json
{
  "foreign": {
    "downcalls": [
      { "returnType": "long", "parameterTypes": ["long", "long", "long", "int", "int", "int", "long"] },
      { "returnType": "long", "parameterTypes": ["long", "long"] },
      { "returnType": "int",  "parameterTypes": ["long", "long"] },
      { "returnType": "int",  "parameterTypes": ["long", "int"] },
      { "returnType": "int",  "parameterTypes": ["long", "long", "long"] },
      { "returnType": "int",  "parameterTypes": ["int", "long"] },
      { "returnType": "void", "parameterTypes": ["long", "long"] },
      { "returnType": "void", "parameterTypes": ["long", "int", "long"] },
      { "returnType": "int",  "parameterTypes": ["long", "int", "long", "long"] }
    ]
  }
}
```

This covers every libc symbol secure-memory calls: `mmap`, `munmap`, `mprotect`, `mlock`,
`munlock`, `getrlimit`, `setrlimit`, `madvise` (Linux), `bzero` (Linux), `memset_s` (macOS).
Pointer arguments use `long` because FFM uses `ValueLayout.ADDRESS` whose carrier is a 64-bit
native pointer; native-image's foreign parser is happy with either `long` or the legacy
`void*` spelling.

> **If you see** `Error parsing panama foreign configuration ... first level of document
> must be a map` **or** `Missing attribute(s) [parameterTypes, returnType]`: you are using
> the pre-24 schema (top-level array, kebab-case `argument-types`/`return-type`). Switch
> to the map form above.

If you also want to construct `FfmSecretFactory` reflectively from configuration (rather
than directly), add reflection metadata for it:

```json
{
  "reflection": [
    {
      "type": "com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory",
      "methods": [
        { "name": "<init>", "parameterTypes": [] }
      ]
    }
  ]
}
```

## Verifying the build

The `samples/java/native-app` module is a worked example of the full pipeline:

```bash
# 1. Build the FFM-native flavor of secure-memory and install to the local Maven repo.
cd java/secure-memory
mvn -Pffm-native -DskipTests clean install

# 2. Build the consuming application's native image. The native-app pom pins
#    securememory explicitly so the locally-built flat JAR overrides the multi-release
#    JAR that appencryption would otherwise pull in transitively.
cd ../../samples/java/native-app
mvn -DskipTests package

# 3. Run it. Look for `Using FFM-based SecretFactory` in the log, no JNA fallback.
./target/asherah-native 5 "hello native"
```

A successful run prints something like:

```
[INFO] App - === Asherah GraalVM Native Image Sample ===
[INFO] App - Crypto Engine: JDK AES-256-GCM
[INFO] App - Java Version: 25
[INFO] TransientSecretFactory - Using FFM-based SecretFactory
[INFO] App - --- Iteration 1 ---
[INFO] App - Encrypt time: 932 µs
[INFO] App - Decrypt time: 182 µs
[INFO] App - Matches original: true
...
[INFO] App - All iterations successful!
```

The default `mvn install` build still works for non-native deployments and still ships a
multi-release JAR with FFM in `META-INF/versions/22` and JNA fallback for Java 17–21.

## Known caveats

- **macOS core dumps.** macOS lacks `madvise(MADV_DONTDUMP)`, so the macOS allocator
  disables core dumps *process-wide* via `setrlimit(RLIMIT_CORE, 0)` on construction. This
  is a documented trade-off shared with the JNA implementation.
- **`mlock` ulimit.** `mmap`'d memory is locked with `mlock()`, which is bounded by
  `RLIMIT_MEMLOCK`. The allocator pre-checks the limit and throws
  `FfmMemoryLimitException` if the request would exceed it. On containerized hosts you may
  need to bump `LimitMEMLOCK` in the systemd unit / `--ulimit memlock=` for Docker.
- **Static linking.** If you build with `--static --libc=musl`, the symbol-lookup defaults
  may differ. Use the standard glibc/macOS toolchain for the supported configurations.

## File layout reference

```
java/secure-memory/
├── pom.xml                                  # Default build + ffm-native profile
├── NATIVE_IMAGE_GUIDE.md                    # This file
└── src/main/
    ├── java/                                # Java 17 baseline (JNA fallback, factory facade)
    │   └── com/godaddy/asherah/securememory/
    │       ├── TransientSecretFactory.java
    │       └── protectedmemoryimpl/         # JNA-based implementation
    └── java22/                              # Java 22+ FFM implementation
        └── com/godaddy/asherah/securememory/ffmimpl/
            ├── NativeLibc.java              # Shared FFM linker/lookup helper
            ├── Platform.java                # OS detection enum
            ├── FfmAllocator.java            # Allocator interface
            ├── FfmProtectedMemoryAllocator.java
            ├── LinuxFfmProtectedMemoryAllocator.java
            ├── MacOSFfmProtectedMemoryAllocator.java
            ├── FfmProtectedMemorySecret.java
            ├── FfmSecretFactory.java
            ├── FfmAllocationFailed.java
            ├── FfmMemoryLimitException.java
            └── FfmOperationFailed.java
```
