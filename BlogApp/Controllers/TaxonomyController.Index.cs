using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

public partial class TaxonomyController
{
    /// <summary>Admin nav links to /Taxonomy → Index. Categories is the full page.</summary>
    [HttpGet]
    public Task<IActionResult> Index() => Categories();
}
