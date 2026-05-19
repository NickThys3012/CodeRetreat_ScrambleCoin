using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ScrambleCoin.Api.Swagger;

/// <summary>
/// Applies the <c>X-Admin-Key</c> security requirement to operations that need it
/// (currently: <c>CreateGame</c>).
/// </summary>
public sealed class AdminKeyOperationFilter : IOperationFilter
{
    private static readonly string[] AdminKeyOperations = ["CreateGame"];

    private static readonly OpenApiSecurityRequirement Requirement = new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "X-Admin-Key"
                }
            },
            []
        }
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (AdminKeyOperations.Contains(operation.OperationId))
        {
            operation.Security.Add(Requirement);
        }
    }
}
