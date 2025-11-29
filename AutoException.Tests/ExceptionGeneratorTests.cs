using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RG.AutoException;
using Shouldly;

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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception exists
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MyTestException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class MyTestException : Exception");
        generatedCode.ShouldContain("public MyTestException() : base() { }");
        generatedCode.ShouldContain("public MyTestException(string message) : base(message) { }");
        generatedCode.ShouldContain("public MyTestException(string message, Exception innerException) : base(message, innerException) { }");
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception has the property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("StupidUserException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class StupidUserException : Exception");
        generatedCode.ShouldContain("public string? Name { get; init; }");
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception has all properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MultiPropException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class MultiPropException : Exception");
        generatedCode.ShouldContain("public int? UserId { get; init; }");
        generatedCode.ShouldContain("public string? UserName { get; init; }");
        generatedCode.ShouldContain("public bool? IsActive { get; init; }");
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception has merged properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("MergedPropsException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class MergedPropsException : Exception");
        generatedCode.ShouldContain("public string? Name { get; init; }");
        generatedCode.ShouldContain("public int? Age { get; init; }");
    }

    [Fact]
    public void DoesNotGeneratePropertyForConflictingTypes()
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

        // Assert - Generator diagnostics should be empty (generator runs successfully)
        diagnostics.ShouldBeEmpty();

        // Assert - The exception should be generated
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ConflictException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class ConflictException : Exception");
        
        // The property should use ConflictingType which doesn't exist,
        // causing a natural compilation error that guides the user
        generatedCode.ShouldContain("public ConflictingType? Id { get; init; }");

        // ConflictingType class should NOT be generated - let compilation fail naturally
        var conflictingTypeSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ConflictingType") && !st.FilePath.Contains("ConflictException"));
        conflictingTypeSyntaxTree.ShouldBeNull();

        // Assert - Compilation should fail because ConflictingType doesn't exist
        var compilationDiagnostics = compilation.GetDiagnostics();
        compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldNotBeEmpty();
        compilationDiagnostics.ShouldContain(d => d.Id == "CS0246" && d.GetMessage().Contains("ConflictingType"));
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

        // Assert - Generator diagnostics should be empty (generator ran successfully)
        diagnostics.ShouldBeEmpty();

        // Verify the generated exception has all primitive type properties
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("PrimitiveTypesException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public int? IntValue { get; init; }");
        generatedCode.ShouldContain("public long? LongValue { get; init; }");
        generatedCode.ShouldContain("public double? DoubleValue { get; init; }");
        generatedCode.ShouldContain("public decimal? DecimalValue { get; init; }");
        generatedCode.ShouldContain("public bool? BoolValue { get; init; }");
        generatedCode.ShouldContain("public char? CharValue { get; init; }");
        generatedCode.ShouldContain("public string? StringValue { get; init; }");
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception only has the primitive property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("NonPrimitiveException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public string? Name { get; init; }");
        // Should not contain any property for the non-primitive type
        generatedCode.ShouldNotContain("SomeClass");
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify no exception was generated for ArgumentException
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ArgumentException") && st.FilePath.Contains(".g.cs"));
        generatedSyntaxTree.ShouldBeNull();
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Verify the generated exception exists with property
        var generatedSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(st => st.FilePath.Contains("ThrowExprException"));
        generatedSyntaxTree.ShouldNotBeNull();

        string generatedCode = generatedSyntaxTree.GetText().ToString();
        generatedCode.ShouldContain("public sealed class ThrowExprException : Exception");
        generatedCode.ShouldContain("public string? Reason { get; init; }");
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
