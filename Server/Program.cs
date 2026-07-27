using Microsoft.AspNetCore.Authentication.JwtBearer;
using ASP.NET_core_MemoAnchor_Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

const string UNITY_AUTH_ISSUER = "https://player-auth.services.api.unity.com";
const string UNITY_JWKS_URL = UNITY_AUTH_ISSUER + "/.well-known/jwks.json";

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration["Hosting:Urls"];
if (!string.IsNullOrWhiteSpace(urls))
{
    builder.WebHost.UseUrls(urls.Trim());
}

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<MemoAnchorDbContext>(options =>
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<IProfileStore, PostgresPlayerDataService>();
builder.Services.AddScoped<IMapMemoStore, PostgresMapMemoStore>();
builder.Services.Configure<ReconstructionOptions>(builder.Configuration.GetSection("Reconstruction"));
builder.Services.AddHttpClient();
builder.Services.AddHttpClient(ReconstructionOptions.HTTP_CLIENT_NAME, (serviceProvider, client) =>
{
    ReconstructionOptions reconstruction = serviceProvider.GetRequiredService<IOptions<ReconstructionOptions>>().Value;
    client.BaseAddress = new Uri(reconstruction.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(Math.Max(1, reconstruction.TimeoutMinutes));
});
builder.Services.Configure<AuthProviderOptions>(builder.Configuration.GetSection("AuthProviders"));
builder.Services.AddSingleton<OAuthStateStore>();
string unityProjectId = builder.Configuration["UnityAuthentication:ProjectId"] ?? "";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = UNITY_AUTH_ISSUER,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                var keys = UnityJwksResolver.GetSigningKeys(UNITY_JWKS_URL);
                return kid != null ? keys.Where(key => key.KeyId == kid) : keys;
            }
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                string? projectId = null;
                if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    projectId = identity.FindFirst("project_id")?.Value ?? identity.FindFirst("projectId")?.Value;
                }

                if (!string.IsNullOrWhiteSpace(projectId)
                    && !string.Equals(projectId.Trim(), unityProjectId, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("project_id does not match configured Unity project.");
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                logger.LogWarning(context.Exception, "JWT validation failed");
                context.Response.Headers.Append("X-Auth-Error", context.Exception.GetType().Name);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                logger.LogWarning("JWT challenge (401). AuthenticateResult.Failure: {Failure}", context.AuthenticateFailure?.Message);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string memoUploadDirectory = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads", "memos");
Directory.CreateDirectory(memoUploadDirectory);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

file static class UnityJwksResolver
{
    private static readonly object Lock = new();
    private static DateTime cachedAt;
    private static IList<SecurityKey>? cachedKeys;

    public static IList<SecurityKey> GetSigningKeys(string jwksUrl)
    {
        lock (Lock)
        {
            if (cachedKeys != null && DateTime.UtcNow - cachedAt < TimeSpan.FromHours(8))
            {
                return cachedKeys;
            }
        }

        using var http = new HttpClient();
        var json = http.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
        var jwks = new JsonWebKeySet(json);
        var keys = jwks.GetSigningKeys();

        lock (Lock)
        {
            cachedAt = DateTime.UtcNow;
            cachedKeys = keys;
        }

        return keys;
    }
}
