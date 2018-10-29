using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Net.CrossCutting.RequestLogger.Setting;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net.CrossCutting.RequestLogger
{
    public class RequestLoggerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRequestLogSetting _requestLogSetting;
        private readonly Serilog.ILogger _serilogFileLogger;
        private readonly Serilog.ILogger _serilogSqlServerLogger;
        private readonly ILogger<RequestLoggerMiddleware> _Logger;

        public RequestLoggerMiddleware(RequestDelegate next, IRequestLogSetting requestLogSetting, ILogger<RequestLoggerMiddleware> logger)
        {
            if (Debugger.IsAttached)
            {
                Trace.WriteLine($"RequestLoggerMiddleware SQLServer {JsonConvert.SerializeObject(requestLogSetting)}");
            }
            _next = next;
            _requestLogSetting = requestLogSetting;
            _Logger = logger;
            switch (_requestLogSetting.Provider.ToLower())
            {
                case "file":
                    _serilogFileLogger = new Serilog.LoggerConfiguration()
                        .WriteTo
                        .File(_requestLogSetting.FilePath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message:lj}{NewLine}", rollingInterval: RollingInterval.Day)
                        .CreateLogger();
                    break;
                case "sqlserver":
                    var columnOptions = new ColumnOptions() { AdditionalDataColumns = new List<DataColumn>() };
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "UniqueId" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "DeviceId" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "Token" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "ReqObj" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "ReqBody" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "ResObj" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(string), ColumnName = "ResBody" });
                    columnOptions.AdditionalDataColumns.Add(new DataColumn { DataType = typeof(long), ColumnName = "Duration" });
                    _serilogSqlServerLogger = new Serilog.LoggerConfiguration()
                        .WriteTo
                        .MSSqlServer(_requestLogSetting.SqlServerConnectionString, _requestLogSetting.SqlServerTableName, columnOptions: columnOptions, autoCreateSqlTable: true)
                        .CreateLogger();
                    if (Debugger.IsAttached)
                    {
                        Trace.WriteLine($"RequestLoggerMiddleware SQLServer {_requestLogSetting.SqlServerConnectionString}");
                    }
                    break;
                default:
                    break;
            }
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (Debugger.IsAttached)
            {
                Trace.WriteLine($"RequestLoggerMiddleware {httpContext.Request.GetEncodedUrl()}");
            }
            try
            {
                if (_requestLogSetting.Status)
                {
                    var uniqueId = Guid.NewGuid();
                    var startTime = DateTime.Now;
                    var deviceId = "";
                    var token = "";
                    if (httpContext.Request.Headers.TryGetValue("deviceid", out StringValues v1))
                    {
                        deviceId = v1.FirstOrDefault();
                    }
                    if (httpContext.Request.Headers.TryGetValue("token", out StringValues v2))
                    {
                        token = v2.FirstOrDefault();
                    }

                    #region Request
                    var req = new
                    {
                        Method = httpContext.Request.Method,
                        Url = new
                        {
                            Schema = httpContext.Request.Scheme,
                            Host = httpContext.Request.Host.Host,
                            Port = httpContext.Request.Host.Port,
                            Path = httpContext.Request.Path.Value,
                            QueryString = httpContext.Request.QueryString.Value ?? "",
                            Full = httpContext.Request.GetEncodedUrl()
                        },
                        ContentLength = httpContext.Request.ContentLength,
                        ContentType = httpContext.Request.ContentType,
                        Cookies = (from c in httpContext.Request.Cookies
                                   select new
                                   {
                                       c.Key,
                                       c.Value
                                   }).AsEnumerable(),
                        Headers = (from h in httpContext.Request.Headers
                                   select new
                                   {
                                       h.Key,
                                       h.Value
                                   }).AsEnumerable(),
                        Protocol = httpContext.Request.Protocol,
                        Queries = (from q in httpContext.Request.Query
                                   select new
                                   {
                                       q.Key,
                                       q.Value
                                   }).AsEnumerable()
                    };
                    #endregion

                    #region Response
                    var res = new
                    {
                        ContentLength = httpContext.Response.ContentLength,
                        ContentType = httpContext.Response.ContentType,
                        //Cookies = httpContext.Response.Cookies,
                        StatusCode = httpContext.Response.StatusCode,
                        Headers = (from h in httpContext.Response.Headers
                                   select new
                                   {
                                       h.Key,
                                       h.Value
                                   }).ToList()
                    };
                    #endregion

                    var skipRequestBody = req.Url.Path.EndsWith("/users/uploadavatar") && req.Url.Path.StartsWith("/api/");
                    httpContext.Request.EnableRewind();
                    string reqBody = "";
                    string resBody = "";
                    var originReqBody = httpContext.Request.Body;
                    var originResBody = httpContext.Response.Body;

                    if (skipRequestBody)
                    {
                        reqBody = "Skiped";
                    }
                    else
                    {
                        using (var streamReader = new StreamReader(httpContext.Request.Body))
                        {
                            reqBody = await streamReader.ReadToEndAsync();
                            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(reqBody));
                        }
                    }

                    try
                    {
                        using (var responseBody = new MemoryStream())
                        {
                            httpContext.Response.Body = responseBody;

                            await _next(httpContext);

                            if (!skipRequestBody)
                            {
                                httpContext.Request.Body = originReqBody;
                            }

                            responseBody.Seek(0, SeekOrigin.Begin);
                            using (var streamReader = new StreamReader(responseBody))
                            {
                                resBody = streamReader.ReadToEnd();
                                httpContext.Response.Body = originResBody;
                                await httpContext.Response.WriteAsync(resBody);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        if (Debugger.IsAttached)
                        {
                            Trace.WriteLine($"RequestLoggerMiddleware exception 2 {ex2.Message}");
                        }
                        _Logger.LogWarning(ex2, ex2.Message);
                    }

                    var duration = (long)((DateTime.Now - startTime).TotalMilliseconds);
                    switch (_requestLogSetting.Provider.ToLower())
                    {
                        case "file":
                            _serilogFileLogger.Information("{id} ============ [request] ============ {deviceId}", uniqueId, deviceId);
                            _serilogFileLogger.Information("{id} {obj}", uniqueId, JsonConvert.SerializeObject(req));
                            _serilogFileLogger.Information("{id} Body: {obj}", uniqueId, reqBody);
                            _serilogFileLogger.Information("{id} ============ [response] ============ {duration}ms ============", uniqueId, duration);
                            _serilogFileLogger.Information("{id} {obj}", uniqueId, JsonConvert.SerializeObject(res));
                            _serilogFileLogger.Information("{id} Body: {obj}", uniqueId, resBody);
                            break;
                        case "sqlserver":
                            _serilogSqlServerLogger.Information("{UniqueId} {DeviceId} {Token} {ReqObj} {ReqBody} {ResObj} {ResBody} {Duration}", uniqueId, deviceId, token, JsonConvert.SerializeObject(req), reqBody, JsonConvert.SerializeObject(res), resBody, duration);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    await _next(httpContext);
                }

            }
            catch (System.Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Trace.WriteLine($"RequestLoggerMiddleware exception {ex.Message}");
                }
                _Logger.LogWarning(ex, ex.Message);
            }
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class RequestLoggerMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLoggerMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggerMiddleware>();
        }
    }
}
