using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BeanSceneReservationSystem.Data;

public static class DatabaseSeeder
{
    private const int TablesPerArea = 10;
    private const int NewTableCapacity = 4;

    // Official areas and the old names that should be renamed to them.
    private static readonly (string OfficialName, string[] LegacyNames, string Prefix)[] RestaurantAreas =
    {
        ("Main", new[] { "Main Dining" }, "M"),
        ("Outside", new[] { "Outdoor" }, "O"),
        ("Balcony", new[] { "Bar" }, "B")
    };

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await NormaliseRestaurantLayoutAsync(context);

        // Add the default breakfast, lunch, and dinner sittings once.
        if (!await context.Sittings.AnyAsync())
        {
            var sittings = new List<Sitting>
            {
                new Sitting
                {
                    SittingType = "Breakfast",
                    StartTime = DateTime.Today.AddHours(8),
                    EndTime = DateTime.Today.AddHours(11),
                    Capacity = 50,
                    IsClosed = false
                },
                new Sitting
                {
                    SittingType = "Lunch",
                    StartTime = DateTime.Today.AddHours(12),
                    EndTime = DateTime.Today.AddHours(15),
                    Capacity = 70,
                    IsClosed = false
                },
                new Sitting
                {
                    SittingType = "Dinner",
                    StartTime = DateTime.Today.AddHours(18),
                    EndTime = DateTime.Today.AddHours(22),
                    Capacity = 90,
                    IsClosed = false
                }
            };

            await context.Sittings.AddRangeAsync(sittings);
            await context.SaveChangesAsync();
        }
    }

    private static async Task NormaliseRestaurantLayoutAsync(ApplicationDbContext context)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        foreach (var (officialName, legacyNames, prefix) in RestaurantAreas)
        {
            var area = await EnsureAreaAsync(context, officialName, legacyNames);
            await NormaliseTablesAsync(context, area, prefix);
        }

        await transaction.CommitAsync();
    }

    private static async Task<Area> EnsureAreaAsync(
        ApplicationDbContext context,
        string officialName,
        string[] legacyNames)
    {
        var official = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == officialName);
        var legacyAreas = await context.Areas
            .Where(a => legacyNames.Contains(a.AreaName))
            .OrderBy(a => a.AreaID)
            .ToListAsync();

        if (official == null && legacyAreas.Count > 0)
        {
            official = legacyAreas[0];
            official.AreaName = officialName;
            legacyAreas.RemoveAt(0);
            await context.SaveChangesAsync();
        }
        else if (official == null)
        {
            official = new Area { AreaName = officialName };
            context.Areas.Add(official);
            await context.SaveChangesAsync();
        }

        foreach (var legacyArea in legacyAreas)
        {
            var tables = await context.Tables
                .Where(t => t.AreaID == legacyArea.AreaID)
                .ToListAsync();

            foreach (var table in tables)
            {
                table.AreaID = official.AreaID;
            }

            await context.SaveChangesAsync();

            var areaStillUsed = await context.Tables.AnyAsync(t => t.AreaID == legacyArea.AreaID);
            if (!areaStillUsed)
            {
                context.Areas.Remove(legacyArea);
                await context.SaveChangesAsync();
            }
        }

        return official;
    }

    // Rename old table numbers and add any missing M, O, or B tables.
    private static async Task NormaliseTablesAsync(ApplicationDbContext context, Area area, string prefix)
    {
        var tables = await context.Tables
            .Where(t => t.AreaID == area.AreaID)
            .OrderBy(t => t.TableID)
            .ToListAsync();

        var officialNumbers = Enumerable.Range(1, TablesPerArea)
            .Select(number => prefix + number)
            .ToList();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tablesToRename = new List<Table>();

        foreach (var table in tables)
        {
            if (TryGetOfficialTableNumber(table.TableNumber, prefix, out var canonical) && claimed.Add(canonical))
            {
                table.TableNumber = canonical;
            }
            else
            {
                tablesToRename.Add(table);
            }
        }

        foreach (var table in tablesToRename)
        {
            var nextNumber = officialNumbers.FirstOrDefault(number => !claimed.Contains(number));
            if (nextNumber == null)
            {
                continue;
            }

            table.TableNumber = nextNumber;
            claimed.Add(nextNumber);
        }

        await context.SaveChangesAsync();

        foreach (var number in officialNumbers.Where(number => !claimed.Contains(number)))
        {
            context.Tables.Add(new Table
            {
                AreaID = area.AreaID,
                TableNumber = number,
                Capacity = NewTableCapacity,
                IsAvailable = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static bool TryGetOfficialTableNumber(string tableNumber, string prefix, out string canonical)
    {
        canonical = tableNumber;
        if (string.IsNullOrWhiteSpace(tableNumber) ||
            tableNumber.Length <= prefix.Length ||
            !tableNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = tableNumber[prefix.Length..];
        if (!int.TryParse(suffix, out var number) || suffix != number.ToString())
        {
            return false;
        }

        if (number < 1 || number > TablesPerArea)
        {
            return false;
        }

        canonical = prefix + number;
        return true;
    }

    public static async Task SeedIdentityAsync(
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        // Create Manager, Staff, and Member roles when they do not exist.
        foreach (var roleName in new[] { "Manager", "Staff", "Member" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            }
        }

        await SeedOptionalUserAsync(
            userManager,
            configuration["SeedUsers:ManagerEmail"],
            configuration["SeedUsers:ManagerPassword"],
            "Manager");

        await SeedOptionalUserAsync(
            userManager,
            configuration["SeedUsers:StaffEmail"],
            configuration["SeedUsers:StaffPassword"],
            "Staff");
    }

    private static async Task SeedOptionalUserAsync(
        UserManager<ApplicationUser> userManager,
        string? email,
        string? password,
        string roleName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }
}
