using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Optimize_DB_Updates;
using Optimize_DB_Updates.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DatabaseContext>(
    o => o.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionString")));

var app = builder.Build();

app.UseHttpsRedirection();

// Performance: 239ms
app.MapPut("increase-salaries", async (int companyId, DatabaseContext dbContext) =>
{
    var company = await dbContext
    .Set<Company>()
    .Include(c => c.Employees)
    .FirstOrDefaultAsync(c => c.Id == companyId);

    if (company is null)
    {
        return Results.NotFound($"The company with Id: {companyId} was not found");
    }

    foreach (var employee in company.Employees)
    {
        employee.Salary *= 1.1m;
    }

    company.LastSalaryUpdateUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync();

    return Results.NoContent();
});

// Performance: 39ms
app.MapPut("increase-salaries-sql", async (int companyId, DatabaseContext dbContext) =>
{
    var company = await dbContext
    .Set<Company>()
    .FirstOrDefaultAsync(c => c.Id == companyId);

    if (company is null)
    {
        return Results.NotFound($"The company with Id: {companyId} was not found");
    }

    // Issue with this endpoint is that the SQL statement still executes regardless of the saveChanges call
    // Fix by wrapping in beginTransaction and CommitTransaction
    await dbContext.Database.BeginTransactionAsync();

    await dbContext.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE Employees SET Salary = Salary * 1.1 WHERE CompanyId = {company.Id}");

    company.LastSalaryUpdateUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync();

    await dbContext.Database.CommitTransactionAsync();

    return Results.NoContent();
});

// Performance: 32ms
app.MapPut("increase-salaries-dapper", async (int companyId, DatabaseContext dbContext) =>
{
    var company = await dbContext
    .Set<Company>()
    .FirstOrDefaultAsync(c => c.Id == companyId);

    if (company is null)
    {
        return Results.NotFound($"The company with Id: {companyId} was not found");
    }

    // Issue with this endpoint is that the SQL statement still executes regardless of the saveChanges call
    // Fix by wrapping in beginTransaction and CommitTransaction
    var transaction = await dbContext.Database.BeginTransactionAsync();

    await dbContext.Database.GetDbConnection().ExecuteAsync(
        "UPDATE Employees SET SALARY = SALARY * 1.1 WHERE CompanyId = @CompanyId",
        new { CompanyId = companyId },
        transaction.GetDbTransaction());

    company.LastSalaryUpdateUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync();

    await dbContext.Database.CommitTransactionAsync();

    return Results.NoContent();
});

app.Run();
