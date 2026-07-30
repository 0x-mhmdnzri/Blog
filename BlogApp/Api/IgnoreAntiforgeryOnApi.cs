using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BlogApp.Api;

/// <summary>Skip global AutoValidateAntiforgery for pure API (key-authenticated) endpoints.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipAntiforgeryAttribute : Attribute, IFilterMetadata, IAntiforgeryPolicy
{
    public bool IsValid => true;
}
