# Third-Party Notices

CanvasLab depends on third-party software restored through NuGet. This file summarizes the reviewed obligations; it does not replace the authoritative license and notice files shipped by those packages.

## SkiaSharp 4.154.0

- Project: <https://github.com/mono/SkiaSharp>
- Package: <https://www.nuget.org/packages/SkiaSharp/4.154.0>
- License: MIT
- Copyright notices in the package include Xamarin, Inc. and Microsoft Corporation.

The MIT copyright and permission notice must remain with copies or substantial portions of SkiaSharp.

## SkiaSharp native assets

CanvasLab resolves `SkiaSharp.NativeAssets.Win32` 4.154.0 for the native Windows codec library. SkiaSharp's generic dependency group also resolves `SkiaSharp.NativeAssets.macOS` 4.154.0, although the macOS library is not used by CanvasLab on Windows.

The native-assets package contains an authoritative `THIRD-PARTY-NOTICES.txt`. It identifies incorporated components and their licenses, including Google Skia under the BSD 3-Clause license and additional components under their respective BSD, MIT-style, Apache 2.0, ISC, FreeType, and other listed terms.

Any installer, portable binary distribution, or GitHub Release must include the applicable SkiaSharp license and the complete upstream third-party notice file from the restored native-assets package. Official binary releases, when available, are published on the GitHub Releases page.

Reviewed package files for version 4.154.0:

- `SkiaSharp/LICENSE.txt`
- `SkiaSharp.NativeAssets.Win32/LICENSE.txt`
- `SkiaSharp.NativeAssets.Win32/THIRD-PARTY-NOTICES.txt`

Do not remove or abbreviate those upstream notices when redistributing the corresponding binaries.
