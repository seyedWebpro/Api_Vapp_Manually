using Api_Vapp.Constants;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Data
{
    /// <summary>
    /// داده‌های اولیه دیتابیس (نقش‌های پیش‌فرض و کاربر ادمین)
    /// </summary>
    public static class DatabaseSeeder
    {
        public const string DefaultAdminPhoneNumber = "09920374397";
        public const string AdminRoleName = "Admin";

        private static readonly DefaultRoleSeed[] DefaultRoles =
        [
            new(AdminRoleName, "مدیر سیستم", "دسترسی کامل به پنل مدیریت")
        ];

        public static async Task SeedAsync(Api_Context context, ILogger logger)
        {
            var roles = await SeedDefaultRolesAsync(context, logger);
            var adminRole = roles[AdminRoleName];

            var adminUser = await EnsureDefaultAdminUserAsync(context, logger);
            await EnsureAdminUserRoleAsync(context, adminUser, adminRole, logger);
            await SeedSubscriptionFeaturesAsync(context, logger);
            await SeedSubscriptionPlansAsync(context, logger);
        }

        private static async Task<IReadOnlyDictionary<string, Role>> SeedDefaultRolesAsync(
            Api_Context context,
            ILogger logger)
        {
            var rolesByName = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

            foreach (var seed in DefaultRoles)
            {
                var role = await EnsureRoleAsync(context, seed, logger);
                rolesByName[role.Name] = role;
            }

            return rolesByName;
        }

        private static async Task<Role> EnsureRoleAsync(
            Api_Context context,
            DefaultRoleSeed seed,
            ILogger logger)
        {
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == seed.Name && !r.IsDeleted);

            if (role != null)
            {
                return role;
            }

            var softDeletedRole = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == seed.Name);

            if (softDeletedRole != null)
            {
                softDeletedRole.IsDeleted = false;
                softDeletedRole.IsActive = true;
                softDeletedRole.DisplayName = seed.DisplayName;
                softDeletedRole.Description = seed.Description;
                softDeletedRole.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                logger.LogInformation("Role {RoleName} restored from soft delete.", seed.Name);
                return softDeletedRole;
            }

            role = new Role
            {
                Name = seed.Name,
                DisplayName = seed.DisplayName,
                Description = seed.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Roles.Add(role);
            await context.SaveChangesAsync();
            logger.LogInformation("Role {RoleName} seeded.", seed.Name);

            return role;
        }

        private static async Task<User> EnsureDefaultAdminUserAsync(Api_Context context, ILogger logger)
        {
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == DefaultAdminPhoneNumber);

            if (user == null)
            {
                user = new User
                {
                    PhoneNumber = DefaultAdminPhoneNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    FullName = "ادمین پیش‌فرض",
                    IsActive = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();
                logger.LogInformation("Default admin user {PhoneNumber} created.", DefaultAdminPhoneNumber);
                return user;
            }

            var changed = false;

            if (user.IsDeleted)
            {
                user.IsDeleted = false;
                changed = true;
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                changed = true;
            }

            if (!user.IsPhoneVerified)
            {
                user.IsPhoneVerified = true;
                changed = true;
            }

            if (changed)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                logger.LogInformation("Default admin user {PhoneNumber} reactivated.", DefaultAdminPhoneNumber);
            }

            return user;
        }

        private static async Task EnsureAdminUserRoleAsync(
            Api_Context context,
            User adminUser,
            Role adminRole,
            ILogger logger)
        {
            var hasActiveAdminRole = await context.UserRoles.AnyAsync(ur =>
                ur.UserId == adminUser.Id
                && ur.RoleId == adminRole.Id
                && ur.IsActive
                && !ur.IsDeleted);

            if (hasActiveAdminRole)
            {
                return;
            }

            var existingUserRole = await context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id);

            if (existingUserRole != null)
            {
                existingUserRole.IsDeleted = false;
                existingUserRole.IsActive = true;
                existingUserRole.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation(
                "Default admin user {PhoneNumber} assigned Admin role.",
                DefaultAdminPhoneNumber);
        }

        private static async Task SeedSubscriptionFeaturesAsync(Api_Context context, ILogger logger)
        {
            var existingByCode = await context.SubscriptionFeatures
                .Where(f => !f.IsDeleted)
                .ToDictionaryAsync(f => f.Code, StringComparer.OrdinalIgnoreCase);

            foreach (var definition in SubscriptionFeatureCatalog.All)
            {
                if (existingByCode.TryGetValue(definition.Code, out var feature))
                {
                    if (SubscriptionFeatureCodes.IsKnown(feature.Code))
                    {
                        feature.Name = definition.Name;
                        feature.Description = definition.Description;
                        feature.SortOrder = definition.SortOrder;
                        feature.UpdatedAt = DateTime.UtcNow;
                    }

                    continue;
                }

                var softDeleted = await context.SubscriptionFeatures
                    .FirstOrDefaultAsync(f => f.Code == definition.Code);

                if (softDeleted != null)
                {
                    softDeleted.IsDeleted = false;
                    softDeleted.IsActive = true;
                    softDeleted.Name = definition.Name;
                    softDeleted.Description = definition.Description;
                    softDeleted.SortOrder = definition.SortOrder;
                    softDeleted.UpdatedAt = DateTime.UtcNow;
                    existingByCode[definition.Code] = softDeleted;
                    continue;
                }

                context.SubscriptionFeatures.Add(new SubscriptionFeature
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Description = definition.Description,
                    SortOrder = definition.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Subscription features seeded.");
        }

        private static async Task SeedSubscriptionPlansAsync(Api_Context context, ILogger logger)
        {
            var featuresByCode = await context.SubscriptionFeatures
                .Where(f => f.IsActive && !f.IsDeleted)
                .ToDictionaryAsync(f => f.Code, StringComparer.OrdinalIgnoreCase);

            foreach (var planDefinition in SubscriptionPlanCatalog.DefaultPlans)
            {
                var plan = await context.SubscriptionPlans
                    .Include(p => p.PlanFeatures)
                    .FirstOrDefaultAsync(p => p.TierCode == planDefinition.TierCode && !p.IsDeleted);

                if (plan == null)
                {
                    plan = new SubscriptionPlan
                    {
                        Name = planDefinition.Name,
                        TierCode = planDefinition.TierCode,
                        Description = planDefinition.Description,
                        Price = planDefinition.Price,
                        SortOrder = planDefinition.SortOrder,
                        DurationDays = 30,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var featureCode in planDefinition.FeatureCodes)
                    {
                        if (!featuresByCode.TryGetValue(featureCode, out var feature))
                            continue;

                        plan.PlanFeatures.Add(new SubscriptionPlanFeature { Feature = feature });
                    }

                    SyncLegacyPlanFlags(plan, planDefinition.FeatureCodes);
                    context.SubscriptionPlans.Add(plan);
                    continue;
                }

                if (plan.PlanFeatures.Count == 0)
                {
                    foreach (var featureCode in planDefinition.FeatureCodes)
                    {
                        if (!featuresByCode.TryGetValue(featureCode, out var feature))
                            continue;

                        plan.PlanFeatures.Add(new SubscriptionPlanFeature
                        {
                            SubscriptionPlanId = plan.Id,
                            SubscriptionFeatureId = feature.Id
                        });
                    }

                    SyncLegacyPlanFlags(plan, planDefinition.FeatureCodes);
                    plan.UpdatedAt = DateTime.UtcNow;
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Subscription plans seeded.");
        }

        private static void SyncLegacyPlanFlags(SubscriptionPlan plan, IEnumerable<string> featureCodes)
        {
            var codes = featureCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            plan.FreeQuickSendEnabled = codes.Contains(SubscriptionFeatureCodes.FreeQuickSend);
            plan.BusinessCardEnabled = codes.Contains(SubscriptionFeatureCodes.BusinessCard);
        }

        private sealed record DefaultRoleSeed(string Name, string DisplayName, string Description);
    }
}
