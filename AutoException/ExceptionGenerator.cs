using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace RG.AutoException
{
    [Generator]
    public class ExceptionGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all throw expressions and statements with potential missing exceptions
            IncrementalValuesProvider<string> missingExceptions = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsThrowWithObjectCreation(node),
                    transform: static (ctx, _) => GetMissingExceptionName(ctx))
                .Where(static name => name is not null)!;

            // Collect unique exception names
            IncrementalValueProvider<ImmutableArray<string>> uniqueExceptions = missingExceptions
                .Collect()
                .Select(static (names, _) => names.Distinct().ToImmutableArray());

            // Register the source output
            context.RegisterSourceOutput(uniqueExceptions, static (ctx, exceptions) =>
            {
                foreach (string exceptionName in exceptions)
                {
                    ctx.AddSource(
                        hintName: exceptionName,
                        sourceText: SourceText.From(
                            $$"""
                            using System;

                            namespace GeneratedExceptions
                            {
                                public sealed class {{exceptionName}} : Exception
                                {
                                    public {{exceptionName}}() : base() { }
                                    public {{exceptionName}}(string message) : base(message) { }
                                    public {{exceptionName}}(string message, Exception innerException) : base(message, innerException) { }
                                }
                            }
                            """,
                            encoding: Encoding.UTF8
                        ));
                }
            });
        }

        private static bool IsThrowWithObjectCreation(SyntaxNode node)
        {
            return node switch
            {
                ThrowExpressionSyntax { Expression: ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: <= 2, Type: IdentifierNameSyntax } } => true,
                ThrowStatementSyntax { Expression: ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: <= 2, Type: IdentifierNameSyntax } } => true,
                _ => false
            };
        }

        private static string? GetMissingExceptionName(GeneratorSyntaxContext context)
        {
            ObjectCreationExpressionSyntax? objectCreation = context.Node switch
            {
                ThrowExpressionSyntax throwExpr => throwExpr.Expression as ObjectCreationExpressionSyntax,
                ThrowStatementSyntax throwStmt => throwStmt.Expression as ObjectCreationExpressionSyntax,
                _ => null
            };

            if (objectCreation?.Type is not IdentifierNameSyntax typeSyntax)
            {
                return null;
            }

            string exceptionName = typeSyntax.Identifier.ValueText;

            // Check if it's a valid exception name and symbol is not found
            if (exceptionName.EndsWith("Exception")
                && !exceptionName.Contains(".")
                && context.SemanticModel.GetSymbolInfo(typeSyntax).Symbol is null)
            {
                return exceptionName;
            }

            return null;
        }
    }
}
