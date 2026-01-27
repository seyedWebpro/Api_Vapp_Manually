using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Linq;
using System.Text;
using Api_Vapp.Data;
using Api_Vapp.Utilities;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration را از appsettings.json بارگذاری کنید:
builder.Configuration.AddJsonFile("appsettings.json");

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
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            // استخراج تمام پیام‌های خطا از ModelState و تبدیل به فارسی
            var errors = context.ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .SelectMany(x => x.Value.Errors.Select(error => 
                {
                    var fieldName = x.Key;
                    var errorMessage = error.ErrorMessage;
                    
                    // اگر ErrorMessage خالی باشد، از ExceptionMessage استفاده می‌کنیم
                    if (string.IsNullOrWhiteSpace(errorMessage) && error.Exception != null)
                    {
                        errorMessage = error.Exception.Message;
                    }
                    
                    return ErrorTranslator.TranslateValidationError(errorMessage, fieldName);
                }))
                .ToList();

            // ساخت پاسخ استاندارد با استفاده از ApiResponse
            var response = Api_Vapp.DTOs.Common.ApiResponse<object>.BadRequest("خطای اعتبارسنجی اطلاعات ورودی", errors);

            // بازگرداندن نتیجه با کد 400
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
        // در Development و با DisableAuth = true، Policy همیشه موفق است
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true) // همیشه موفق
            .Build();
    }
    else
    {
        // در Production یا Development با DisableAuth = false، احراز هویت لازم است
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
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
builder.Services.AddScoped<Api_Vapp.Interfaces.ISpecialOccasionRepository, Api_Vapp.Repositories.SpecialOccasionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAutomatedMessageRepository, Api_Vapp.Repositories.AutomatedMessageRepository>();

// ثبت Repository های مالی و کیف پول
builder.Services.AddScoped<Api_Vapp.Interfaces.IWalletRepository, Api_Vapp.Repositories.WalletRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IPaymentRepository, Api_Vapp.Repositories.PaymentRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackRepository, Api_Vapp.Repositories.CashbackRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackTransactionRepository, Api_Vapp.Repositories.CashbackTransactionRepository>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackDraftRepository, Api_Vapp.Repositories.CashbackDraftRepository>();

// ثبت Repository تنظیمات اعلان‌ها
builder.Services.AddScoped<Api_Vapp.Interfaces.IUserNotificationSettingsRepository, Api_Vapp.Repositories.UserNotificationSettingsRepository>();

// ثبت سرویس‌های احراز هویت
builder.Services.AddScoped<Api_Vapp.Interfaces.IJwtService, Api_Vapp.Services.JwtService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISmsService, Api_Vapp.Services.SmsService>();
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

// ثبت سرویس‌های مدیریت پیام و اتوماسیون
builder.Services.AddScoped<Api_Vapp.Interfaces.IMessageService, Api_Vapp.Services.MessageService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IAutomatedMessageService, Api_Vapp.Services.AutomatedMessageService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ISpecialOccasionService, Api_Vapp.Services.SpecialOccasionService>();

// ثبت سرویس‌های مالی و کیف پول
builder.Services.AddScoped<Api_Vapp.Interfaces.IWalletService, Api_Vapp.Services.WalletService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.IPaymentService, Api_Vapp.Services.PaymentService>();
builder.Services.AddScoped<Api_Vapp.Interfaces.ICashbackService, Api_Vapp.Services.CashbackService>();

// ثبت سرویس تنظیمات اعلان‌ها
builder.Services.AddScoped<Api_Vapp.Interfaces.INotificationSettingsService, Api_Vapp.Services.NotificationSettingsService>();

// ثبت Background Services برای پیام‌های خودکار و زمان‌دار
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.AutomatedMessageBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledCampaignBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledMessageBackgroundService>();
builder.Services.AddHostedService<Api_Vapp.Services.BackgroundServices.ScheduledCashbackBackgroundService>();

// use in OTP 
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

#region File Upload Configuration
// تنظیمات آپلود فایل
builder.Services.Configure<Api_Vapp.DTOs.File.FileUploadOptions>(builder.Configuration.GetSection("FileUpload"));
builder.Services.AddScoped<Api_Vapp.Interfaces.IFileUploadService, Api_Vapp.Services.FileUploadService>();
#endregion

#region dbContext
builder.Services.AddDbContext<Api_Context>(options =>
{
    options.UseSqlServer(builder.Configuration["defultConnection"]);  //  localConnection  
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

//jwt
app.UseAuthentication();

//CORS policy
app.UseCors("myCors");

app.UseAuthorization();

app.UseStaticFiles(); // برای wwwroot

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();