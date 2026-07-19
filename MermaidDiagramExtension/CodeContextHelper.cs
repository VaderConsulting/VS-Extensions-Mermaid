using System;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MermaidDiagramExtension
{
    internal static class CodeContextHelper
    {
        public static bool IsCSharpEditorContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetTextView() != null && IsCSharpFile();
        }

        public static string GetSelectedOrEnclosingCode()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ITextView TextView = GetTextView();
            if (TextView == null)
            {
                return null;
            }

            if (!IsCSharpFile())
            {
                return null;
            }

            SnapshotSpan? Selection = GetSelectionOrCaretSpan(TextView);
            if (Selection == null)
            {
                return null;
            }

            return Selection.Value.GetText();
        }

        private static ITextView GetTextView()
        {
            IServiceProvider ServiceProvider = MermaidDiagramPackage.ServiceProvider;
            if (ServiceProvider == null)
            {
                return null;
            }

            IVsTextManager TextManager = ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            if (TextManager == null)
            {
                return null;
            }

            TextManager.GetActiveView(1, null, out IVsTextView TextView);
            if (TextView == null)
            {
                return null;
            }

            return GetEditorAdaptersFactory().GetWpfTextView(TextView);
        }

        private static bool IsCSharpFile()
        {
            ITextView TextView = GetTextView();
            if (TextView == null)
            {
                return false;
            }

            return TextView.TextBuffer.ContentType.IsOfType("CSharp");
        }

        private static SnapshotSpan? GetSelectionOrCaretSpan(ITextView TextView)
        {
            SnapshotSpan? Span = GetSelectedSpan(TextView);
            if (Span != null)
            {
                return Span;
            }

            return GetEnclosingMethodOrBlockSpan(TextView);
        }

        private static SnapshotSpan? GetSelectedSpan(ITextView TextView)
        {
            if (TextView.Selection.IsEmpty)
            {
                return null;
            }

            return new SnapshotSpan(TextView.Selection.Start.Position, TextView.Selection.End.Position);
        }

        private static SnapshotSpan? GetEnclosingMethodOrBlockSpan(ITextView TextView)
        {
            Microsoft.VisualStudio.Text.SnapshotPoint Caret = TextView.Caret.Position.BufferPosition;
            ITextSnapshot Snapshot = Caret.Snapshot;
            string Text = Snapshot.GetText();
            int Position = Caret.Position;

            int Start = FindMethodOrBlockStart(Text, Position);
            int End = FindMethodOrBlockEnd(Text, Position);
            if (Start < 0 || End <= Start)
            {
                return null;
            }

            return new SnapshotSpan(Snapshot, Start, End - Start);
        }

        private static int FindMethodOrBlockStart(string Text, int Position)
        {
            int SearchStart = Math.Max(0, Position - 1);
            int BraceDepth = 0;
            bool InMethod = false;
            int MethodStart = -1;

            for (int I = SearchStart; I >= 0; I--)
            {
                char C = Text[I];
                if (C == '}')
                {
                    BraceDepth++;
                }
                else if (C == '{')
                {
                    if (BraceDepth == 0)
                    {
                        MethodStart = I;
                        InMethod = true;
                        break;
                    }
                    BraceDepth--;
                }
            }

            if (!InMethod || MethodStart < 0)
            {
                return -1;
            }

            for (int I = MethodStart - 1; I >= 0; I--)
            {
                if (IsMethodDeclarationStart(Text, I))
                {
                    return SkipBackwardWhitespaceAndNewline(Text, I);
                }
            }

            return MethodStart;
        }

        private static bool IsMethodDeclarationStart(string Text, int Index)
        {
            if (Index <= 0 || Index >= Text.Length)
            {
                return false;
            }

            if (Text[Index] != '(')
            {
                return false;
            }

            int ParenDepth = 1;
            for (int I = Index - 1; I >= 0; I--)
            {
                char C = Text[I];
                if (C == ')')
                {
                    ParenDepth++;
                }
                else if (C == '(')
                {
                    ParenDepth--;
                    if (ParenDepth == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int SkipBackwardWhitespaceAndNewline(string Text, int Index)
        {
            while (Index > 0 && (char.IsWhiteSpace(Text[Index - 1]) || Text[Index - 1] == '\r' || Text[Index - 1] == '\n'))
            {
                Index--;
            }

            return Index;
        }

        private static int FindMethodOrBlockEnd(string Text, int Position)
        {
            int BraceDepth = 0;
            bool FoundStart = false;
            int Start = Math.Max(0, Position - 1);

            for (int I = Start; I >= 0; I--)
            {
                if (Text[I] == '}')
                {
                    BraceDepth++;
                }
                else if (Text[I] == '{')
                {
                    if (BraceDepth == 0)
                    {
                        FoundStart = true;
                        break;
                    }
                    BraceDepth--;
                }
            }

            if (!FoundStart)
            {
                return -1;
            }

            BraceDepth = 0;
            for (int I = Position; I < Text.Length; I++)
            {
                char C = Text[I];
                if (C == '{')
                {
                    BraceDepth++;
                }
                else if (C == '}')
                {
                    BraceDepth--;
                    if (BraceDepth < 0)
                    {
                        return I + 1;
                    }
                }
            }

            return -1;
        }

        private static Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService GetEditorAdaptersFactory()
        {
            IServiceProvider ServiceProvider = MermaidDiagramPackage.ServiceProvider;
            if (ServiceProvider == null)
            {
                return null;
            }

            return ServiceProvider.GetService(typeof(Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService)) as Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService;
        }
    }
}
