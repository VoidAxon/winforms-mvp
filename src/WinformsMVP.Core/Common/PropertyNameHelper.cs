using System;
using System.Linq.Expressions;

namespace WinformsMVP.Common
{
    public class PropertyNameHelper
    {
        public static string GetName<TViewModel, TValue>(Expression<Func<TViewModel, TValue>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression member)
                return member.Member.Name;

            if (propertyExpression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            throw new ArgumentException("Invalid property expression", nameof(propertyExpression));
        }

        public static string GetName<TViewModel>(Expression<Func<TViewModel, object>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression member)
                return member.Member.Name;

            if (propertyExpression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            throw new ArgumentException("Invalid property expression", nameof(propertyExpression));
        }
    }
}
