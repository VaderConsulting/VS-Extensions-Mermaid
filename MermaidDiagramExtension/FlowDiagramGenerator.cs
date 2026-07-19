using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MermaidDiagramExtension
{
    internal static class FlowDiagramGenerator
    {
        private const string PREFIX = "N";
        private static int _NodeId;

        public static void Generate(string Code, out string Mermaid, out string ErrorMessage)
        {
            Mermaid = null;
            ErrorMessage = null;
            _NodeId = 0;

            SyntaxTree Tree = CSharpSyntaxTree.ParseText(Code);
            if (Tree.GetDiagnostics().Any())
            {
                ErrorMessage = "Unable to generate diagram from the selected code. The code may contain syntax errors.";
                return;
            }

            CompilationUnitSyntax Root = (CompilationUnitSyntax)Tree.GetRoot();
            BlockSyntax Block = FindFirstMethodOrBlock(Root);
            if (Block == null)
            {
                ErrorMessage = "Unable to generate diagram from the selected code. No method or block found.";
                return;
            }

            StringBuilder Sb = new StringBuilder();
            Sb.AppendLine("flowchart TD");
            HashSet<string> Defined = new HashSet<string>();
            string StartId = EmitNode(Sb, "Start", Defined);
            string ExitId = EmitNode(Sb, "End", Defined);
            string FirstId = ProcessBlock(Sb, Block, Defined);
            Sb.AppendLine(StartId + " --> " + FirstId);
            Sb.AppendLine(ExitId);
            Mermaid = Sb.ToString();
        }

        private static BlockSyntax FindFirstMethodOrBlock(SyntaxNode Node)
        {
            if (Node is MethodDeclarationSyntax Method)
            {
                return Method.Body;
            }

            if (Node is BlockSyntax Block)
            {
                return Block;
            }

            foreach (SyntaxNode Child in Node.ChildNodes())
            {
                BlockSyntax Found = FindFirstMethodOrBlock(Child);
                if (Found != null)
                {
                    return Found;
                }
            }

            return null;
        }

        private static string ProcessBlock(StringBuilder Sb, BlockSyntax Block, HashSet<string> Defined)
        {
            if (Block == null || Block.Statements.Count == 0)
            {
                return EmitNode(Sb, "…", Defined);
            }

            StatementSyntax First = Block.Statements[0];
            return ProcessStatement(Sb, First, Block.Statements, 0, Defined);
        }

        private static string ProcessStatement(StringBuilder Sb, StatementSyntax Stmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            if (Stmt is IfStatementSyntax IfStmt)
            {
                return ProcessIf(Sb, IfStmt, Siblings, Index, Defined);
            }

            if (Stmt is SwitchStatementSyntax SwitchStmt)
            {
                return ProcessSwitch(Sb, SwitchStmt, Siblings, Index, Defined);
            }

            if (Stmt is ForStatementSyntax ForStmt)
            {
                return ProcessFor(Sb, ForStmt, Siblings, Index, Defined);
            }

            if (Stmt is ForEachStatementSyntax ForEachStmt)
            {
                return ProcessForEach(Sb, ForEachStmt, Siblings, Index, Defined);
            }

            if (Stmt is WhileStatementSyntax WhileStmt)
            {
                return ProcessWhile(Sb, WhileStmt, Siblings, Index, Defined);
            }

            if (Stmt is DoStatementSyntax DoStmt)
            {
                return ProcessDo(Sb, DoStmt, Siblings, Index, Defined);
            }

            if (Stmt is ReturnStatementSyntax ReturnStmt)
            {
                string Id = EmitNode(Sb, "Return", Defined);
                return Id;
            }

            if (Stmt is ThrowStatementSyntax)
            {
                string Id = EmitNode(Sb, "Throw", Defined);
                return Id;
            }

            if (Stmt is ExpressionStatementSyntax ExprStmt)
            {
                string Label = ExprStmt.Expression.ToString().Replace("\"", "'");
                if (Label.Length > 40)
                {
                    Label = Label.Substring(0, 37) + "...";
                }

                string Id = EmitNode(Sb, Label, Defined);
                string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
                if (NextId != null)
                {
                    Sb.AppendLine(Id + " --> " + NextId);
                }

                return Id;
            }

            if (Stmt is LocalDeclarationStatementSyntax)
            {
                string Id = EmitNode(Sb, "…", Defined);
                string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
                if (NextId != null)
                {
                    Sb.AppendLine(Id + " --> " + NextId);
                }

                return Id;
            }

            if (Stmt is BlockSyntax InnerBlock)
            {
                return ProcessBlock(Sb, InnerBlock, Defined);
            }

            string FallbackId = EmitNode(Sb, "…", Defined);
            string Next = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (Next != null)
            {
                Sb.AppendLine(FallbackId + " --> " + Next);
            }

            return FallbackId;
        }

        private static string ProcessIf(StringBuilder Sb, IfStatementSyntax IfStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string CondLabel = IfStmt.Condition.ToString().Replace("\"", "'");
            if (CondLabel.Length > 30)
            {
                CondLabel = CondLabel.Substring(0, 27) + "...";
            }

            string CondId = EmitDecision(Sb, CondLabel, Defined);
            string ThenId = ProcessStatement(Sb, IfStmt.Statement, IfStmt.Statement is BlockSyntax B ? B.Statements : new SyntaxList<StatementSyntax>().Add(IfStmt.Statement), 0, Defined);
            Sb.AppendLine(CondId + " -->|True| " + ThenId);

            string ElseId = null;
            if (IfStmt.Else != null)
            {
                ElseId = ProcessStatement(Sb, IfStmt.Else.Statement, IfStmt.Else.Statement is BlockSyntax B2 ? B2.Statements : new SyntaxList<StatementSyntax>().Add(IfStmt.Else.Statement), 0, Defined);
                Sb.AppendLine(CondId + " -->|False| " + ElseId);
            }

            string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (NextId != null)
            {
                Sb.AppendLine(ThenId + " --> " + NextId);
                if (ElseId != null)
                {
                    Sb.AppendLine(ElseId + " --> " + NextId);
                }
            }

            return CondId;
        }

        private static string ProcessSwitch(StringBuilder Sb, SwitchStatementSyntax SwitchStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string CondId = EmitDecision(Sb, "switch", Defined);
            foreach (SwitchSectionSyntax Section in SwitchStmt.Sections)
            {
                string Label = Section.Labels.ToString().Replace("\"", "'").Replace("\r\n", " ");
                string CaseId = Section.Statements.Count > 0
                    ? ProcessStatement(Sb, Section.Statements[0], Section.Statements, 0, Defined)
                    : EmitNode(Sb, "…", Defined);
                Sb.AppendLine(CondId + " -->|" + Label + "| " + CaseId);
                string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
                if (NextId != null)
                {
                    Sb.AppendLine(CaseId + " --> " + NextId);
                }
            }

            return CondId;
        }

        private static string ProcessFor(StringBuilder Sb, ForStatementSyntax ForStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string LoopId = EmitNode(Sb, "for", Defined);
            string BodyId = ProcessStatement(Sb, ForStmt.Statement, ForStmt.Statement is BlockSyntax B ? B.Statements : new SyntaxList<StatementSyntax>().Add(ForStmt.Statement), 0, Defined);
            Sb.AppendLine(LoopId + " --> " + BodyId);
            Sb.AppendLine(BodyId + " --> " + LoopId);
            string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (NextId != null)
            {
                Sb.AppendLine(LoopId + " -->|exit| " + NextId);
            }

            return LoopId;
        }

        private static string ProcessForEach(StringBuilder Sb, ForEachStatementSyntax ForEachStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string LoopId = EmitNode(Sb, "foreach", Defined);
            string BodyId = ProcessStatement(Sb, ForEachStmt.Statement, ForEachStmt.Statement is BlockSyntax B ? B.Statements : new SyntaxList<StatementSyntax>().Add(ForEachStmt.Statement), 0, Defined);
            Sb.AppendLine(LoopId + " --> " + BodyId);
            Sb.AppendLine(BodyId + " --> " + LoopId);
            string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (NextId != null)
            {
                Sb.AppendLine(LoopId + " -->|done| " + NextId);
            }

            return LoopId;
        }

        private static string ProcessWhile(StringBuilder Sb, WhileStatementSyntax WhileStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string CondLabel = WhileStmt.Condition.ToString().Replace("\"", "'");
            string CondId = EmitDecision(Sb, CondLabel, Defined);
            string BodyId = ProcessStatement(Sb, WhileStmt.Statement, WhileStmt.Statement is BlockSyntax B ? B.Statements : new SyntaxList<StatementSyntax>().Add(WhileStmt.Statement), 0, Defined);
            Sb.AppendLine(CondId + " -->|True| " + BodyId);
            Sb.AppendLine(BodyId + " --> " + CondId);
            string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (NextId != null)
            {
                Sb.AppendLine(CondId + " -->|False| " + NextId);
            }

            return CondId;
        }

        private static string ProcessDo(StringBuilder Sb, DoStatementSyntax DoStmt, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            string BodyId = ProcessStatement(Sb, DoStmt.Statement, DoStmt.Statement is BlockSyntax B ? B.Statements : new SyntaxList<StatementSyntax>().Add(DoStmt.Statement), 0, Defined);
            string CondId = EmitDecision(Sb, "condition", Defined);
            Sb.AppendLine(BodyId + " --> " + CondId);
            Sb.AppendLine(CondId + " -->|True| " + BodyId);
            string NextId = GetNextStatementId(Sb, Siblings, Index, Defined);
            if (NextId != null)
            {
                Sb.AppendLine(CondId + " -->|False| " + NextId);
            }

            return BodyId;
        }

        private static string GetNextStatementId(StringBuilder Sb, SyntaxList<StatementSyntax> Siblings, int Index, HashSet<string> Defined)
        {
            if (Index + 1 >= Siblings.Count)
            {
                return null;
            }

            return ProcessStatement(Sb, Siblings[Index + 1], Siblings, Index + 1, Defined);
        }

        private static string EmitNode(StringBuilder Sb, string Label, HashSet<string> Defined)
        {
            string Id = PREFIX + (_NodeId++);
            Defined.Add(Id);
            string Escaped = Label.Replace("[", "[").Replace("]", "]");
            Sb.AppendLine(Id + "[" + Escaped + "]");
            return Id;
        }

        private static string EmitDecision(StringBuilder Sb, string Label, HashSet<string> Defined)
        {
            string Id = PREFIX + (_NodeId++);
            Defined.Add(Id);
            string Escaped = Label.Replace("{", "(").Replace("}", ")");
            Sb.AppendLine(Id + "{" + Escaped + "}");
            return Id;
        }
    }
}
