# Contributing to CanvasLab

Thanks for your interest in CanvasLab.

## Current status
CanvasLab is open-source and in beta. Please open an issue to discuss substantial changes before beginning work.

## Development principles
- Keep the app local-first and offline.
- Use clear, simple English in the user interface.
- Preserve original images; never silently overwrite them.
- Favor small, focused changes and automated tests.
- Include sample images created for testing, not personal images.
- Check third-party package licenses before adding dependencies.

## Development setup
- Windows
- .NET 10 SDK
- WPF

From the repository root:

```powershell
dotnet restore .\CanvasLab.sln
dotnet build .\CanvasLab.sln --configuration Release --no-restore
dotnet run --project .\tests\CanvasLab.SmokeTests\CanvasLab.SmokeTests.csproj --configuration Release --no-build
```

The smoke-test project is an executable check runner. It returns exit code `0` when all checks pass.

## Reporting issues
When reporting a bug, include reproduction steps, Windows version, input format, expected behavior, and actual behavior. Do not upload private images without removing sensitive content.
