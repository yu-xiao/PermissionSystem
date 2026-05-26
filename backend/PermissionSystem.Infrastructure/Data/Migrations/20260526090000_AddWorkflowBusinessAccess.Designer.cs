using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PermissionSystem.Infrastructure.Data;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526090000_AddWorkflowBusinessAccess")]
    partial class AddWorkflowBusinessAccess
    {
    }
}
