using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Files;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Files;

public sealed class FileSecurityAndCompensationTests
{
    [Fact]
    public async Task Scanner_ShouldRejectExtensionAndMagicMismatch()
    {
        var scanner = new FileContentScanner();

        await using var content = new MemoryStream([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var result = await scanner.ScanAsync(new FileScanRequest
        {
            Content = content,
            FileName = "payload.txt",
            Extension = ".txt",
            ClientContentType = "text/plain"
        });

        Assert.False(result.IsClean);
        Assert.Contains("match", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scanner_ShouldRejectEicarSignature()
    {
        var scanner = new FileContentScanner();
        var eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

        await using var content = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(eicar));
        var result = await scanner.ScanAsync(new FileScanRequest
        {
            Content = content,
            FileName = "payload.txt",
            Extension = ".txt",
            ClientContentType = "text/plain"
        });

        Assert.False(result.IsClean);
        Assert.Contains("Malware", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileService_ShouldStreamHashAndActivateAfterStorageSave()
    {
        var bytes = new byte[] { 0xff, 0xd8, 0xff, 0x00, 0x01 };
        var repository = new InMemoryRepository<FileResource>();
        var storage = new TestFileStorageService();
        var service = new FileService(
            repository,
            new TestUnitOfWork(),
            storage,
            new FileContentScanner(),
            new AllowAllFileBusinessAccessChecker(),
            new TestCurrentUserService(),
            new TestTenantWriteResolver(),
            new FileStorageOptions());

        await using var content = new MemoryStream(bytes);
        var result = await service.UploadAsync(new UploadFileRequest
        {
            Content = content,
            OriginalName = "photo.jpg",
            ContentType = "image/jpeg",
            Size = bytes.Length
        });

        Assert.Equal(FileStatus.Active, result.FileStatus);
        Assert.Equal(FileScanStatus.Clean, result.ScanStatus);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), result.Sha256);
        Assert.Single(storage.SavedObjectKeys);
        Assert.Equal(FileStatus.Active, repository.Items.Single().FileStatus);
    }

    [Fact]
    public async Task BusinessAccessChecker_ShouldFollowOrderDataPermission()
    {
        var visibleOrder = new DemoBusinessOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            CreatedBy = TestIds.NormalUserId,
            DepartmentId = null
        };
        var hiddenOrder = new DemoBusinessOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            CreatedBy = TestIds.AdminUserId,
            DepartmentId = null
        };
        var checker = new FileBusinessAccessChecker(
            new InMemoryRepository<DemoBusinessOrder>(visibleOrder, hiddenOrder),
            new FixedDataScopeService(new DataScopeContext
            {
                ScopeType = DataScopeType.CurrentUser,
                CurrentUserId = TestIds.NormalUserId
            }),
            new DataPermissionFilter());

        Assert.True(await checker.CanAccessAsync(
            DemoBusinessOrderConstants.BusinessType,
            visibleOrder.Id));
        Assert.False(await checker.CanAccessAsync(
            DemoBusinessOrderConstants.BusinessType,
            hiddenOrder.Id));
    }

    [Fact]
    public async Task FileService_ShouldRejectDownloadOutsideBusinessDataScope()
    {
        var file = new FileResource
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            OriginalName = "secret.jpg",
            FileName = "secret.jpg",
            Extension = ".jpg",
            ContentType = "image/jpeg",
            Size = 5,
            StorageProvider = "Test",
            BucketName = "default",
            ObjectKey = "files/secret.jpg",
            FileStatus = FileStatus.Active,
            ScanStatus = FileScanStatus.Clean,
            BusinessType = DemoBusinessOrderConstants.BusinessType,
            BusinessId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var storage = new TestFileStorageService();
        storage.Objects[file.ObjectKey] = [0xff, 0xd8, 0xff, 0x00, 0x01];
        var service = new FileService(
            new InMemoryRepository<FileResource>(file),
            new TestUnitOfWork(),
            storage,
            new FileContentScanner(),
            new DenyFileBusinessAccessChecker(),
            new TestCurrentUserService(),
            new TestTenantWriteResolver(),
            new FileStorageOptions());

        var exception = await Assert.ThrowsAsync<PermissionSystem.Shared.Exceptions.BusinessException>(() =>
            service.DownloadAsync(file.Id));

        Assert.Equal(PermissionSystem.Shared.Constants.ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(0, storage.OpenReadCount);
    }

    [Fact]
    public async Task CompensationJob_ShouldFinalizePendingUpload()
    {
        var file = new FileResource
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            OriginalName = "photo.jpg",
            FileName = "photo.jpg",
            Extension = ".jpg",
            ContentType = "image/jpeg",
            Size = 5,
            StorageProvider = "Test",
            BucketName = "default",
            ObjectKey = "files/photo.jpg",
            FileStatus = FileStatus.Pending,
            ScanStatus = FileScanStatus.Clean,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var repository = new InMemoryRepository<FileResource>(file);
        var storage = new TestFileStorageService();
        storage.Objects[file.ObjectKey] = [0xff, 0xd8, 0xff, 0x00, 0x01];
        var job = CreateCompensationJob(repository, storage);

        await job.ExecuteAsync();

        Assert.Equal(FileStatus.Active, file.FileStatus);
        Assert.Equal(FileScanStatus.Clean, file.ScanStatus);
        Assert.Null(file.NextRetryAt);
    }

    [Fact]
    public async Task CompensationJob_ShouldRetryDeleteAndEventuallySoftDelete()
    {
        var file = new FileResource
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            OriginalName = "photo.jpg",
            FileName = "photo.jpg",
            Extension = ".jpg",
            ContentType = "image/jpeg",
            Size = 5,
            StorageProvider = "Test",
            BucketName = "default",
            ObjectKey = "files/photo.jpg",
            FileStatus = FileStatus.PendingDelete,
            ScanStatus = FileScanStatus.Clean,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var repository = new InMemoryRepository<FileResource>(file);
        var storage = new TestFileStorageService
        {
            FailDeleteCount = 1
        };
        storage.Objects[file.ObjectKey] = [0xff, 0xd8, 0xff, 0x00, 0x01];
        var job = CreateCompensationJob(repository, storage);

        await job.ExecuteAsync();
        Assert.False(file.IsDeleted);
        Assert.NotNull(file.NextRetryAt);

        file.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await job.ExecuteAsync();

        Assert.True(file.IsDeleted);
        Assert.Equal(FileStatus.Deleted, file.FileStatus);
        Assert.Empty(storage.Objects);
    }

    private static FileStorageCompensationJob CreateCompensationJob(
        IRepository<FileResource> repository,
        TestFileStorageService storage)
    {
        return new FileStorageCompensationJob(
            repository,
            storage,
            new FileContentScanner(),
            new TestSystemTenantScope(),
            new TestUnitOfWork(),
            NullLogger<FileStorageCompensationJob>.Instance);
    }

    private sealed class AllowAllFileBusinessAccessChecker : IFileBusinessAccessChecker
    {
        public Task<bool> CanAccessAsync(string? businessType, Guid? businessId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task EnsureAccessAsync(FileResource fileResource, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DenyFileBusinessAccessChecker : IFileBusinessAccessChecker
    {
        public Task<bool> CanAccessAsync(string? businessType, Guid? businessId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task EnsureAccessAsync(FileResource fileResource, CancellationToken cancellationToken = default)
        {
            throw new PermissionSystem.Shared.Exceptions.BusinessException(
                PermissionSystem.Shared.Constants.ErrorCode.NotFound,
                "File was not found.");
        }
    }

    private sealed class FixedDataScopeService : IDataScopeService
    {
        private readonly DataScopeContext _context;

        public FixedDataScopeService(DataScopeContext context)
        {
            _context = context;
        }

        public Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context);
        }

        public Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestSystemTenantScope : ISystemTenantScope
    {
        public IDisposable Begin(string operation) => new Scope();

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class TestFileStorageService : IFileStorageService
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public List<string> SavedObjectKeys { get; } = [];

        public int FailDeleteCount { get; set; }

        public int OpenReadCount { get; private set; }

        public string StorageProvider => "Test";

        public FileStorageReference CreateReference(Guid fileId, string extension)
        {
            return new FileStorageReference
            {
                StorageProvider = StorageProvider,
                BucketName = "default",
                ObjectKey = $"files/{fileId:N}{extension}"
            };
        }

        public async Task<FileStorageSaveResult> SaveAsync(
            FileStorageSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            Objects[request.Reference.ObjectKey] = memory.ToArray();
            SavedObjectKeys.Add(request.Reference.ObjectKey);
            return new FileStorageSaveResult
            {
                StorageProvider = StorageProvider,
                BucketName = request.Reference.BucketName,
                ObjectKey = request.Reference.ObjectKey
            };
        }

        public Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default)
        {
            OpenReadCount++;
            if (!Objects.TryGetValue(fileResource.ObjectKey, out var content))
            {
                throw new FileNotFoundException();
            }

            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default)
        {
            if (FailDeleteCount > 0)
            {
                FailDeleteCount--;
                throw new IOException("Storage delete failed.");
            }

            Objects.Remove(fileResource.ObjectKey);
            return Task.CompletedTask;
        }
    }
}
