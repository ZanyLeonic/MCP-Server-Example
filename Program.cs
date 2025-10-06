using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<EchoTool>();
await builder.Build().RunAsync();

[McpServerToolType]
public class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public string Echo(string message) => $"hello {message}";

    [McpServerTool, Description("Returns the current date and time in ISO 8601 format.")]
    public string Date() => DateTime.Now.ToString("O");
}