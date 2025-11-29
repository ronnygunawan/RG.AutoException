using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace RG.AutoException
{
    [Generator]
    public class ExceptionGenerator : IIncrementalGenerator
    {
        // Represents a property found in an object initializer
        private sealed class PropertyInfo
        {
            public string Name { get; }
            public string TypeName { get; }

            public PropertyInfo(string name, string typeName)
            {
                Name = name;
                TypeName = typeName;
            }

            public override bool Equals(object? obj) =>
                obj is PropertyInfo other && Name == other.Name && TypeName == other.TypeName;

            public override int GetHashCode() => (Name, TypeName).GetHashCode();
        }

        // Represents exception info including its properties
        private sealed class ExceptionInfo
        {
            public string Name { get; }
            public ImmutableArray<PropertyInfo> Properties { get; }

            public ExceptionInfo(string name, ImmutableArray<PropertyInfo> properties)
            {
                Name = name;
                Properties = properties;
            }

            public override bool Equals(object? obj) =>
                obj is ExceptionInfo other && Name == other.Name && Properties.SequenceEqual(other.Properties);

            public override int GetHashCode()
            {
                int hash = Name.GetHashCode();
                foreach (PropertyInfo prop in Properties)
                {
                    hash = (hash * 397) ^ prop.GetHashCode();
                }
                return hash;
            }
        }

        // Primitive types that are supported for init-only properties
        private static readonly HashSet<string> SupportedPrimitiveTypes = new HashSet<string>
        {
            "string", "String", "System.String",
            "int", "Int32", "System.Int32",
            "long", "Int64", "System.Int64",
            "short", "Int16", "System.Int16",
            "byte", "Byte", "System.Byte",
            "sbyte", "SByte", "System.SByte",
            "uint", "UInt32", "System.UInt32",
            "ulong", "UInt64", "System.UInt64",
            "ushort", "UInt16", "System.UInt16",
            "float", "Single", "System.Single",
            "double", "Double", "System.Double",
            "decimal", "Decimal", "System.Decimal",
            "bool", "Boolean", "System.Boolean",
            "char", "Char", "System.Char",
            "Guid", "System.Guid",
            "DateTime", "System.DateTime",
            "DateTimeOffset", "System.DateTimeOffset",
            "TimeSpan", "System.TimeSpan"
        };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all throw expressions and statements with potential missing exceptions
            IncrementalValuesProvider<ExceptionInfo?> missingExceptions = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsThrowWithObjectCreation(node),
                    transform: static (ctx, _) => GetExceptionInfo(ctx))
                .Where(static info => info is not null);

            // Collect and group exceptions by name, merging properties
            IncrementalValueProvider<ImmutableArray<ExceptionInfo>> mergedExceptions = missingExceptions
                .Collect()
                .Select(static (exceptions, _) => MergeExceptions(exceptions!));

            // Register the source output
            context.RegisterSourceOutput(mergedExceptions, static (ctx, exceptions) =>
            {
                bool hasConflictingType = false;

                foreach (ExceptionInfo exceptionInfo in exceptions)
                {
                    string propertiesSource = GenerateProperties(exceptionInfo.Properties);

                    // Check if any property has ConflictingType
                    if (exceptionInfo.Properties.Any(p => p.TypeName == "ConflictingType"))
                    {
                        hasConflictingType = true;
                    }

                    ctx.AddSource(
                        hintName: exceptionInfo.Name,
                        sourceText: SourceText.From(
                            $$"""
                            using System;

                            namespace GeneratedExceptions
                            {
                                public sealed class {{exceptionInfo.Name}} : Exception
                                {
                                    public {{exceptionInfo.Name}}() : base() { }
                                    public {{exceptionInfo.Name}}(string message) : base(message) { }
                                    public {{exceptionInfo.Name}}(string message, Exception innerException) : base(message, innerException) { }
                            {{propertiesSource}}
                                }
                            }
                            """,
                            encoding: Encoding.UTF8
                        ));
                }

                // Generate ConflictingType class if needed
                if (hasConflictingType)
                {
                    ctx.AddSource(
                        hintName: "ConflictingType",
                        sourceText: SourceText.From(
                            """
                            namespace GeneratedExceptions
                            {
                                /// <summary>
                                /// This type indicates that a property has conflicting types across different usages.
                                /// Review the code and ensure consistent property types are used.
                                /// </summary>
                                public sealed class ConflictingType
                                {
                                    private ConflictingType() { }
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
                ThrowExpressionSyntax { Expression: ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax } objectCreation }
                    => HasValidArgumentCount(objectCreation),
                ThrowStatementSyntax { Expression: ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax } objectCreation }
                    => HasValidArgumentCount(objectCreation),
                _ => false
            };
        }

        private static bool HasValidArgumentCount(ObjectCreationExpressionSyntax objectCreation)
        {
            // Allow when there's no argument list (just initializer) or argument list has <= 2 args
            return objectCreation.ArgumentList is null || objectCreation.ArgumentList.Arguments.Count <= 2;
        }

        private static ExceptionInfo? GetExceptionInfo(GeneratorSyntaxContext context)
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
            if (!exceptionName.EndsWith("Exception")
                || exceptionName.Contains(".")
                || context.SemanticModel.GetSymbolInfo(typeSyntax).Symbol is not null)
            {
                return null;
            }

            // Extract properties from object initializer
            ImmutableArray<PropertyInfo> properties = ExtractProperties(objectCreation, context.SemanticModel);

            return new ExceptionInfo(exceptionName, properties);
        }

        private static ImmutableArray<PropertyInfo> ExtractProperties(
            ObjectCreationExpressionSyntax objectCreation,
            SemanticModel semanticModel)
        {
            if (objectCreation.Initializer is null)
            {
                return ImmutableArray<PropertyInfo>.Empty;
            }

            var properties = new List<PropertyInfo>();

            foreach (ExpressionSyntax expression in objectCreation.Initializer.Expressions)
            {
                if (expression is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax propertyName)
                {
                    // Get the type of the right-hand side expression
                    TypeInfo typeInfo = semanticModel.GetTypeInfo(assignment.Right);
                    ITypeSymbol? typeSymbol = typeInfo.Type;

                    if (typeSymbol is not null)
                    {
                        string typeName = GetSimpleTypeName(typeSymbol);

                        // Only include primitive types
                        if (IsSupportedPrimitiveType(typeName))
                        {
                            properties.Add(new PropertyInfo(propertyName.Identifier.ValueText, typeName));
                        }
                    }
                }
            }

            return properties.ToImmutableArray();
        }

        private static string GetSimpleTypeName(ITypeSymbol typeSymbol)
        {
            // Get the special type name for well-known types
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_String => "string",
                SpecialType.System_Int32 => "int",
                SpecialType.System_Int64 => "long",
                SpecialType.System_Int16 => "short",
                SpecialType.System_Byte => "byte",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Single => "float",
                SpecialType.System_Double => "double",
                SpecialType.System_Decimal => "decimal",
                SpecialType.System_Boolean => "bool",
                SpecialType.System_Char => "char",
                _ => typeSymbol.Name
            };
        }

        private static bool IsSupportedPrimitiveType(string typeName)
        {
            return SupportedPrimitiveTypes.Contains(typeName);
        }

        private static ImmutableArray<ExceptionInfo> MergeExceptions(ImmutableArray<ExceptionInfo?> exceptions)
        {
            // Group by exception name and merge properties
            var grouped = exceptions
                .Where(e => e is not null)
                .GroupBy(e => e!.Name)
                .Select(g =>
                {
                    string name = g.Key;

                    // Collect all properties for this exception
                    var allProperties = g
                        .SelectMany(e => e!.Properties)
                        .GroupBy(p => p.Name)
                        .Select(pg =>
                        {
                            var types = pg.Select(p => p.TypeName).Distinct().ToList();
                            if (types.Count == 1)
                            {
                                return new PropertyInfo(pg.Key, types[0]);
                            }
                            else
                            {
                                // Conflicting types
                                return new PropertyInfo(pg.Key, "ConflictingType");
                            }
                        })
                        .ToImmutableArray();

                    return new ExceptionInfo(name, allProperties);
                })
                .ToImmutableArray();

            return grouped;
        }

        private static string GenerateProperties(ImmutableArray<PropertyInfo> properties)
        {
            if (properties.IsEmpty)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (PropertyInfo prop in properties)
            {
                sb.AppendLine();
                sb.AppendLine($"        public {prop.TypeName}? {prop.Name} {{ get; init; }}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
