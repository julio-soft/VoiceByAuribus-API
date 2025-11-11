using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Filters;

public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                .ToList();

            var response = ApiResponse<object>.ErrorResponse("Validation failed", errors);
            
            context.Result = new BadRequestObjectResult(response);
            return;
        }

        await next();
    }
}
