using FluentValidation;
using HtmlElementsApi.Models;
using HtmlElementsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HtmlElementsApi.Controllers;

[ApiController]
[Route("api/elements")]
public class ElementsController : ControllerBase
{
    private readonly IElementsService _service;
    private readonly IValidator<ElementExtractRequest> _validator;

    public ElementsController(IElementsService service, IValidator<ElementExtractRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<ElementExtractResponse>> Post(
        [FromBody] ElementExtractRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            var detail = ModelState.ErrorCount > 0
                ? string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "invalid request body"
                        : e.ErrorMessage))
                : "request body is required and must be valid JSON";
            return Ok(ElementExtractResponse.Failure(ErrorCodes.MissingParameter, detail));
        }

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errorCode = validation.Errors[0].ErrorCode;
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Ok(ElementExtractResponse.Failure(errorCode, errorMessage));
        }

        var response = await _service.ProcessAsync(request, cancellationToken);
        return Ok(response);
    }
}
