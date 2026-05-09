# Connecter Browser for s&box

![Connecter Browser logo](Assets/Connecter-Logo.png)

Editor-only s&box library for browsing a local [Connecter](https://www.designconnected.com/connecter) workspace and importing assets into the active project on demand.

## What It Does

- Adds `View > Connecter Browser` to the s&box editor.
- Auto-discovers a Connecter workspace from saved settings and common local locations.
- Lets you manually set the Connecter workspace path in the dock.
- Does not bake in a single Connecter path; the saved workspace path can be changed or cleared any time.
- Reads Connecter repository roots from `<workspace>/default.dcdb`.
- Falls back to `<workspace>/settings.xml` if the database does not expose roots.
- Provides two browsing modes:
  - `Files`: folder-first navigation through Connecter roots.
  - `Assets`: flat recursive grid of importable assets across all Connecter roots.
- Imports into the active project under `Assets/ConnecterImports/<ConnecterRootName>/...`.
- Keeps source assets untouched.

## Setup

1. Put this library in your s&box libraries/addons location or reference it from a project.
2. Open s&box.
3. Open the dock from:

   ```text
   Editor > View > Connecter Browser
   ```

4. If the workspace is not found automatically, paste the Connecter workspace folder into the `Workspace` field and press the checkmark button.

The browser remembers the chosen workspace path in `%LOCALAPPDATA%/sbox/ConnecterBrowser/workspace.txt`. The workspace should be the folder containing `default.dcdb` or `settings.xml`, not one of the asset repository folders. Use the auto-detect button to clear the manual value and scan common locations again.

## Files Mode

`Files` mode is for browsing the actual Connecter folder structure.

- The left tree shows Connecter repository roots and folders.
- The right list/grid shows the current folder.
- Search recursively scans the selected folder.
- Folders can be imported recursively after confirmation.

## Assets Mode

`Assets` mode behaves more like a content browser.

- It hides the folder tree.
- It recursively scans all Connecter repositories.
- It shows a flat grid of matching assets.
- Images show source thumbnails before import.
- Models, materials, audio, and unsupported files show type tiles.
- Dragging an item imports it first, then starts a normal s&box asset drag.

Use the filter menu to show:

- `All`
- `Models`
- `Materials`
- `Images`
- `Audio`
- `Sbox`
- `Unsupported`

## Import Behavior

Single-file imports copy the selected file into the active project. For model sources, the importer also copies likely same-folder dependencies such as `.mtl` files and texture siblings.

Supported model source files:

- `.fbx`
- `.obj`
- `.vox`

These are copied and wrapped in a generated `.vmdl` using a `RenderMeshFile` node plus `AnimBindPose`. The generated `.vmdl` becomes the primary asset for drag/drop.

Images, audio, `.vmat`, and existing `.vmdl` files are copied and registered with `AssetSystem.RegisterFile`.

Unsupported files such as `.blend`, `.glb`, and `.gltf` stay visible in the browser, but v1 does not attempt conversion. Export those to `.fbx`, `.obj`, or `.vox` first.

## Safety

- Source files are never moved, deleted, or modified.
- Imports are constrained to the current project's `Assets/ConnecterImports` folder.
- Folder imports show a file-count and size confirmation first.
- The Connecter database is read-only.

## Current Limitations

- Connecter tags, descriptions, custom previews, and metadata are not used yet.
- Pre-import model/material thumbnails are type tiles, not rendered previews.
- Blender and glTF conversion is not implemented.
- Auto-discovery checks common local locations, but unusual setups may still need the workspace path set manually.

## Main Files

- `Editor/ConnecterBrowserDock.cs`: editor dock UI.
- `Editor/ConnecterAssetScanner.cs`: file and asset-mode scanning.
- `Editor/ConnecterImporter.cs`: copy/register/generate `.vmdl` import flow.
- `Editor/ConnecterWorkspaceReader.cs`: Connecter repository root discovery.
- `Code/ConnecterWorkspace.cs`: shared records.
- `Code/ConnecterAssetKind.cs`: extension classification.
- `Code/ConnecterPathUtility.cs`: runtime-safe path helpers.
- `Code/ConnecterModelDocBuilder.cs`: generated `.vmdl` text.
