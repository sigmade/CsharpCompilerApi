using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace CompilerApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CodeCompilerController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CompileAndRun([FromBody] CodeRequest request)
    {
        try
        {
            // Компиляция кода
            var syntaxTree = CSharpSyntaxTree.ParseText(request.Code);

            var compilation = CSharpCompilation.Create(
                "DynamicCompilation",
                new[] { syntaxTree },
                new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
                },
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                // Если ошибки компиляции
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                return BadRequest(failures.Select(failure => failure.GetMessage()));
            }

            // Выполнение скомпилированного кода
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var entryPoint = assembly.EntryPoint;
            var outputWriter = new StringWriter();
            Console.SetOut(outputWriter);

            entryPoint.Invoke(null, null);

            var output = outputWriter.ToString();

            return Ok(output);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}

public class CodeRequest
{
    public string Code { get; set; }
}
