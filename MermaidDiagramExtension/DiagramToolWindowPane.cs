using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace MermaidDiagramExtension
{
    [Guid("b2c3d4e5-f6a7-8901-bcde-f12345678901")]
    public class DiagramToolWindowPane : ToolWindowPane
    {
        private DiagramControl _Control;

        public DiagramToolWindowPane() : base(null)
        {
            Caption = "Mermaid Diagram";
            _Control = new DiagramControl();
            Content = _Control;
        }

        public void SetContent(string Mermaid, bool IsFlow)
        {
            _Control?.SetDiagram(Mermaid, IsFlow);
        }

        public void ShowError(string Message)
        {
            _Control?.ShowError(Message);
        }
    }
}
