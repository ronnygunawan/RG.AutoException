using RG.AutoException.Internals;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Text;

namespace RG.AutoException
{
    [Generator]
    public class ExceptionGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }
#endif
            context.RegisterForSyntaxNotifications(() => new ThrowSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not ThrowSyntaxReceiver receiver)
            {
                return;
            }

            foreach (string exceptionName in receiver.MissingExceptions)
            {
                context.AddSource(
                    hintName: exceptionName,
                    sourceText: SourceText.From(
                        $$"""
                        using System;

                        namespace GeneratedExceptions
                        {
                            public sealed class {{exceptionName}} : Exception { }
                        }
                        """,
                        encoding: Encoding.UTF8
                    ));
            }
        }
    }
}
