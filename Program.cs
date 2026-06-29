using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Api_Vapp.Configuration;
using Api_Vapp.Data;
using Api_Vapp.Filters;
using Api_Vapp.Utilities;
using Serilog;
using Serilog.Events;

// Configure Serilog before creating the builder
var logPath = Path.Combine(Directory.GetCurrentDirectory(), "log");
if (!Directory.Exists(logPath))
{
    Directory.CreateDirectory(logPath);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Filter.ByExcluding(logEvent => logEvent.MessageTemplate.Text == "در حال پردازش درخواست")
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logPath, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        encoding: Encoding.UTF8
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
    || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Docker"
    || File.Exists("/.dockerenv");

if (builder.Environment.IsDevelopment() && !isDocker)
{
    if (OperatingSystem.IsWindows())
    {
        builder.Configuration.AddJsonFile("appsettings.Development.Windows.json", optional: false, reloadOnChange: true);
        Log.Information("Vapp Windows dev — appsettings.Development.Windows.json (Trusted_Connection, Server=.)");
    }
    else
    {
        builder.Configuration.AddJsonFile("appsettings.Development.Mac.json", optional: false, reloadOnChange: true);
        Log.Information("Vapp non-Windows dev — appsettings.Development.Mac.json (LocalDocker → localhost:1436).");
    }

    builder.Configuration.AddJsonFile("appsettings.Development.Local.json", optional: true, reloadOnChange: true);
}

if (isDocker && File.Exists("appsettings.Docker.json"))
{
    builder.Configuration.AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: true);
    Log.Information("Vapp Docker environment detected — loading appsettings.Docker.json");
}
else if (!isDocker)
{
    Log.Information("Vapp development/local environment — using appsettings.Development.json");
}

builder.Configuration.AddEnvironmentVariables();

#region File Upload Configuration for Large Files
// تنظیمات برای آپلود فایل‌های حجیم
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2147483648; // 2GB
    options.ValueLengthLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 2147483648; // 2GB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 2147483648; // 2GB
});
#endregion

#region policy‍
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "myCors", policy =>
    {
        //policy.WithOrigins("http://example.com","http://www.contoso.com");
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
#endregion policy

// HttpClient
builder.Services.AddHttpClient();



// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiTraceIdResultFilter>();
})
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = ErrorTranslator.ExtractModelStateErrors(context.ModelState);
            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            var message = ValidationResponseMessageResolver.Resolve(context.ModelState, path, errors);
            var response = Api_Vapp.DTOs.Common.ApiResponse<object>.BadRequest(
                message,
                errors,
                Api_Vapp.DTOs.Common.ErrorCodes.ValidationFailed);
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

#region swagger 
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "باشگاه مشتریان Vapp API", 
        Version = "v1",
        Description = "API جامع سیستم مدیریت باشگاه مشتریان با قابلیت‌های پیام‌رسانی، کیف پول دیجیتال و اتوماسیون بازاریابی",
        Contact = new OpenApiContact
        {
            Name = "تیم پشتیبانی Vapp",
            Email = "support@vapp.ir"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License"
        }
    });

    // خواندن XML Documentation Comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // Grouping Controllers
    options.TagActionsBy(api =>
    {
        var controllerName = api.ActionDescriptor.RouteValues["controller"];
        return new[] { controllerName ?? "Default" };
    });

    // Custom Operation IDs
    // options.CustomOperationIds(apiDesc =>
    // {
    //     return apiDesc.TryGetMethodInfo(out var methodInfo) ? methodInfo.Name : null;
    // });

    // Enable Annotations
    // options.EnableAnnotations(); // نیاز به پکیج اضافی دارد

    #region JWT Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.\n\nبرای استفاده از این API، ابتدا از endpoint /api/Auth/login توکن دریافت کنید و سپس آن را در header Authorization با فرمت 'Bearer {token}' ارسال کنید."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme 
            {
                Reference = new OpenApiReference 
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    #endregion

    // Schema Filters برای بهتر نمایش دادن Response ها
    options.SchemaFilter<Api_Vapp.Filters.SwaggerSchemaFilter>();

    // Operation Filter برای اضافه کردن Header زمان پاسخ به Swagger
    options.OperationFilter<Api_Vapp.Filters.SwaggerResponseTimeFilter>();
    
    // Operation Filter برای اضافه کردن Examples به Swagger
    options.OperationFilter<Api_Vapp.Examples.SwaggerExamplesFilter>();
});
#endregion

