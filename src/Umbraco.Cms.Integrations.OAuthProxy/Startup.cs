using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Umbraco.Cms.Integrations.OAuthProxy.Configuration;

namespace Umbraco.Cms.Integrations.OAuthProxy
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
            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            services.AddHttpClient("HubspotToken", c =>
            {
                c.BaseAddress = new Uri("https://api.hubapi.com/");
            });
            services.AddHttpClient("HubspotFormsToken", c =>
            {
                c.BaseAddress = new Uri("https://api.hubapi.com/");
            });
            services.AddHttpClient("SemrushToken", c =>
            {
                c.BaseAddress = new Uri("https://oauth.semrush.com/");
            });
            services.AddHttpClient("ShopifyToken", c =>
            {
                c.BaseAddress = new Uri("https://shop-replace.myshopify.com/admin/");
            });

            services.AddRazorPages();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });
        }
    }
}