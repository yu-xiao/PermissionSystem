using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PermissionSystem.Infrastructure.Data;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260525093000_AdjustWorkflowBusinessBindingIndex")]
    partial class AdjustWorkflowBusinessBindingIndex
    {
    }
}
