namespace Qlcheck.Tests;

// A Fact that is skipped, with a message saying why, on machines without the ASP.NET Core
// reference pack (or runtime) next to the running .NET runtime.
internal sealed class AspNetPackFactAttribute : FactAttribute
{
    public AspNetPackFactAttribute()
    {
        if (SharedFrameworks.AspNetCore().Count == 0)
        {
            Skip = "The Microsoft.AspNetCore.App reference pack is not installed next to the running .NET runtime.";
        }
    }
}
