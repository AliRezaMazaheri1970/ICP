namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents a base class for value objects, which are objects defined by their properties rather than a unique identity.
    /// This class provides a standard implementation for value-based equality.
    /// </summary>
    public abstract class ValueObject
    {
        /// <summary>
        /// When implemented in a derived class, returns all components that are used for equality checks.
        /// </summary>
        /// <returns>An enumerable of objects that define the value of this object.</returns>
        protected abstract IEnumerable<object?> GetEqualityComponents();

        /// <summary>
        /// Determines whether the specified object is equal to the current object by comparing their value components.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            var other = (ValueObject)obj;

            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        /// <summary>
        /// Returns a hash code for the current object, calculated from its value components.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
        }

        /// <summary>
        /// Compares two ValueObject instances for equality.
        /// </summary>
        /// <param name="left">The first ValueObject to compare.</param>
        /// <param name="right">The second ValueObject to compare.</param>
        /// <returns>true if the two objects are equal; otherwise, false.</returns>
        public static bool operator ==(ValueObject? left, ValueObject? right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Compares two ValueObject instances for inequality.
        /// </summary>
        /// <param name="left">The first ValueObject to compare.</param>
        /// <param name="right">The second ValueObject to compare.</param>
        /// <returns>true if the two objects are not equal; otherwise, false.</returns>
        public static bool operator !=(ValueObject? left, ValueObject? right)
        {
            return !(left == right);
        }
    }
}