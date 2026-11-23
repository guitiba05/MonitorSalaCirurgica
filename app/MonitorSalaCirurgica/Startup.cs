using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Infra;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MonitorSalaCirurgica
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // aceita "28,5" e "28.5" nos campos numéricos
            services.AddControllersWithViews(o => o.ModelBinderProviders.Insert(0, new NumeroModelBinderProvider()));

            services.AddSession(options =>
            {
                options.Cookie.IsEssential = true;
                options.IdleTimeout = TimeSpan.FromMinutes(120);
            });

            // ---- configurações lidas do appsettings.json ----
            // Banco SQL (ConexaoBD, como no CadAluno)
            ConexaoBD.StringConexao = Configuration.GetConnectionString("BancoSQL");

            // FIWARE: endereço da EC2, portas, fiware-service e fiware-servicepath
            var configFiware = new ConfigFiware();
            Configuration.GetSection("Fiware").Bind(configFiware);
            HelperFiware.Configura(configFiware);

            // Supervisão automática e Alexa
            Configuration.GetSection("Supervisao").Bind(ConfigSupervisao.Atual);
            Configuration.GetSection("Alexa").Bind(ConfigAlexa.Atual);

            // Supervisão automática (critério H5): roda em segundo plano enquanto a aplicação estiver no ar
            services.AddHostedService<SupervisorService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ConfiguracaoArquivo.PastaDoProjeto = env.ContentRootPath;

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/Home/Error");

            // Usa sempre o formato brasileiro (vírgula decimal, datas dd/MM/yyyy)
            var cultura = new CultureInfo("pt-BR");
            CultureInfo.DefaultThreadCurrentCulture = cultura;
            CultureInfo.DefaultThreadCurrentUICulture = cultura;
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(cultura),
                SupportedCultures = new List<CultureInfo> { cultura },
                SupportedUICultures = new List<CultureInfo> { cultura }
            });

            // Pelo túnel do ngrok (Host diferente de localhost) só o endpoint da skill da Alexa responde:
            // o resto da aplicação fica acessível apenas no próprio computador.
            app.Use(async (contexto, proximo) =>
            {
                string host = contexto.Request.Host.Host;
                bool local = host == "localhost" || host == "127.0.0.1" || host == "::1" || host == "[::1]";
                if (!local && ConfigAlexa.Atual.SomenteSkillPeloTunel &&
                    !contexto.Request.Path.StartsWithSegments("/api/alexa"))
                {
                    contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await proximo();
            });

            app.UseStaticFiles();
            app.UseSession();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
