using Microsoft.AspNetCore.Components;

namespace Softwareschmiede.Components;

public partial class App
{
    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    private string BaseHref => Configuration.GetSection("Hosting").GetValue<string>("BasePath") ?? "/";

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }
}
