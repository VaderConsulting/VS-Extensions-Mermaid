using System;
using System.ComponentModel.Design;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace MermaidDiagramExtension
{
    internal static class MermaidDiagramCommands
    {
        private static readonly Guid CommandSetGuid = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        private const int IdGenerateFlowDiagram = 0x0100;
        private const int IdGenerateSequenceDiagram = 0x0101;

        private static AsyncPackage _Package;
        private static DiagramToolWindowPane _ToolWindowPane;

        public static async Task InitializeAsync(AsyncPackage Package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Package.DisposalToken);
            _Package = Package;
            OleMenuCommandService CommandService = await Package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (CommandService == null)
            {
                return;
            }

            CommandID FlowCommandId = new CommandID(CommandSetGuid, IdGenerateFlowDiagram);
            OleMenuCommand FlowCommand = new OleMenuCommand(OnGenerateFlowDiagram, FlowCommandId);
            FlowCommand.BeforeQueryStatus += OnBeforeQueryStatus;
            CommandService.AddCommand(FlowCommand);

            CommandID SequenceCommandId = new CommandID(CommandSetGuid, IdGenerateSequenceDiagram);
            OleMenuCommand SequenceCommand = new OleMenuCommand(OnGenerateSequenceDiagram, SequenceCommandId);
            SequenceCommand.BeforeQueryStatus += OnBeforeQueryStatus;
            CommandService.AddCommand(SequenceCommand);
        }

        private static void OnBeforeQueryStatus(object Sender, EventArgs E)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OleMenuCommand Command = Sender as OleMenuCommand;
            if (Command == null)
            {
                return;
            }

            Command.Visible = CodeContextHelper.IsCSharpEditorContext();
            Command.Enabled = Command.Visible;
        }

        private static void OnGenerateFlowDiagram(object Sender, EventArgs E)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GenerateAndShowDiagram(IsFlow: true);
        }

        private static void OnGenerateSequenceDiagram(object Sender, EventArgs E)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GenerateAndShowDiagram(IsFlow: false);
        }

        private static void GenerateAndShowDiagram(bool IsFlow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string Code = CodeContextHelper.GetSelectedOrEnclosingCode();
            if (string.IsNullOrWhiteSpace(Code))
            {
                ShowError("No code selected. Select code or place the caret inside a method.");
                return;
            }

            string Mermaid = null;
            string ErrorMessage = null;
            if (IsFlow)
            {
                FlowDiagramGenerator.Generate(Code, out Mermaid, out ErrorMessage);
            }
            else
            {
                SequenceDiagramGenerator.Generate(Code, out Mermaid, out ErrorMessage);
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ShowError(ErrorMessage);
                return;
            }

            ShowOrUpdateDiagram(Mermaid, IsFlow);
        }

        private static void ShowOrUpdateDiagram(string Mermaid, bool IsFlow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_ToolWindowPane == null)
            {
                _ToolWindowPane = _Package.FindToolWindow(typeof(DiagramToolWindowPane), 0, true) as DiagramToolWindowPane;
                if (_ToolWindowPane == null)
                {
                    return;
                }
            }

            _ToolWindowPane.SetContent(Mermaid, IsFlow);
            IVsWindowFrame Frame = _ToolWindowPane.Frame as IVsWindowFrame;
            if (Frame != null)
            {
                Frame.Show();
            }
        }

        public static void RegenerateDiagram(bool IsFlow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GenerateAndShowDiagram(IsFlow);
        }

        private static void ShowError(string Message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_ToolWindowPane == null)
            {
                _ToolWindowPane = _Package.FindToolWindow(typeof(DiagramToolWindowPane), 0, true) as DiagramToolWindowPane;
            }

            if (_ToolWindowPane != null)
            {
                _ToolWindowPane.ShowError(Message);
                IVsWindowFrame Frame = _ToolWindowPane.Frame as IVsWindowFrame;
                if (Frame != null)
                {
                    Frame.Show();
                }
            }
        }
    }
}
