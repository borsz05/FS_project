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
            builder.Services.AddScoped<ISchedulerService, SchedulerService>();
            var app = builder.Build();

            app.UseRouting();

            app.MapControllerRoute(
                name:"default",
                pattern: "{controller}/{action}/{id}"
                );


            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.Run();
        }
    }
}
