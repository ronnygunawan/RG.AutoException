using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RG.AutoException.Internals
{
    internal class ThrowSyntaxReceiver : ISyntaxContextReceiver
    {
        public HashSet<string> MissingExceptions = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            switch (context.Node)
            {
                case ThrowExpressionSyntax
                {
                    Expression: ObjectCreationExpressionSyntax
                    {
                        ArgumentList.Arguments.Count: 0,
                        Type: IdentifierNameSyntax { Identifier.ValueText: string exceptionName } typeSyntax
                    }
                } throwExpressionSyntax when (exceptionName.EndsWith("Exception") && !exceptionName.Contains(".") && context.SemanticModel.GetSymbolInfo(typeSyntax).Symbol is null):
                    MissingExceptions.Add(exceptionName);
                    break;
                case ThrowStatementSyntax
                {
                    Expression: ObjectCreationExpressionSyntax
                    {
                        ArgumentList.Arguments.Count: 0,
                        Type: IdentifierNameSyntax { Identifier.ValueText: string exceptionName } typeSyntax
                    }
                } throwStatementSyntax when(exceptionName.EndsWith("Exception") && !exceptionName.Contains(".") && context.SemanticModel.GetSymbolInfo(typeSyntax).Symbol is null):
                    MissingExceptions.Add(exceptionName);
                    break;
            }
        }
    }
}
