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

        // Represents information about a constructor extracted from base class
        private sealed class ConstructorInfo
        {
            public string Parameters { get; }
            public string BaseCallArgs { get; }

            public ConstructorInfo(string parameters, string baseCallArgs)
            {
                Parameters = parameters;
                BaseCallArgs = baseCallArgs;
            }

            public override bool Equals(object? obj) =>
                obj is ConstructorInfo other && Parameters == other.Parameters && BaseCallArgs == other.BaseCallArgs;

            public override int GetHashCode() => (Parameters, BaseCallArgs).GetHashCode();
        }

        // Represents exception info including its properties and base class
        private sealed class ExceptionInfo
        {
            public string Name { get; }
            public ImmutableArray<PropertyInfo> Properties { get; }
            public string? BaseClassName { get; }
            public ImmutableArray<ConstructorInfo> Constructors { get; }

            public ExceptionInfo(string name, ImmutableArray<PropertyInfo> properties, string? baseClassName = null, ImmutableArray<ConstructorInfo> constructors = default)
            {
                Name = name;
                Properties = properties;
                BaseClassName = baseClassName;
                Constructors = constructors.IsDefault ? ImmutableArray<ConstructorInfo>.Empty : constructors;
            }

            public override bool Equals(object? obj) =>
                obj is ExceptionInfo other && Name == other.Name && Properties.SequenceEqual(other.Properties) && BaseClassName == other.BaseClassName && Constructors.SequenceEqual(other.Constructors);

            public override int GetHashCode()
            {
                int hash = Name.GetHashCode();
                foreach (PropertyInfo prop in Properties)
                {
                    hash = (hash * 397) ^ prop.GetHashCode();
                }
                if (BaseClassName is not null)
                {
                    hash = (hash * 397) ^ BaseClassName.GetHashCode();
                }
                foreach (ConstructorInfo ctor in Constructors)
                {
                    hash = (hash * 397) ^ ctor.GetHashCode();
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
                foreach (ExceptionInfo exceptionInfo in exceptions)
                {
                    string propertiesSource = GenerateProperties(exceptionInfo.Properties);
                    string baseClassName = exceptionInfo.BaseClassName ?? "Exception";
                    string constructors = GenerateConstructors(exceptionInfo.Name, exceptionInfo.Constructors);

                    ctx.AddSource(
                        hintName: exceptionInfo.Name,
                        sourceText: SourceText.From(
                            $$"""
                            using System;

                            namespace GeneratedExceptions
                            {
                                public sealed class {{exceptionInfo.Name}} : {{baseClassName}}
                                {
                            {{constructors}}{{propertiesSource}}
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
                // Support explicit cast expressions: throw (ArgumentException)new MyException()
                ThrowExpressionSyntax { Expression: CastExpressionSyntax { Expression: ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax } objectCreation } }
                    => HasValidArgumentCount(objectCreation),
                ThrowStatementSyntax { Expression: CastExpressionSyntax { Expression: ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax } objectCreation } }
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
            ObjectCreationExpressionSyntax? objectCreation = null;
            CastExpressionSyntax? castExpression = null;

            switch (context.Node)
            {
                case ThrowExpressionSyntax throwExpr:
                    if (throwExpr.Expression is CastExpressionSyntax castExpr1)
                    {
                        castExpression = castExpr1;
                        objectCreation = castExpr1.Expression as ObjectCreationExpressionSyntax;
                    }
                    else
                    {
                        objectCreation = throwExpr.Expression as ObjectCreationExpressionSyntax;
                    }
                    break;
                case ThrowStatementSyntax throwStmt:
                    if (throwStmt.Expression is CastExpressionSyntax castExpr2)
                    {
                        castExpression = castExpr2;
                        objectCreation = castExpr2.Expression as ObjectCreationExpressionSyntax;
                    }
                    else
                    {
                        objectCreation = throwStmt.Expression as ObjectCreationExpressionSyntax;
                    }
                    break;
            }

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

            // Extract base class from cast expression if present
            string? baseClassName = null;
            ImmutableArray<ConstructorInfo> constructors = ImmutableArray<ConstructorInfo>.Empty;
            if (castExpression is not null)
            {
                ITypeSymbol? castType = context.SemanticModel.GetTypeInfo(castExpression.Type).Type;
                if (castType is INamedTypeSymbol namedType && IsExceptionType(castType))
                {
                    baseClassName = castType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    constructors = ExtractPublicConstructors(namedType);
                }
            }

            // Extract properties from object initializer
            ImmutableArray<PropertyInfo> properties = ExtractProperties(objectCreation, context.SemanticModel);

            return new ExceptionInfo(exceptionName, properties, baseClassName, constructors);
        }

        private static ImmutableArray<ConstructorInfo> ExtractPublicConstructors(INamedTypeSymbol typeSymbol)
        {
            var constructors = new List<ConstructorInfo>();

            foreach (IMethodSymbol ctor in typeSymbol.InstanceConstructors)
            {
                // Only include public constructors (not protected)
                if (ctor.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                // Build parameter list and base call arguments
                var parameters = new List<string>();
                var baseCallArgs = new List<string>();

                foreach (IParameterSymbol param in ctor.Parameters)
                {
                    string typeName = GetParameterTypeNameWithNullability(param.Type);
                    parameters.Add($"{typeName} {param.Name}");
                    baseCallArgs.Add(param.Name);
                }

                constructors.Add(new ConstructorInfo(
                    string.Join(", ", parameters),
                    string.Join(", ", baseCallArgs)
                ));
            }

            return constructors.ToImmutableArray();
        }

        private static string GetParameterTypeNameWithNullability(ITypeSymbol typeSymbol)
        {
            // Get the base type name
            string baseName = typeSymbol.SpecialType switch
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
                SpecialType.System_Object => "object",
                _ => typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            };

            // Add nullable modifier for reference types
            // We always add ? for reference types to match modern C# nullable annotations
            if (typeSymbol.IsReferenceType && !baseName.EndsWith("?"))
            {
                return baseName + "?";
            }

            return baseName;
        }

        private static bool IsExceptionType(ITypeSymbol type)
        {
            ITypeSymbol? current = type;
            while (current is not null)
            {
                if (current.ToDisplayString() == "System.Exception")
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
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

                    // Determine base class - check for conflicts
                    var baseClasses = g
                        .Select(e => e!.BaseClassName)
                        .Where(b => b is not null)
                        .Distinct()
                        .ToList();

                    string? baseClassName = null;
                    ImmutableArray<ConstructorInfo> constructors = ImmutableArray<ConstructorInfo>.Empty;
                    if (baseClasses.Count == 1)
                    {
                        baseClassName = baseClasses[0];
                        // Get constructors from the first exception with this base class
                        constructors = g.First(e => e!.BaseClassName == baseClassName)!.Constructors;
                    }
                    else if (baseClasses.Count > 1)
                    {
                        // Conflicting base classes
                        baseClassName = "ConflictingType";
                    }

                    return new ExceptionInfo(name, allProperties, baseClassName, constructors);
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

        // Default constructors for Exception base class when no cast expression is used
        private static readonly ImmutableArray<ConstructorInfo> DefaultExceptionConstructors = ImmutableArray.Create(
            new ConstructorInfo("", ""),
            new ConstructorInfo("string? message", "message"),
            new ConstructorInfo("string? message, Exception? innerException", "message, innerException")
        );

        private static string GenerateConstructors(string exceptionName, ImmutableArray<ConstructorInfo> constructors)
        {
            var sb = new StringBuilder();

            // If no constructors were extracted (no cast expression), use default Exception constructors
            var ctorsToGenerate = constructors.IsEmpty ? DefaultExceptionConstructors : constructors;

            foreach (var ctor in ctorsToGenerate)
            {
                sb.AppendLine($"        public {exceptionName}({ctor.Parameters}) : base({ctor.BaseCallArgs}) {{ }}");
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }
    }
}
