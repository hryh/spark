﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Spark.Engine;
using Spark.Engine.Extensions;
using Spark.Mongo.Extensions;
using Spark.Web.Data;
using Spark.Web.Models;
using Spark.Web.Models.Config;
using Spark.Web.Services;
using Spark.Web.Hubs;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Spark.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Bind to Spark and store settings from appSettings.json
            SparkSettings sparkSettings = new SparkSettings();
            Configuration.Bind("SparkSettings", sparkSettings);
            services.AddSingleton<SparkSettings>(sparkSettings);

            StoreSettings storeSettings = new StoreSettings();
            Configuration.Bind("StoreSettings", storeSettings);

            // Read examples settings from config
            ExamplesSettings examplesSettings = new ExamplesSettings();
            Configuration.Bind("ExamplesSettings", examplesSettings);
            services.Configure<ExamplesSettings>(options => Configuration.GetSection("ExamplesSettings").Bind(options));
            services.AddSingleton<ExamplesSettings>(examplesSettings);

            // Configure cookie policy
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            // Add database context for user administration
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection"))
            );

            // Add Identity management
            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddRoles<IdentityRole>()
                .AddDefaultUI()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddAuthorization();

            // Set up a default policy for CORS that accepts any origin, method and header.
            // only for test purposes.
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin();
                    policy.AllowAnyMethod();
                    policy.AllowAnyHeader();
                }));

            // Sets up the MongoDB store
            services.AddMongoFhirStore(storeSettings);

            // AddFhir also calls AddMvcCore
            services.AddFhir(sparkSettings);

            services.AddTransient<ServerMetadata>();

            // AddMvc needs to be called since we are using a Home page that is reliant on the full MVC framework
            services.AddMvc(options =>
            {
                options.InputFormatters.RemoveType<JsonPatchInputFormatter>();
                options.InputFormatters.RemoveType<JsonInputFormatter>();
                options.OutputFormatters.RemoveType<JsonOutputFormatter>();

            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Spark API", Version = "v1" });
            });

            services.AddSignalR();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Spark API");
            });

            app.UseAuthentication();
            app.UseCors();

            app.UseSignalR(routes =>
            {
                routes.MapHub<MaintenanceHub>("/maintenanceHub");
            });

            // UseFhir also calls UseMvc
            app.UseFhir(r => r.MapRoute(name: "default", template: "{controller}/{action}/{id?}", defaults: new { controller = "Home", action = "Index" }));
        }
    }
}