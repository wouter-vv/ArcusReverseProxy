using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Arcus.WebApi.Correlation;
using ProxyKit;

namespace Arcus.Demo.WebAPI
{
    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the configuration of key/value application properties.
        /// </summary>
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {


            services.AddControllers(options =>
            {
                options.ReturnHttpNotAcceptable = true;
                options.RespectBrowserAcceptHeader = true;

                RestrictToJsonContentType(options);
                AddEnumAsStringRepresentation(options);

            });


            services.AddHealthChecks();
            services.AddCorrelation();

            services.AddProxy();

#if DEBUG
            var openApiInformation = new OpenApiInfo
            {
                Title = "Arcus.Demo.WebAPI",
                Version = "v1"
            };

            services.AddSwaggerGen(swaggerGenerationOptions =>
            {
                swaggerGenerationOptions.SwaggerDoc("v1", openApiInformation);
                swaggerGenerationOptions.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Arcus.Demo.WebAPI.Open-Api.xml"));
            });
#endif
        }

        private static void RestrictToJsonContentType(MvcOptions options)
        {
            var allButJsonInputFormatters = options.InputFormatters.Where(formatter => !(formatter is SystemTextJsonInputFormatter));
            foreach (IInputFormatter inputFormatter in allButJsonInputFormatters)
            {
                options.InputFormatters.Remove(inputFormatter);
            }

            // Removing for text/plain, see https://docs.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting?view=aspnetcore-3.0#special-case-formatters
            options.OutputFormatters.RemoveType<StringOutputFormatter>();
        }

        private static void AddEnumAsStringRepresentation(MvcOptions options)
        {
            var onlyJsonOutputFormatters = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>();
            foreach (SystemTextJsonOutputFormatter outputFormatter in onlyJsonOutputFormatters)
            {
                outputFormatter.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<Arcus.WebApi.Logging.ExceptionHandlingMiddleware>();
            app.UseCorrelation();
            app.UseRouting();

            //var roundRobin = new RoundRobin
            //{
            //    new UpstreamHost("https://codit-mock-server.azure-api.net/ord", weight: 1),
            //    new UpstreamHost("https://codit-mock-server.azure-api.net/cat", weight: 2),
            //    new UpstreamHost("https://codit-mock-server.azure-api.net/prod", weight: 3)
            //};


#warning Please configure application with HTTPS transport layer security

#warning Please configure application with authentication mechanism: https://webapi.arcus-azure.net/features/security/auth/shared-access-key

#if DEBUG
            app.UseSwagger(swaggerOptions =>
            {
                swaggerOptions.RouteTemplate = "api/{documentName}/docs.json";
            });
            app.UseSwaggerUI(swaggerUiOptions =>
            {
                swaggerUiOptions.SwaggerEndpoint("/api/v1/docs.json", "Arcus.Demo.WebAPI");
                swaggerUiOptions.RoutePrefix = "api/docs";
                swaggerUiOptions.DocumentTitle = "Arcus.Demo.WebAPI";
            });
#endif

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/api/orders/{*url}", async x =>
                {
                    // check for everything after orders
                    app.RunProxy(async context =>
                    {
                        var host = new UpstreamHost("https://codit-mock-server.azure-api.net/ord");

                        var response = await context
                            .ForwardTo(host)
                            .Send();
                        return response;
                    });
                });
                endpoints.MapGet("/api/catalog/{*url}", async x =>
                {
                    app.RunProxy(async context =>
                    {
                        var host = new UpstreamHost("https://codit-mock-server.azure-api.net/cat");

                        return await context
                            .ForwardTo(host)
                            .Send();
                    });
                });
                endpoints.MapGet("/api/products/{*url}", async x =>
                {
                    app.RunProxy(async context =>
                    {
                        var host = new UpstreamHost("https://codit-mock-server.azure-api.net/prod");

                        return await context
                            .ForwardTo(host)
                            .Send();
                    });

                }); 
                endpoints.MapControllerRoute(
                     name: "Health_Get",
                     pattern: "api/v1/health"
                 );
                endpoints.MapControllerRoute(
                     name: "Health_Get_Startup",
                     pattern: "/"
                 );

            });

            //app.UseEndpoints(endpoints => endpoints.MapControllers());

        }
    }
}
