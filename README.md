# Mermaid Diagram Extension for Visual Studio

A Visual Studio 2022 VSIX extension that converts C# source code into **Mermaid** flow and sequence diagrams, rendered live in an embedded WebView2 tool window.

**Source last updated:** 2026-03-08

**Initiated:** 2026-03-08 · **Framework:** .NET Framework 4.7.2 · **Solution:** `MermaidDiagramExtension.sln`

---

## Features

- **Flow diagrams** - derives control flow from `if`/`else`, `switch`, loops, `return`, `throw`
- **Sequence diagrams** - captures method invocations and object interactions
- **Editor context menu** - right-click in any C# file for *Generate Mermaid Flow Diagram* or *Generate Mermaid Sequence Diagram*
- **Diagram tool window** - dockable pane with WebView2 renderer; toolbar: Refresh, Copy Mermaid, Show Source, Fit Diagram, Export SVG, Export PNG

---

## Requirements

| Component | Version |
|-----------|---------|
| Visual Studio | 2022 |
| .NET Framework | 4.7.2 |
| WebView2 Runtime | Included with VS 2022 |

---

## Installation

1. Build to produce the `.vsix`
2. Double-click `MermaidDiagramExtension.vsix` or use **Extensions > Manage Extensions > Install from VSIX**
3. Download `mermaid.min.js` and save to `MermaidDiagramExtension\mermaid\mermaid.min.js`, then rebuild

---

## Project Structure

```
MermaidDiagramExtension/
+-- MermaidDiagramPackage.cs         # AsyncPackage entry point
+-- MermaidDiagramCommands.cs        # Context-menu command handlers
+-- FlowDiagramGenerator.cs          # C# AST -> Mermaid flowchart
+-- SequenceDiagramGenerator.cs      # C# AST -> Mermaid sequence diagram
+-- DiagramControl.xaml              # WPF + WebView2 diagram panel
+-- mermaid/viewer/index.html        # Embedded HTML viewer
```

---

## Solution structure

| Project | Language | Type | Purpose |
|---------|----------|------|---------|
| `MermaidDiagramExtension` (`MermaidDiagramExtension/MermaidDiagramExtension.csproj`) | C# | VSIX package (.NET Framework 4.7.2) | Roslyn-based C# to Mermaid flow/sequence generators and WebView2 diagram tool window |

NuGet: Microsoft.VisualStudio.SDK 17.8, Microsoft.CodeAnalysis.CSharp 4.11, Microsoft.Web.WebView2.

## How to open

Open `Mermaid.sln` (or `MermaidDiagramExtension/MermaidDiagramExtension.sln`) in Visual Studio 2022 with the Visual Studio extension development workload, restore NuGet packages, and press F5 to launch the experimental instance.

## Attribution and provenance

Working copy from my Historical Dev folder.

Working copy from my Development folder `VS Extensions/Mermaid`.

## License

MIT © 2026 VaderConsulting for Dave Robinson's code. See `LICENSE`.
