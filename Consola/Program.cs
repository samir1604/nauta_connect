using ConnectionManager.Configuration;
using Microsoft.Extensions.Hosting;
using Nauta.Cli.ConfigureParser;
using NautaCredential.Configuration;
using NautaManager.Configuration;
using System.Net;

var builder = Host.CreateApplicationBuilder();

var cookieContainer = new CookieContainer();

builder.Services
    .AddHttpClientConfig(cookieContainer)
    .AddCredentialConfig()
    .AddNautaManagerConfig();

using IHost host = builder.Build();

await ConfigureCommandLineParser.Configure(host, args);
