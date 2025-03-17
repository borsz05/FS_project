using TaskManager.Services;

namespace TaskManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllersWithViews();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<ISchedulerService, SchedulerService>();

            // CORS policy beállítása
            var corsPolicy = "AllowSpecificOrigins";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: corsPolicy,
                    policy =>
                    {
                        policy.WithOrigins("http://127.0.0.1:5500")
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    });
            });

            var app = builder.Build();

            app.UseRouting();

            app.UseCors(corsPolicy);

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}"
            );
            //if (app.Environment.IsDevelopment())
            //{
            //    app.UseSwagger();
            //    app.UseSwaggerUI();
            //}

            app.Run();
        }
    }
}
