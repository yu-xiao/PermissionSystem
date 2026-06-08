using System.Text.Json;
using PermissionSystem.Application.PrintTemplates;

namespace PermissionSystem.Tests;

public sealed class PrintTemplateRendererTests
{
    [Fact]
    public void Render_ShouldReplaceVariablesAndEncodeHtml()
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            OrderNo = "PO202605260001",
            ApplicantName = "<Admin>"
        });

        var html = PrintTemplateRenderer.Render("<h1>{{OrderNo}}</h1><p>{{ApplicantName}}</p>", data);

        Assert.Contains("<h1>PO202605260001</h1>", html);
        Assert.Contains("&lt;Admin&gt;", html);
    }

    [Fact]
    public void Render_ShouldExpandItemLoop()
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            items = new[]
            {
                new { Name = "Item A", Qty = 2, Price = 10 },
                new { Name = "Item B", Qty = 3, Price = 20 }
            }
        });

        var html = PrintTemplateRenderer.Render("{{#items}}<span>{{Name}}:{{Qty}}:{{Price}}</span>{{/items}}", data);

        Assert.Contains("<span>Item A:2:10</span>", html);
        Assert.Contains("<span>Item B:3:20</span>", html);
    }
}
