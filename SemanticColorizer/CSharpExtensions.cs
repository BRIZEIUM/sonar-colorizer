using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarColorizer
{
    public static class CSharpExtensions
    {
        public static SyntaxKind CSharpKind(this SyntaxNode node) {
            return node.Kind();
        }

        public static bool IsCSharpPredefinedTypeSyntax(this SyntaxNode node)
        {
            return node is PredefinedTypeSyntax;
        }
    }
}