#region JWT
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters long. Please set a secure secret key in appsettings.json or environment variables.");
}

builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };

    // بررسی Blacklist برای توکن‌های لغو شده
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenBlacklistService = context.HttpContext.RequestServices
                .GetRequiredService<Api_Vapp.Interfaces.ITokenBlacklistService>();
            
            var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            
            if (!string.IsNullOrEmpty(jti))
            {
                var isBlacklisted = await tokenBlacklistService.IsTokenBlacklistedAsync(jti);
                if (isBlacklisted)
                {
                    context.Fail("توکن لغو شده است. لطفاً دوباره وارد شوید.");
                    return;
                }
            }

            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                var userRepository = context.HttpContext.RequestServices
                    .GetRequiredService<Api_Vapp.Interfaces.IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId);

                if (user == null || user.IsDeleted)
                {
                    context.Fail(ControlledErrorHelper.InvalidToken);
                    return;
                }

                if (!user.IsActive)
                {
                    context.HttpContext.Items["InactiveUser"] = true;
                    context.Fail(ControlledErrorHelper.InactiveUserAccount);
                }
            }
        }
    };
});
#endregion

#region Authorization Policy (برای غیرفعال کردن Auth در Development از appsettings.json)
builder.Services.AddAuthorization(options =>
{
    // بررسی تنظیمات برای غیرفعال کردن Auth در Development
    var disableAuth = builder.Configuration.GetValue<bool>("Development:DisableAuth", false);
    var isDevelopment = builder.Environment.IsDevelopment();

    if (isDevelopment && disableAuth)
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();

        options.AddPolicy("AdminOnly", policy =>
            policy.RequireAssertion(_ => true));
    }
    else
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin"));
    }
});
#endregion

#region httpcontext
builder.Services.AddHttpContextAccessor();
#endregion


// ثبت Repository ها
builder.Services.AddScoped(typeof(Api_Vapp._Utilities.IBaseRepository<>), typeof(Api_Vapp._Utilities.BaseRepository<>));
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserRepository, Api_Vapp.Repositories.UserRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IRoleRepository, Api_Vapp.Repositories.RoleRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserRoleRepository, Api_Vapp.Repositories.UserRoleRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IContactNotebookRepository, Api_Vapp.Repositories.ContactNotebookRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IContactRepository, Api_Vapp.Repositories.ContactRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageRepository, Api_Vapp.Repositories.MessageRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageCampaignRepository, Api_Vapp.Repositories.MessageCampaignRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageTemplateRepository, Api_Vapp.Repositories.MessageTemplateRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageSessionRepository, Api_Vapp.Repositories.MessageSessionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IQuickActionRepository, Api_Vapp.Repositories.QuickActionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserFormRepository, Api_Vapp.Repositories.UserFormRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ILuckyWheelRepository, Api_Vapp.Repositories.LuckyWheelRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISpecialOccasionRepository, Api_Vapp.Repositories.SpecialOccasionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAutomatedMessageRepository, Api_Vapp.Repositories.AutomatedMessageRepository>();

// ثبت Repository های مالی و کیف پول
builder.Services.AddScoped<Api_Vapp.Interfaces.IWalletRepository, Api_Vapp.Repositories.WalletRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IPaymentRepository, Api_Vapp.Repositories.PaymentRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackRepository, Api_Vapp.Repositories.CashbackRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackTransactionRepository, Api_Vapp.Repositories.CashbackTransactionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackDraftRepository, Api_Vapp.Repositories.CashbackDraftRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IReferralProgramRepository, Api_Vapp.Repositories.ReferralProgramRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IReferralProgramDraftRepository, Api_Vapp.Repositories.ReferralProgramDraftRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IReferralUsageRepository, Api_Vapp.Repositories.ReferralUsageRepository>();

// ثبت Repository تنظیمات اعلان‌ها
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserNotificationSettingsRepository, Api_Vapp.Repositories.UserNotificationSettingsRepository>();

// ثبت سرویس‌های احراز هویت
builder.Services.AddScoped<Api_Vapp.Interfaces.IJwtService, Api_Vapp.Services.JwtService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISmsService, Api_Vapp.Services.SmsService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISmsDeliveryRecordRepository, Api_Vapp.Repositories.SmsDeliveryRecordRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISmsDeliveryTrackingService, Api_Vapp.Services.SmsDeliveryTrackingService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IRefreshTokenService, Api_Vapp.Services.RefreshTokenService>();
builder.Services.AddSingleton<Api_Vapp.Interfaces.ITokenBlacklistService, Api_Vapp.Services.TokenBlacklistService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAuthService, Api_Vapp.Services.AuthService>();

