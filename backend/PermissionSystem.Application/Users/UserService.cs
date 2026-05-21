using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Excels;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IExcelService _excelService;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IPasswordHashService passwordHashService,
        IExcelService excelService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHashService = passwordHashService;
        _excelService = excelService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<UserResponse>> GetPagedAsync(UserQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(request);

        var totalCount = query.LongCount();
        var users = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();

        var result = users.Select(ToResponse).ToList();
        return Task.FromResult(PagedResult<UserResponse>.Create(result, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.UserName, "Username is required.");
        ValidateRequired(request.Password, "Password is required.");
        ValidateRequired(request.DisplayName, "Display name is required.");

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        if (_userRepository.Query().Any(entity => entity.TenantId == request.TenantId && entity.NormalizedUserName == normalizedUserName))
        {
            throw new BusinessException(ErrorCode.Conflict, "Username already exists.");
        }

        var user = new User
        {
            TenantId = request.TenantId,
            DepartmentId = request.DepartmentId,
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = _passwordHashService.HashPassword(request.Password),
            IsEnabled = request.IsEnabled
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);

        user.DepartmentId = request.DepartmentId;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.IsEnabled = request.IsEnabled;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);

        foreach (var relation in _userRoleRepository.Query().Where(entity => entity.UserId == id).ToList())
        {
            _userRoleRepository.Remove(relation);
        }

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(Guid id, SetUserEnabledRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        user.IsEnabled = request.IsEnabled;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.NewPassword, "New password is required.");

        var user = await GetUserOrThrowAsync(id, cancellationToken);
        user.PasswordHash = _passwordHashService.HashPassword(request.NewPassword);

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignRolesAsync(Guid id, AssignUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        var roleIds = request.RoleIds.Distinct().ToArray();
        var validRoleIds = _roleRepository.Query()
            .Where(entity => entity.TenantId == user.TenantId && roleIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();

        if (validRoleIds.Length != roleIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more roles are invalid.");
        }

        foreach (var relation in _userRoleRepository.Query().Where(entity => entity.UserId == id).ToList())
        {
            _userRoleRepository.Remove(relation);
        }

        foreach (var roleId in validRoleIds)
        {
            await _userRoleRepository.AddAsync(new UserRole
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = roleId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<byte[]> ExportAsync(UserQueryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = ApplyQuery(request)
            .OrderBy(entity => entity.UserName)
            .Select(entity => new UserExportRow
            {
                UserName = entity.UserName,
                DisplayName = entity.DisplayName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                IsEnabled = entity.IsEnabled,
                CreatedAt = entity.CreatedAt
            })
            .ToList();

        return _excelService.ExportAsync(
            new ExportRequest<UserExportRow>
            {
                SheetName = "Users",
                Items = rows
            },
            cancellationToken);
    }

    public Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        return _excelService.CreateTemplateAsync<UserImportRow>("User Import Template", cancellationToken);
    }

    public async Task<ImportResult<UserImportRow>> ImportPreviewAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = await _excelService.ImportAsync<UserImportRow>(stream, cancellationToken);
        var errors = result.Errors.ToList();
        var validItems = new List<UserImportRow>();
        var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 1;

        foreach (var item in result.Items)
        {
            rowNumber++;
            var normalizedUserName = item.UserName.Trim().ToUpperInvariant();
            var hasError = false;

            if (!seenUserNames.Add(normalizedUserName))
            {
                errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    ColumnName = "Username",
                    Message = "Username is duplicated in the import file.",
                    RawValue = item.UserName
                });
                hasError = true;
            }

            if (_userRepository.Query().Any(entity => entity.NormalizedUserName == normalizedUserName))
            {
                errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    ColumnName = "Username",
                    Message = "Username already exists.",
                    RawValue = item.UserName
                });
                hasError = true;
            }

            if (!hasError)
            {
                validItems.Add(item);
            }
        }

        return new ImportResult<UserImportRow>
        {
            TotalRows = result.TotalRows,
            SuccessRows = validItems.Count,
            FailedRows = errors.Select(error => error.RowNumber).Distinct().Count(),
            Items = validItems,
            Errors = errors
        };
    }

    private async Task<User> GetUserOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "User was not found.");
    }

    private IQueryable<User> ApplyQuery(UserQueryRequest request)
    {
        var query = _userRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.UserName.Contains(keyword) ||
                entity.DisplayName.Contains(keyword) ||
                (entity.Email != null && entity.Email.Contains(keyword)));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        return query;
    }

    private UserResponse ToResponse(User user)
    {
        var roleIds = _userRoleRepository.Query()
            .Where(entity => entity.UserId == user.Id)
            .Select(entity => entity.RoleId)
            .ToArray();

        return new UserResponse
        {
            Id = user.Id,
            TenantId = user.TenantId,
            DepartmentId = user.DepartmentId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsEnabled = user.IsEnabled,
            CreatedAt = user.CreatedAt,
            RoleIds = roleIds
        };
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
