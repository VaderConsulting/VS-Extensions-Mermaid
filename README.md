# Mermaid Diagram Extension for Visual Studio

Visual Studio extension that converts C# source code into **Mermaid** flow and sequence diagrams.

## Features

- **Flow diagrams** – Control flow from `if`/`else`, `switch`, loops, `return`, `throw`, etc.
- **Sequence diagrams** – Method invocations and object interactions.
- **Editor context menu** – Right-click in a C# file:
  - **Generate Mermaid Flow Diagram**
  - **Generate Mermaid Sequence Diagram**
- **Selection or method** – Works on selected code or the enclosing method when nothing is selected.
- **Diagram pane** – Tool window with WebView2, toolbar (Refresh, Copy Mermaid, Show Source, Fit, Export SVG/PNG).

## Requirements

- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET Framework 4.7.2
- WebView2 runtime (usually installed with VS)

## Build

1. Open the solution in Visual Studio 2022, or from command line:
   ```bash
   dotnet build MermaidDiagramExtension\MermaidDiagramExtension.csproj
   ```
2. The VSIX is produced at:
   `MermaidDiagramExtension\bin\Debug\net472\MermaidDiagramExtension.vsix`
3. Install by double-clicking the `.vsix` or **Extensions → Manage Extensions → Install from VSIX**.

## Mermaid library (required for diagram rendering)

The viewer loads Mermaid from the extension folder. You must add the Mermaid script once:

1. Download `mermaid.min.js` from [Mermaid releases](https://github.com/mermaid-js/mermaid/releases) or [unpkg](https://unpkg.com/mermaid/dist/mermaid.min.js).
2. Place it in `MermaidDiagramExtension\mermaid\mermaid.min.js`.
3. Rebuild the extension so the file is included in the VSIX.

If `mermaid.min.js` is missing, the diagram tab will show an error until you add it and reinstall the extension.

## Usage

1. Open a C# file.
2. Select a method body or a block of code (or leave the caret inside a method with no selection).
3. Right-click → **Generate Mermaid Flow Diagram** or **Generate Mermaid Sequence Diagram**.
4. The Mermaid Diagram tool window opens (or updates) with the diagram. Dock it as needed (e.g. right side).
5. Use the toolbar: **Refresh**, **Copy Mermaid**, **Show Source**, **Fit Diagram**, **Export SVG**, **Export PNG**.

## Project structure

- `MermaidDiagramExtension/` – VSIX project (package, commands, tool window, WPF + WebView2).
- `MermaidDiagramExtension/mermaid/viewer/index.html` – Embedded viewer that loads Mermaid and renders diagrams.
- `MermaidDiagramExtension/mermaid/mermaid.min.js` – Add this file (see above).

## Specification

This implementation follows the *Visual Studio Mermaid Diagram Extension Specification* (v1.0, 2026-03-08).
