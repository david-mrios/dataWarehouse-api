using dw_api_web.Data;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<DatabaseConnectionManager>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Configura Swagger para generar la documentación de la API
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cube API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cube API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the root of the application
    });
}
else
{
    // Opción para habilitar Swagger en entornos no de desarrollo
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cube API v1");
        c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger in production
    });
}

app.UseAuthorization();

app.MapControllers();

app.Run();
