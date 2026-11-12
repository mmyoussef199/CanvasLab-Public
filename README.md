# CanvasLab

**CanvasLab** is a free, open-source Windows desktop image utility for inspecting image colors and making image files smaller. Images are processed locally on your computer, with no accounts, cloud processing, telemetry, or uploads.

> **Beta:** v0.1.0-beta.1. Tested on Windows 10 and Windows 11. The installer is currently **unsigned**, so Windows may display an Unknown Publisher or SmartScreen warning. Always download from the official GitHub Releases page.

## Download and install

Download the Windows x64 installer from [GitHub Releases](https://github.com/mmyoussef199/CanvasLab-Public/releases). The first beta release asset will be named `CanvasLab-Setup-0.1.0-beta.1.exe`.

Run the installer and follow its prompts. No separate .NET installation or developer tools are required. The setup installs for the current user, creates a Start Menu shortcut, and optionally creates a Desktop shortcut. Uninstall through Windows Settings.

**Release status:** The public source repository is being prepared. A downloadable release must be verified on the Releases page before it is considered published.

## Features

### Pick Colors

- Pick a pixel to read its HEX, RGB, alpha, and image coordinates.
- Copy a selected HEX value.
- See representative image colors with approximate coverage.
- Pick colors at Fit, 100%, other zoom levels, and after panning.

### Make Image Smaller

- Resize while preserving proportions by default.
- Export JPEG, PNG, or lossy WebP.
- Preview the encoded result and estimated space savings before saving.
- Preserve PNG and WebP transparency; JPEG flattens transparency onto white.
- Apply EXIF orientation on opening.
- Protect the original image against accidental overwrite.

PNG export is lossless, so the Image Quality control applies only to JPEG and WebP.

## Requirements

- Windows 10 or Windows 11, 64-bit
- The downloadable installer bundles its required .NET runtime
- Developers building from source need the .NET 10 SDK and Windows

## Build from source

```powershell
dotnet restore .\CanvasLab.sln
dotnet build .\CanvasLab.sln --configuration Release --no-restore
dotnet run --project .\tests\CanvasLab.SmokeTests\CanvasLab.SmokeTests.csproj --configuration Release --no-build
```

To make a self-contained publish:

```powershell
dotnet publish .\src\CanvasLab\CanvasLab.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false --output .\.artifacts\publish\win-x64
```

To build the installer, use an appropriately licensed Inno Setup compiler and `installer/CanvasLab.iss` after creating the self-contained publish. Installer binaries and local build output must not be committed to the source repository.

## Feedback

Please report reproducible bugs via [GitHub Issues](https://github.com/mmyoussef199/CanvasLab-Public/issues), including your Windows version, reproduction steps, and expected versus actual behavior. Do not attach private or sensitive images.

## License and third-party notices

CanvasLab is licensed under [MIT](LICENSE) and uses [SkiaSharp](https://github.com/mono/SkiaSharp). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `licenses/` directory for required upstream notices.

Copyright © 2026 Mohamed Youssef.
