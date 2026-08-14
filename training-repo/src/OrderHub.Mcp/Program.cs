using Microsoft.AspNetCore.Builder;   // console 專案沒有 Web SDK 的 implicit usings,WebApplication 與 MapMcp 都靠這行
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;
using OrderHub.Infrastructure.Repositories;

if (args.Contains("--http"))
{
    // HTTP 版:給 n8n 等遠端 client 用(n8n 的 MCP 節點只支援 SSE / streamable HTTP,不支援 stdio)。
    // streamable HTTP 端點在 http://localhost:3001/(MapMcp 預設掛在根路徑)
    var builder = WebApplication.CreateBuilder(args);
    // HTTP 版的協定通道是 socket 不是 stdout,所以照 WebApplication 預設把 log 印到 stdout 沒問題
    // (不要把 stdio 分支那行 LogToStandardErrorThreshold 複製過來——這裡不需要)
    AddOrderHubServices(builder.Services, builder.Configuration);
    builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithTools<OrderHubTools>()
        .WithResources<OrderHubResources>()
        .WithPrompts<OrderHubPrompts>();

    var app = builder.Build();
    app.MapMcp();
    app.Run("http://localhost:3001"); // port 若被占用會擲例外(不會自動改 port),請先釋放 3001 或改這行的 port
}
else
{
    // stdio 版:活動 2 的原樣,一行都沒改。stdout 是協定通道,log 一律走 stderr
    var builder = Host.CreateApplicationBuilder(args);

    // 重要:stdout 是 MCP 的協定通道,所有 log 一律走 stderr
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    AddOrderHubServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<OrderHubTools>()
        .WithResources<OrderHubResources>()
        .WithPrompts<OrderHubPrompts>();

    await builder.Build().RunAsync();
}

// 兩個 transport 共用同一套分層接線:工具走 service / repository,不直接摸 DbContext(與 OrderHub.Web 一致)
static void AddOrderHubServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<OrderHubDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("Default")
            ?? "Server=localhost;Database=OrderHubTraining;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"));

    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IOrderService, OrderService>();
}
