using System;

namespace WinformsMVP.Common.Helpers
{
    public sealed class EqualityHelper
    {
        public static bool Equals<T>(T left, T right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            if (left is IEquatable<T> equatableLeft)
                return equatableLeft.Equals(right);

            if (left is IComparable<T> comparableLeft)
                return comparableLeft.CompareTo(right) == 0;

            return left.Equals(right);
        }
    }
}
