using FinalProjectApi.Data;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectApi.Extensions
{
    public static class DatabaseExtension
    {
        public static void AddApplicationDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            });
        }
    }
}
