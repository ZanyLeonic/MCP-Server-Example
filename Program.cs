using MCPServerDemo.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithStdioServerTransport()
    .WithTools<EchoTool>()
    .WithTools<JourneyPlanner>();

using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.tfl.gov.uk") };
httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("mcp-demo", "1.0"));
builder.Services.AddSingleton(httpClient);

var app = builder.Build();

app.MapMcp();

await app.RunAsync();

[McpServerToolType]
public class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public string Echo(string message) => $"hello {message}";

    [McpServerTool, Description("Returns the current date and time in ISO 8601 format.")]
    public string Date() => DateTime.Now.ToString("O");
}