// ثبت سرویس مدیریت کاربران
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserService, Api_Vapp.Services.UserService>();

// ثبت سرویس مدیریت نقش‌ها
builder.Services.AddScoped<Api_Vapp.Interfaces.IRoleService, Api_Vapp.Services.RoleService>();

// ثبت سرویس مدیریت روابط کاربر-نقش
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserRoleService, Api_Vapp.Services.UserRoleService>();

// ثبت سرویس‌های مدیریت مخاطبین
builder.Services.AddScoped<Api_Vapp.Interfaces.IContactNotebookService, Api_Vapp.Services.ContactNotebookService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IContactService, Api_Vapp.Services.ContactService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IQuickActionService, Api_Vapp.Services.QuickActionService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserFormService, Api_Vapp.Services.UserFormService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ILuckyWheelService, Api_Vapp.Services.LuckyWheelService>();

// ثبت سرویس‌های مدیریت پیام و اتوماسیون
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageService, Api_Vapp.Services.MessageService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAutomatedMessageService, Api_Vapp.Services.AutomatedMessageService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISpecialOccasionService, Api_Vapp.Services.SpecialOccasionService>();

// ثبت سرویس‌های مالی و کیف پول
builder.Services.AddScoped<Api_Vapp.Interfaces.IWalletService, Api_Vapp.Services.WalletService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IPaymentService, Api_Vapp.Services.PaymentService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackService, Api_Vapp.Services.CashbackService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IReferralProgramService, Api_Vapp.Services.ReferralProgramService>();

// ثبت سرویس تنظیمات اعلان‌ها
builder.Services.AddScoped<Api_Vapp.Interfaces.INotificationSettingsService, Api_Vapp.Services.NotificationSettingsService>();

// ثبت سرویس‌های پنل ادمین
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminSubscriptionPlanService, Api_Vapp.Services.Admin.AdminSubscriptionPlanService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminSubscriptionFeatureService, Api_Vapp.Services.Admin.AdminSubscriptionFeatureService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminUserSubscriptionService, Api_Vapp.Services.Admin.AdminUserSubscriptionService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminSupportTicketService, Api_Vapp.Services.Admin.AdminSupportTicketService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserSupportTicketService, Api_Vapp.Services.Admin.UserSupportTicketService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminEducationalVideoService, Api_Vapp.Services.Admin.AdminEducationalVideoService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminMessageApprovalService, Api_Vapp.Services.Admin.AdminMessageApprovalService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminTemplateApprovalService, Api_Vapp.Services.Admin.AdminTemplateApprovalService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAdminDashboardService, Api_Vapp.Services.Admin.AdminDashboardService>();

// ثبت Background Services برای پیام‌های خودکار و زمان‌دار
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.AutomatedMessageBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledCampaignBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledMessageBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledCashbackBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.SmsDeliverySyncBackgroundService>();

// use in OTP 
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

#region File Upload Configuration
// تنظیمات آپلود فایل
builder.Services.Configure<Api_Vapp.DTOs.File.FileUploadOptions>(builder.Configuration.GetSection("FileUpload"));
builder.Services.Configure<Api_Vapp.Utilities.FormBuilderOptions>(builder.Configuration.GetSection(Api_Vapp.Utilities.FormBuilderOptions.SectionName));
builder.Services.Configure<Api_Vapp.Utilities.LuckyWheelOptions>(builder.Configuration.GetSection(Api_Vapp.Utilities.LuckyWheelOptions.SectionName));
builder.Services.AddScoped<Api_Vapp.Interfaces.IFileUploadService, Api_Vapp.Services.FileUploadService>();
#endregion

#region dbContext
var connectionString = SqlServerConnectionConfiguration.GetConnectionString(builder.Configuration);
builder.Services.AddDbContext<Api_Context>(options =>
{
    options.UseSqlServer(connectionString);
});
#endregion

#region Cache Configuration
// تنظیمات کش
//builder.Services.Configure<Api_Vapp.DTOs.Common.CacheSettings>(builder.Configuration.GetSection("Cache"));
//var cacheSettings = builder.Configuration.GetSection("Cache").Get<Api_Vapp.DTOs.Common.CacheSettings>();

