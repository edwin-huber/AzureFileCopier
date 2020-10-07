using Microsoft.Extensions.Configuration;

namespace aafccore.util
{
    public static class CopierConfiguration
    {
        public static readonly IConfiguration Config = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile("localappsettings.json", optional: true, reloadOnChange: true)
                        .Build();

    }
}
