using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MermaidDiagramExtension
{
    internal static class SequenceDiagramGenerator
    {
        public static void Generate(string Code, out string Mermaid, out string ErrorMessage)
        {
            Mermaid = null;
            ErrorMessage = null;

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

            List<InvocationInfo> Invocations = new List<InvocationInfo>();
            CollectInvocations(Block, null, Invocations);

            if (Invocations.Count == 0)
            {
                Mermaid = "sequenceDiagram\n    participant Caller\n    Note over Caller: No method calls found in selection.";
                return;
            }

            StringBuilder Sb = new StringBuilder();
            Sb.AppendLine("sequenceDiagram");
            HashSet<string> Participants = new HashSet<string>();
            string Caller = "Caller";
            Participants.Add(Caller);

            foreach (InvocationInfo Inv in Invocations)
            {
                string Callee = SanitizeParticipant(Inv.Receiver ?? "Target");
                if (!Participants.Contains(Callee))
                {
                    Sb.AppendLine("    participant " + Callee);
                    Participants.Add(Callee);
                }

                string MethodName = Inv.MethodName + "()";
                if (Inv.IsReturn)
                {
                    Sb.AppendLine("    " + Callee + "-->>" + Caller + ": " + MethodName);
                }
                else
                {
                    Sb.AppendLine("    " + Caller + "->>+" + Callee + ": " + MethodName);
                }
            }

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

        private static void CollectInvocations(SyntaxNode Node, string CurrentActor, List<InvocationInfo> Invocations)
        {
            if (Node is InvocationExpressionSyntax Invocation)
            {
                string Receiver = GetReceiverName(Invocation);
                string MethodName = GetMethodName(Invocation);
                Invocations.Add(new InvocationInfo { Receiver = Receiver, MethodName = MethodName, IsReturn = false });
                return;
            }

            foreach (SyntaxNode Child in Node.ChildNodes())
            {
                CollectInvocations(Child, CurrentActor, Invocations);
            }
        }

        private static string GetReceiverName(InvocationExpressionSyntax Invocation)
        {
            ExpressionSyntax Expr = Invocation.Expression;
            if (Expr is MemberAccessExpressionSyntax Member)
            {
                return Member.Expression.ToString();
            }

            if (Expr is IdentifierNameSyntax)
            {
                return null;
            }

            return Expr.ToString();
        }

        private static string GetMethodName(InvocationExpressionSyntax Invocation)
        {
            ExpressionSyntax Expr = Invocation.Expression;
            if (Expr is MemberAccessExpressionSyntax Member)
            {
                return Member.Name.ToString();
            }

            if (Expr is IdentifierNameSyntax Id)
            {
                return Id.ToString();
            }

            return "Call";
        }

        private static string SanitizeParticipant(string Name)
        {
            string Trimmed = Name.Trim();
            if (Trimmed.Length > 30)
            {
                Trimmed = Trimmed.Substring(0, 27) + "...";
            }

            return Trimmed.Replace("-", "_").Replace(" ", "_");
        }

        private class InvocationInfo
        {
            public string Receiver { get; set; }
            public string MethodName { get; set; }
            public bool IsReturn { get; set; }
        }
    }
}