// Register IMemoryCache
builder.Services.AddMemoryCache(options =>
{
    // تنظیم SizeLimit از appsettings.json
    var sizeLimit = builder.Configuration.GetValue<int?>("Cache:MemoryCache:SizeLimit");
    if (sizeLimit.HasValue && sizeLimit.Value > 0)
    {
        options.SizeLimit = sizeLimit.Value;
    }
});

// Register Redis (اگر Provider = Redis باشد)
//if (cacheSettings?.Provider == "Redis" && !string.IsNullOrEmpty(cacheSettings.Redis?.ConnectionString))
//{
//    builder.Services.AddStackExchangeRedisCache(options =>
//    {
//        options.Configuration = cacheSettings.Redis.ConnectionString;
//        options.InstanceName = cacheSettings.Redis.InstanceName;
//    });
//}

// Register Cache Services
//builder.Services.AddScoped<Api_Vapp.Interfaces.ICacheService, Api_Vapp.Services.CacheService>();
//builder.Services.AddScoped<Api_Vapp.Interfaces.ICacheInvalidationService, Api_Vapp.Services.CacheInvalidationService>();
#endregion

#region Rate Limit Configuration
// تنظیمات Rate Limiting برای جلوگیری از حملات DDoS
//builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimit"));
//var rateLimitSettings = builder.Configuration.GetSection("RateLimit").Get<RateLimitSettings>();

// Register Redis برای Rate Limiting (اگر Provider = Redis باشد)
//if (rateLimitSettings?.Provider == "Redis" && !string.IsNullOrEmpty(rateLimitSettings.Redis?.ConnectionString))
//{
//    // اگر Redis برای Cache هم استفاده می‌شود، از همان connection استفاده می‌کنیم
//    if (cacheSettings?.Provider != "Redis")
//    {
//        builder.Services.AddStackExchangeRedisCache(options =>
//        {
//            options.Configuration = rateLimitSettings.Redis.ConnectionString;
//            options.InstanceName = rateLimitSettings.Redis.InstanceName ?? "VappShop:";
//        });
//    }
//}

// Register Rate Limit Service
//builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
#endregion



var app = builder.Build();

if (app.Environment.IsProduction() || app.Environment.EnvironmentName == "Docker" || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<Api_Context>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Checking database connection...");
        logger.LogInformation("Database: {DatabaseName}", context.Database.GetDbConnection().Database);

        try
        {
            var csb = new SqlConnectionStringBuilder(context.Database.GetDbConnection().ConnectionString);
            logger.LogInformation(
                "SQL endpoint: {DataSource}; User: {User}; DatabaseProvider: {Provider}",
                csb.DataSource,
                csb.UserID ?? "(integrated)",
                builder.Configuration["DatabaseProvider"] ?? "(legacy)");
        }
        catch
        {
            /* ignore parse errors */
        }

        var pendingMigrations = context.Database.GetPendingMigrations().ToList();
        logger.LogInformation("Pending migrations: {PendingCount}", pendingMigrations.Count);
        context.Database.Migrate();
        logger.LogInformation("Migration completed successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    c.DocExpansion(DocExpansion.None);
});

app.UseHttpsRedirection();

app.UseRouting();

// Response Time Middleware (برای محاسبه زمان پاسخ) - Inline
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    context.Response.OnStarting(() =>
    {
        stopwatch.Stop();
        context.Response.Headers.Append("X-Response-Time-Ms", stopwatch.ElapsedMilliseconds.ToString());
        return Task.CompletedTask;
    });
    await next();
});

// Global Exception Handler (باید قبل از همه Middleware ها باشد)
app.UseMiddleware<Api_Vapp.Middleware.GlobalExceptionHandlerMiddleware>();

// Rate Limiting Middleware (قبل از Authentication برای جلوگیری از حملات DDoS)
//app.UseMiddleware<RateLimitMiddleware>();

// CORS must run before auth middleware that short-circuits with 401/403,
// otherwise browsers block the response and the SPA sees a network error.
app.UseCors("myCors");

//jwt
app.UseAuthentication();

app.UseMiddleware<Api_Vapp.Middleware.BearerAuthenticationEnforcementMiddleware>();

app.UseAuthorization();

app.UseStaticFiles(); // برای wwwroot

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));

app.MapFallbackToFile("index.html");

if (app.Environment.IsDevelopment() && !isDocker)
    DevBrowserLauncher.Register(app);

try
{
    Log.Information("Application starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}