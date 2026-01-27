using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;

namespace Api_Vapp.Filters
{
    /// <summary>
    /// Schema Filter برای بهتر نمایش دادن DTO ها در Swagger
    /// این Filter توضیحات فیلدها را از Data Annotations (مثل [Description]) می‌خواند
    /// و آن‌ها را به Schema اضافه می‌کند
    /// </summary>
    public class SwaggerSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == null)
                return;

            // اضافه کردن توضیحات به فیلدهای Schema
            if (schema.Properties != null)
            {
                foreach (var property in context.Type.GetProperties())
                {
                    var propertyName = property.Name;
                    var camelCaseName = ToCamelCase(propertyName);

                    // بررسی Property در Schema (با نام اصلی یا camelCase)
                    if (schema.Properties.ContainsKey(propertyName) || 
                        schema.Properties.ContainsKey(camelCaseName))
                    {
                        var key = schema.Properties.ContainsKey(propertyName) 
                            ? propertyName 
                            : camelCaseName;

                        var propertySchema = schema.Properties[key];

                        // خواندن Description از [Description] attribute
                        var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
                        if (descriptionAttribute != null && !string.IsNullOrWhiteSpace(descriptionAttribute.Description))
                        {
                            propertySchema.Description = descriptionAttribute.Description;
                        }

                        // خواندن DisplayName از [DisplayName] attribute
                        var displayNameAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
                        if (displayNameAttribute != null && !string.IsNullOrWhiteSpace(displayNameAttribute.DisplayName))
                        {
                            if (string.IsNullOrWhiteSpace(propertySchema.Description))
                            {
                                propertySchema.Description = displayNameAttribute.DisplayName;
                            }
                        }

                        // خواندن توضیحات از XML Documentation (اگر موجود باشد)
                        // این بخش نیاز به XML Documentation Comments دارد
                        // که در صورت وجود، از آن استفاده می‌شود

                        // بهتر نمایش دادن Nullable types
                        if (IsNullable(property.PropertyType))
                        {
                            propertySchema.Nullable = true;
                        }

                        // بهتر نمایش دادن Enum ها
                        if (property.PropertyType.IsEnum)
                        {
                            propertySchema.Enum = new List<IOpenApiAny>();
                            foreach (var enumValue in Enum.GetValues(property.PropertyType))
                            {
                                propertySchema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(enumValue.ToString()));
                            }
                        }
                    }
                }
            }

            // اضافه کردن توضیحات به خود Schema (کلاس)
            var classDescription = context.Type.GetCustomAttribute<DescriptionAttribute>();
            if (classDescription != null && !string.IsNullOrWhiteSpace(classDescription.Description))
            {
                if (string.IsNullOrWhiteSpace(schema.Description))
                {
                    schema.Description = classDescription.Description;
                }
            }
        }

        /// <summary>
        /// تبدیل نام Property به camelCase
        /// </summary>
        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// بررسی اینکه آیا نوع Nullable است یا نه
        /// </summary>
        private bool IsNullable(Type type)
        {
            if (!type.IsValueType)
                return true; // Reference types are nullable

            if (Nullable.GetUnderlyingType(type) != null)
                return true; // Nullable<T>

            return false;
        }
    }
}

