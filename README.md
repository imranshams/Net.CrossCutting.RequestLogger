# Net.CrossCutting.RequestLogger

Handle Cross-Cutting conserns via the power of Asp.net-core middelwares.  

**RequestLogger** make it possible to log each http request and it related response to multiple data source (DB or file for example).

At the beginning you need to config **RequestLogger**, this is requierd because we need to fetch some dynamic settings at start point. To achieve that add a new json properties named **RequestLog** to your  **appseting.josn** file.

```json
{
  "RequestLog": {
    "Status": true,
    //file,sqlserver
    "Provider": "sqlserver",
    "FilePath": "logs/requests/reqlog-.txt",
    "SqlServerConnectionString": "Data Source=SQL-SRV\\SQL2017;Initial Catalog=Logs;User ID=sa;Password=admin",
    "SqlServerTableName": "RequestLog"
  }
}
```

Then edit your *startup* and *ConfigureServices* methods (composition root of your Dependency Injection **Startup.cs** file by default) as below.  

```c#
private RequestLogSetting requestLogSetting;

public Startup(IConfiguration configuration)
{
    //...
    requestLogSetting = Configuration.GetSection("RequestLog").Get<RequestLogSetting>();
    //...
}

public void ConfigureServices(IServiceCollection services)
{
    //...
    services.AddSingleton<IRequestLogSetting>(requestLogSetting);
    //...
}
```

Finally edit your *Configure* method. This tell .net core runtime to inject **RequestLogger** middelware to its pipeline. 

```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    //Sample 1: Log all recieved requests
    appBuilder.UseRequestLoggerMiddleware();    
    //...
}
}
```

Also you can filter the routing of middelware as you want like the following example. In this example the middelware logs all recieved requests when incomming http requests started with '/api'.
```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), appBuilder =>
    {
        appBuilder.UseRequestLoggerMiddleware();
    });
    //...
}
```
