using Cryze.API.Python;
using Cryze.API.Services;
using Cryze.API.Store;

namespace Cryze.API
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
      // Bind the JsonStoreConfiguration to the configuration
      services.Configure<StoreConfiguration>(Configuration.GetSection(nameof(StoreConfiguration)));
      services.Configure<JsonStoreConfiguration>(Configuration.GetSection(nameof(JsonStoreConfiguration)));
      services.Configure<WyzeSdkServiceConfiguration>(Configuration.GetSection(nameof(WyzeSdkServiceConfiguration)));

      services.AddSingleton<ICameraStore, JsonCameraInfoService>(); // camera store, used to store camera information. Currently JSON backed
      services.AddSingleton<WyzeSdkService>(); // wyze sdk service, used to interact with wyze cameras, runs inside PythonServices
      services.AddSingleton<CameraStoreActivities>(); // database server-side activities, like loading wyze credentials
      services.AddSingleton<PythonServices>(); // provides common things like a logger and python engine configuration
      services.AddSingleton<PythonRuntimeTask>(); // Runs the python code in a separate thread
      services.AddSingleton<InMemoryCameraMessageStore>(); // Stores camera messages in memory. In the future, this will be replaced with a database
      services.AddControllers();
      services.AddControllersWithViews();
    }

    public void Configure(IApplicationBuilder app)
    {
      app.UseDeveloperExceptionPage();

      app.UseStaticFiles();

      app.UseRouting();

      app.UseAuthorization();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
        // Add your endpoints here
      });

      // Run the startup activities for the camera store
      var cameraStoreActivities = app.ApplicationServices.GetRequiredService<CameraStoreActivities>();
      // Fail loudly instead of hanging forever with :8080 never bound (seen 2026-07-19/07-23/10-15).
      // Throwing aborts host startup, so the container exits and its restart policy takes over.
      var timeout = TimeSpan.FromSeconds(Configuration.GetValue("StoreConfiguration:StartupTimeoutSeconds", 120));
      if (!cameraStoreActivities.RunStartupActivitiesAsync().Wait(timeout))
      {
        throw new TimeoutException($"Startup activities did not finish within {timeout.TotalSeconds}s (Python worker stuck?)");
      }
   }
  }
}