using System.Linq.Expressions;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DataPermissionFilter : IDataPermissionFilter
{
    public IQueryable<TEntity> Apply<TEntity>(
        IQueryable<TEntity> query,
        DataScopeContext dataScope,
        Expression<Func<TEntity, Guid?>> userIdSelector,
        Expression<Func<TEntity, Guid?>> departmentIdSelector)
    {
        if (dataScope.HasAllDataScope)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var body = BuildFilterExpression(dataScope, parameter, userIdSelector, departmentIdSelector);
        if (body is null)
        {
            return query.Where(_ => false);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    private static Expression? BuildFilterExpression<TEntity>(
        DataScopeContext dataScope,
        ParameterExpression parameter,
        Expression<Func<TEntity, Guid?>> userIdSelector,
        Expression<Func<TEntity, Guid?>> departmentIdSelector)
    {
        Expression? userFilter = null;
        Expression? departmentFilter = null;

        if (dataScope.ScopeType == DataScopeType.CurrentUser && dataScope.CurrentUserId.HasValue)
        {
            userFilter = Expression.Equal(
                ReplaceParameter(userIdSelector, parameter),
                Expression.Constant(dataScope.CurrentUserId, typeof(Guid?)));
        }

        if (dataScope.ScopeType is DataScopeType.CurrentDepartment
            or DataScopeType.CurrentDepartmentAndChildren
            or DataScopeType.CustomDepartments)
        {
            var departmentIds = dataScope.DepartmentIds
                .Distinct()
                .Select(id => (Guid?)id)
                .ToArray();

            if (departmentIds.Length > 0)
            {
                var containsMethod = typeof(Enumerable)
                    .GetMethods()
                    .Single(method =>
                        method.Name == nameof(Enumerable.Contains) &&
                        method.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(Guid?));

                departmentFilter = Expression.Call(
                    containsMethod,
                    Expression.Constant(departmentIds),
                    ReplaceParameter(departmentIdSelector, parameter));
            }
        }

        return userFilter is null ? departmentFilter : departmentFilter is null ? userFilter : Expression.OrElse(userFilter, departmentFilter);
    }

    private static Expression ReplaceParameter<TEntity>(
        Expression<Func<TEntity, Guid?>> selector,
        ParameterExpression parameter)
    {
        return new ParameterReplaceVisitor(selector.Parameters[0], parameter).Visit(selector.Body)
            ?? throw new InvalidOperationException("Unable to build data permission filter expression.");
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
