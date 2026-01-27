using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace Api_Vapp.Filters
{
    /// <summary>
    /// Operation Filter برای اضافه کردن Header زمان پاسخ به Swagger
    /// این Filter Header "X-Response-Time-Ms" را به تمام Response ها اضافه می‌کند
    /// که نشان می‌دهد API در چه مدت زمانی پاسخ داده است
    /// </summary>
    /// <remarks>
    /// این Filter به صورت خودکار به تمام endpoint ها اعمال می‌شود
    /// و Header زمان پاسخ را در Swagger UI نمایش می‌دهد.
    /// 
    /// **استفاده:**
    /// - برای Monitoring و Debugging
    /// - برای Performance Analysis
    /// - برای بهینه‌سازی API
    /// </remarks>
    public class SwaggerResponseTimeFilter : IOperationFilter
    {
        /// <summary>
        /// اضافه کردن Header زمان پاسخ به تمام Response ها
        /// </summary>
        /// <param name="op">Operation که باید تغییر کند</param>
        /// <param name="ctx">Context شامل اطلاعات متد و Controller</param>
        public void Apply(OpenApiOperation op, OperationFilterContext ctx)
        {
            // بررسی وجود Responses
            if (op.Responses == null)
                return;

            // اضافه کردن Header به تمام Response ها
            op.Responses.Values.ToList().ForEach(response =>
            {
                // ایجاد Dictionary برای Headers اگر وجود ندارد
                response.Headers ??= new Dictionary<string, OpenApiHeader>();
                
                // اضافه کردن Header زمان پاسخ
                response.Headers["X-Response-Time-Ms"] = new OpenApiHeader
                {
                    Description = "زمان پاسخ API به میلی‌ثانیه",
                    Schema = new OpenApiSchema 
                    { 
                        Type = "integer", 
                        Format = "int64",
                        Description = "زمان پاسخ به میلی‌ثانیه (مثلاً 150 به معنای 150 میلی‌ثانیه)"
                    },
                    Required = false
                };
            });
        }
    }
}

