using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;



var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

List<string>? allowedTenantIds = config.GetValue<string>("ALLOWED_TENANT_IDS")?.Split(',').ToList() ?? throw new ArgumentNullException(nameof(allowedTenantIds), "ALLOWED_TENANT_IDS must be set in the environment variables");
List<string>? validApplicationIds = config.GetValue<string>("ALLOWED_APPLICATION_IDS")?.Split(',').ToList() ?? throw new ArgumentNullException(nameof(validApplicationIds), "ALLOWED_APPLICATION_IDS must be set in the environment variables");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseFunctionsAuthorization();
    })
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddFunctionsAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtFunctionsBearer(options =>
            {
                var validIssuers = allowedTenantIds.Select(tenantId => $"https://sts.windows.net/{tenantId}/");
                options.Authority = "https://sts.windows.net/common";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuers = validIssuers,
                    ValidateAudience = false,
                };
            });
        services.AddFunctionsAuthorization(options =>
            {
                var defaultPolicy = new AuthorizationPolicyBuilder();
                defaultPolicy.RequireAssertion(context =>
                {
                    foreach (string appId in validApplicationIds)
                    {
                        if (context.User.HasClaim("appid", appId))
                        {
                            return true;
                        }
                    }
                    return false;
                });
                options.DefaultPolicy = defaultPolicy.Build();
            });
    })
    .Build();

host.Run();
