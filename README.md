# Net.CrossCutting.RequestLogger

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

Startup

```c#
public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            requestLogSetting = Configuration.GetSection("RequestLog").Get<RequestLogSetting>();
        }
```

ConfigureServices:

```c#
public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRequestLogSetting>(requestLogSetting);

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }
```

Configure:

```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), appBuilder =>
            {
                appBuilder.UseRequestLoggerMiddleware();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
```
