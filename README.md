# Slnx Merger

Slnx Merger is a Windows desktop application (WPF, .NET 8) designed to **compare and merge `.slnx` files**.

It helps keep multiple solution files synchronized when they share common projects and folder structures.

## Build

```bash
cd SlnxMerger
dotnet build -c Release
# Optional: publish a Windows build
dotnet publish -c Release -r win-x64 --self-contained false
```

The executable is generated in `bin/Release/net8.0-windows/`.
You can also open and build the project directly in Visual Studio 2022 or later.

## Usage

1. Select two `.slnx` files (using browse buttons or drag-and-drop).
2. Click **Compare**.
3. Review the comparison tree:
   - Green: present only in the left file
   - Orange: present only in the right file
   - Gray: identical in both files
4. Choose the items to synchronize, then apply changes:
   - **Apply to Right**: updates the right file to match the left file
   - **Apply to Left**: updates the left file to match the right file

Before writing changes, the app creates a backup file (`*.slnx.bak`) and asks for confirmation.

## Options

- **Ignore non-common root folders** (enabled by default): compares only top-level folders present in both solutions.
- **Show differences only**: hides identical branches.

## Technical Notes

- Project and file matching is based on their resolved absolute paths.
- Relative paths are recalculated when items are copied between files.
- The original `.slnx` XML structure is preserved, including metadata such as configurations, properties, and dependencies.
- The app supports both flat and nested folder styles used in `.slnx` files.
- Inserted items are sorted alphabetically for consistent output.
