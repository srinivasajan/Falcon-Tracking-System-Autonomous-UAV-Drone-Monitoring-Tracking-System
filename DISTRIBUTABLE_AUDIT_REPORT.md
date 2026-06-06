# DISTRIBUTABLE WINDOWS APPLICATION AUDIT REPORT

**Date/Time:** 2026-06-06T14:54:48+05:30  
**Auditor:** Zero-Trust Runtime Verification  
**Status: ALL STEPS VERIFIED FROM FILESYSTEM + EXECUTION EVIDENCE**

---

## PHASE 1: Pre-Existing Artifact Audit

### Packaged Outputs (.exe / .msi / .msix / .appinstaller)
| Finding | Result |
|---------|--------|
| `*.msi` files | ❌ None found |
| `*.msix` files | ❌ None found |
| `*.appinstaller` files | ❌ None found |
| `publish/` directories | ❌ None found (pre-audit) |
| WiX Toolset (`.wixproj`) | ❌ None found |
| Inno Setup scripts (`.iss`) | ❌ None found (pre-audit) |
| **Pre-existing distributable installer** | ❌ **DOES NOT EXIST** |

### Existing EXEs Found (Debug build stubs, framework-dependent)
| Path | Size | Type |
|------|------|------|
| `DroneControl.UI\bin\Debug\net8.0-windows\DroneControl.UI.exe` | **0.14 MB** | Framework-dependent stub — NOT distributable |
| `DroneControl.UI\bin\Release\net8.0-windows\DroneControl.UI.exe` | 0.14 MB | Framework-dependent stub — NOT distributable |

> **Verdict:** The 0.14 MB stubs require .NET 8 runtime to be pre-installed on the target machine. They are **not self-contained** and cannot be distributed standalone.

---

## PHASE 2: .csproj Property Audit

**File:** `DroneControl.UI\DroneControl.UI.csproj`

| Property | Value | Found |
|----------|-------|-------|
| `UseWPF` | `true` | ✅ Yes |
| `OutputType` | `WinExe` | ✅ Yes |
| `PublishSingleFile` | — | ❌ Not set (added via CLI) |
| `SelfContained` | — | ❌ Not set (added via CLI) |
| `RuntimeIdentifier` | — | ❌ Not set (added via CLI) |
| `WindowsDesktop SDK` | Implied by `UseWPF` | ✅ Yes |

**Identified UI Startup Project:** `DroneControl\DroneControl.UI\DroneControl.UI.csproj`

---

## PHASE 3: Self-Contained Publish Build

### Command Executed
```
dotnet publish DroneControl\DroneControl.UI\DroneControl.UI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o "Publish\FalconDroneSystem"
```

### Result: ✅ SUCCESS (0 errors, 3 warnings)

### Published Output: `Publish\FalconDroneSystem\`
| File | Size (MB) |
|------|-----------|
| **DroneControl.UI.exe** | **164.02 MB** ← primary executable |
| libSkiaSharp.dll | 11.07 MB |
| onnxruntime.dll | 16.53 MB |
| libSkiaSharp.pdb | 79.95 MB |
| libHarfBuzzSharp.pdb | 19.95 MB |
| D3DCompiler_47_cor3.dll | 4.52 MB |
| e_sqlite3.dll | 1.61 MB |
| appsettings.json | — |
| *(+other native DLLs)* | — |

> **The published EXE bundles .NET 8 runtime and all dependencies. No pre-installation required on target Windows 10+ machine.**

---

## PHASE 4: Inno Setup Installer Build

### Installer Script Created
**Path:** `Installer\FalconSetup.iss`

| Property | Value |
|----------|-------|
| App Name | Falcon Drone System |
| Version | 1.0 |
| Publisher | srinivasajan |
| Compression | LZMA2 Ultra64 |
| Solid Compression | Yes |
| Default Install Dir | `%ProgramFiles%\Falcon Drone System` |
| Start Menu Group | Falcon Drone System |
| Desktop Shortcut | Optional (unchecked by default) |
| Min OS | Windows 10 (10.0.17763) |
| Architecture | x64compatible |

### Inno Setup Compiler
- **Tool:** Inno Setup 6.7.3 (`C:\Program Files (x86)\Inno Setup 6\ISCC.exe`)
- **Compile Time:** 134.687 seconds

### Build Result: ✅ SUCCESS (0 errors, 1 warning fixed)

### Installer Output
| Property | Value |
|----------|-------|
| **Path** | `Installer\Output\FalconDroneSystem_v1.0_Setup.exe` |
| **Size** | **73.4 MB** (LZMA2 compressed from ~323 MB raw) |
| **Compression Ratio** | ~77% |
| Timestamp | 2026-06-06 |

---

## Final Summary

| Step | Status | Evidence |
|------|--------|----------|
| Pre-existing distributable | ❌ Not found | Filesystem scan — no .msi/.msix/.iss/publish dirs |
| UI startup project identified | ✅ `DroneControl.UI.csproj` (`UseWPF=true`, `OutputType=WinExe`) | Source: csproj |
| Self-contained publish | ✅ SUCCESS | `Publish\FalconDroneSystem\DroneControl.UI.exe` — 164 MB |
| Inno Setup installer built | ✅ SUCCESS | `Installer\Output\FalconDroneSystem_v1.0_Setup.exe` — 73.4 MB |

**The Falcon Drone System is now fully packaged and ready for distribution as a standalone Windows installer.**
