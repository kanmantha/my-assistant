using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Search;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    private readonly ICurrentUserService _currentUser;

    public SearchController(ISearchService search, ICurrentUserService currentUser)
    {
        _search = search;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string[]? scopes, CancellationToken ct)
    {
        var result = await _search.SearchAsync(_currentUser.UserId!.Value, new SearchRequest { Query = q ?? string.Empty, Scopes = scopes }, ct);
        return Ok(ApiResponse<SearchResponse>.Ok(result));
    }
}
