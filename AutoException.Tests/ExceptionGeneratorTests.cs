using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RG.AutoException;

namespace AutoException.Tests;

public class ExceptionGeneratorTests
{
    [Fact]
    public void GeneratesBasicException()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new MyTestException();
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception exists
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MyTestException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class MyTestException : Exception", generatedCode);
        Assert.Contains("public MyTestException() : base() { }", generatedCode);
        Assert.Contains("public MyTestException(string message) : base(message) { }", generatedCode);
        Assert.Contains("public MyTestException(string message, Exception innerException) : base(message, innerException) { }", generatedCode);
    }

    [Fact]
    public void GeneratesExceptionWithStringProperty()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new StupidUserException
                        {
                            Name = "Bambang"
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception has the property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("StupidUserException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class StupidUserException : Exception", generatedCode);
        Assert.Contains("public string? Name { get; init; }", generatedCode);
    }

    [Fact]
    public void GeneratesExceptionWithMultipleProperties()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new MultiPropException
                        {
                            UserId = 42,
                            UserName = "TestUser",
                            IsActive = true
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception has all properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MultiPropException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class MultiPropException : Exception", generatedCode);
        Assert.Contains("public int? UserId { get; init; }", generatedCode);
        Assert.Contains("public string? UserName { get; init; }", generatedCode);
        Assert.Contains("public bool? IsActive { get; init; }", generatedCode);
    }

    [Fact]
    public void MergesPropertiesFromMultipleUsages()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod1()
                    {
                        throw new MergedPropsException
                        {
                            Name = "Test"
                        };
                    }

                    public void TestMethod2()
                    {
                        throw new MergedPropsException
                        {
                            Age = 25
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception has merged properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MergedPropsException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class MergedPropsException : Exception", generatedCode);
        Assert.Contains("public string? Name { get; init; }", generatedCode);
        Assert.Contains("public int? Age { get; init; }", generatedCode);
    }

    [Fact]
    public void GeneratesConflictingTypeForMismatchedTypes()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod1()
                    {
                        throw new ConflictException
                        {
                            Id = "bambang"
                        };
                    }

                    public void TestMethod2()
                    {
                        throw new ConflictException
                        {
                            Id = 1024
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert - We expect errors because ConflictingType can't be assigned
        // but the generated code should contain ConflictingType
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ConflictException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class ConflictException : Exception", generatedCode);
        Assert.Contains("public ConflictingType? Id { get; init; }", generatedCode);

        // Verify ConflictingType is also generated
        var conflictingTypeSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ConflictingType") && !st.FilePath.Contains("ConflictException"));
        Assert.NotNull(conflictingTypeSyntaxTree);

        string conflictingTypeCode = conflictingTypeSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class ConflictingType", conflictingTypeCode);
    }

    [Fact]
    public void SupportsVariousPrimitiveTypes()
    {
        // Arrange
        string source = """
            using System;
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new PrimitiveTypesException
                        {
                            IntValue = 42,
                            LongValue = 100L,
                            DoubleValue = 3.14,
                            DecimalValue = 99.99m,
                            BoolValue = true,
                            CharValue = 'A',
                            StringValue = "test"
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception has all primitive type properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("PrimitiveTypesException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public int? IntValue { get; init; }", generatedCode);
        Assert.Contains("public long? LongValue { get; init; }", generatedCode);
        Assert.Contains("public double? DoubleValue { get; init; }", generatedCode);
        Assert.Contains("public decimal? DecimalValue { get; init; }", generatedCode);
        Assert.Contains("public bool? BoolValue { get; init; }", generatedCode);
        Assert.Contains("public char? CharValue { get; init; }", generatedCode);
        Assert.Contains("public string? StringValue { get; init; }", generatedCode);
    }

    [Fact]
    public void DoesNotGeneratePropertiesForNonPrimitiveTypes()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class SomeClass { }

                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new NonPrimitiveException
                        {
                            Name = "Valid"
                        };
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception only has the primitive property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("NonPrimitiveException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public string? Name { get; init; }", generatedCode);
        // Should not contain any property for the non-primitive type
        Assert.DoesNotContain("SomeClass", generatedCode);
    }

    [Fact]
    public void IgnoresExistingExceptions()
    {
        // Arrange
        string source = """
            using System;
            namespace TestCode
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                        throw new ArgumentException("test");
                    }
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify no exception was generated for ArgumentException
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ArgumentException") && st.FilePath.Contains(".g.cs"));
        Assert.Null(generatedSyntaxTree);
    }

    [Fact]
    public void WorksWithThrowExpression()
    {
        // Arrange
        string source = """
            namespace TestCode
            {
                public class TestClass
                {
                    public string GetValue(string? input) =>
                        input ?? throw new ThrowExprException { Reason = "input was null" };
                }
            }
            """;

        // Act
        var (compilation, diagnostics) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify the generated exception exists with property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ThrowExprException"));
        Assert.NotNull(generatedSyntaxTree);

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        Assert.Contains("public sealed class ThrowExprException : Exception", generatedCode);
        Assert.Contains("public string? Reason { get; init; }", generatedCode);
    }

    private static (Compilation, ImmutableArray<Diagnostic>) RunGenerator(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Exception).Assembly.Location),
        ];

        // Add runtime references
        string runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestCompilation",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ExceptionGenerator();

        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> diagnostics);

        return (outputCompilation, diagnostics);
    }
}
