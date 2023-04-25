using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MiniValidation;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;
using SupplierRegistrationMinimalAPI.Data;
using SupplierRegistrationMinimalAPI.Models;


namespace SupplierRegistrationMinimalAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Configure Services

            builder.Services.AddDbContext<MinimalContextDb>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("SupplierRegistrationMinimalAPI")));

            builder.Services.AddIdentityConfiguration();
            builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RemoveSupplier", policy => policy.RequireClaim("RemoveSupplier"));
            });


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Minimal API Sample",
                    Description = "Daniel Felix - .NET Developer",
                    Contact = new OpenApiContact { Name = "Daniel Felix", Email = "felixdaniel-developer@outlook.com" },
                    License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Insira o token JWT desta maneira: Bearer {seu token}",
                    Name = "Authorization",
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                        new string[] {}
                    }
                });
            });

            var app = builder.Build();

            #endregion

            #region Configure Pipeline

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthConfiguration();
            app.UseHttpsRedirection();

            MapActions(app);

            app.Run();

            #endregion

            #region Endpoints

            void MapActions(WebApplication app)
            {

                app.MapPost("/register", [AllowAnonymous] async (
                    SignInManager<IdentityUser> signInManager,
                    UserManager<IdentityUser> userManager,
                    IOptions<AppJwtSettings> appJwtSettings,
                    RegisterUser registerUser) =>
                {
                    if (registerUser == null)
                        return Results.BadRequest("User not found!");

                    if (!MiniValidator.TryValidate(registerUser, out var errors))
                        return Results.ValidationProblem(errors);

                    var user = new IdentityUser
                    {
                        UserName = registerUser.Email,
                        Email = registerUser.Email,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(user, registerUser.Password);
                    if (!result.Succeeded)
                        return Results.BadRequest(result.Errors);

                    var jwt = new JwtBuilder()
                            .WithUserManager(userManager)
                            .WithJwtSettings(appJwtSettings.Value)
                            .WithEmail(user.Email)
                            .WithJwtClaims()
                            .WithUserClaims()
                            .WithUserRoles()
                            .BuildUserResponse();

                    return Results.Ok(jwt);
                })
                .ProducesValidationProblem()
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("RegisterUser")
                .WithTags("User");

                app.MapPost("/login", [AllowAnonymous] async (
                    SignInManager<IdentityUser> signInManager,
                    UserManager<IdentityUser> userManager,
                    IOptions<AppJwtSettings> appJwtSettings,
                    LoginUser loginUser) =>
                {
                    if (loginUser == null)
                        return Results.BadRequest("Uninformed user");

                    if (!MiniValidator.TryValidate(loginUser, out var errors))
                        return Results.ValidationProblem(errors);

                    var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);
                    if (result.IsLockedOut)
                        return Results.BadRequest("Blocked user");

                    if (!result.Succeeded)
                        return Results.BadRequest("Username or password is invalid");

                    var jwt = new JwtBuilder()
                        .WithUserManager(userManager)
                        .WithJwtSettings(appJwtSettings.Value)
                        .WithEmail(loginUser.Email)
                        .WithJwtClaims()
                        .WithUserClaims()
                        .WithUserRoles()
                        .BuildUserResponse();

                    return Results.Ok(jwt);
                })
                .ProducesValidationProblem()
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("LoginUser")
                .WithTags("User");


                app.MapGet("/supplier", [AllowAnonymous] async (MinimalContextDb context) =>
                    await context.Suppliers.ToListAsync())
                    .WithName("GetSupplier")
                    .WithTags("Supplier");

                app.MapGet("/supplier/{id}", async (Guid id, MinimalContextDb context) =>
                    await context.Suppliers.FindAsync(id)
                        is SupplierModel supplier
                        ? Results.Ok(supplier)
                        : Results.NotFound())
                    .Produces<SupplierModel>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status404NotFound)
                    .WithName("GetSupplierById")
                    .WithTags("Supplier");

                app.MapPost("/supplier", [Authorize] async (MinimalContextDb context, SupplierModel supplier) =>
                {
                    if (!MiniValidator.TryValidate(supplier, out var errors))
                        return Results.ValidationProblem(errors);

                    context.Suppliers.Add(supplier);
                    var result = await context.SaveChangesAsync();

                    return result > 0 ? Results.Created($"/supplier/{supplier.Id}", supplier) : Results.BadRequest("There was a problem saving the record.");

                })
                .ProducesValidationProblem()
                .Produces<SupplierModel>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("PostSupplier")
                .WithTags("Supplier");

                app.MapPut("/supplier/{id}", [Authorize] async (Guid id, MinimalContextDb context, SupplierModel supplier) =>
                {
                    var supplierDb = await context.Suppliers.AsNoTracking<SupplierModel>().FirstOrDefaultAsync(f => f.Id == id);
                    if (supplierDb == null) return Results.NotFound();

                    if (!MiniValidator.TryValidate(supplier, out var errors))
                        return Results.ValidationProblem(errors);

                    context.Suppliers.Update(supplier);
                    var result = await context.SaveChangesAsync();

                    return result > 0 ? Results.NoContent() : Results.BadRequest("There was a problem saving the record.");

                })
                .ProducesValidationProblem()
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("PutSupplier")
                .WithTags("Supplier");

                app.MapDelete("/supplier/{id}", [Authorize] async (Guid id, MinimalContextDb context) =>
                {
                    var supplier = await context.Suppliers.FindAsync(id);
                    if (supplier == null) return Results.NotFound();

                    context.Suppliers.Remove(supplier);
                    var result = await context.SaveChangesAsync();

                    return result > 0 ? Results.Created($"/supplier/{supplier.Id}", supplier) : Results.BadRequest("There was a problem saving the record.");

                })
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .RequireAuthorization("RemoveSupplier")
                .WithName("DeleteSupplier")
                .WithTags("Supplier");
            }

            #endregion
        }
    }
}