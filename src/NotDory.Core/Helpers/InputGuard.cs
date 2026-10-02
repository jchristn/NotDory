namespace NotDory.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Shared checks for values that arrive in request bodies, settings files, or database rows: string length limits,
    /// list cleanup and count limits, and clamping numbers to a range with a fallback for NaN and infinity. Numbers are
    /// clamped so a slightly out-of-range value still works; oversized strings and lists are rejected with an
    /// <see cref="ArgumentOutOfRangeException"/>, which the REST layer reports as 400.
    /// </summary>
    public static class InputGuard
    {
        #region Public-Methods

        /// <summary>
        /// Reject a string longer than a limit.
        /// </summary>
        /// <param name="value">The value; null passes.</param>
        /// <param name="maxLength">The longest allowed length in characters.</param>
        /// <param name="name">The member name, for the error message.</param>
        /// <returns>The value unchanged.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is longer than the limit.</exception>
        public static string? MaxLength(string? value, int maxLength, string name)
        {
            if (value != null && value.Length > maxLength) throw new ArgumentOutOfRangeException(name, name + " may be at most " + maxLength + " characters (got " + value.Length + ").");
            return value;
        }

        /// <summary>
        /// Clean a list of strings: null becomes an empty list, null and blank entries are dropped, entries are trimmed,
        /// and the count and each entry's length are limited.
        /// </summary>
        /// <param name="values">The list; may be null.</param>
        /// <param name="maxCount">The most entries allowed after cleanup.</param>
        /// <param name="maxItemLength">The longest allowed entry.</param>
        /// <param name="name">The member name, for the error message.</param>
        /// <returns>A new, cleaned list.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there are too many entries or one is too long.</exception>
        public static List<string> CleanList(IEnumerable<string?>? values, int maxCount, int maxItemLength, string name)
        {
            List<string> result = new List<string>();
            if (values == null) return result;
            foreach (string? value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                string trimmed = value.Trim();
                MaxLength(trimmed, maxItemLength, name + " entry");
                result.Add(trimmed);
            }

            if (result.Count > maxCount) throw new ArgumentOutOfRangeException(name, name + " may have at most " + maxCount + " entries (got " + result.Count + ").");
            return result;
        }

        /// <summary>
        /// Reject a list with more entries than a limit.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The list; null passes.</param>
        /// <param name="maxCount">The most entries allowed.</param>
        /// <param name="name">The member name, for the error message.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the list has too many entries.</exception>
        public static List<T>? MaxCount<T>(List<T>? values, int maxCount, string name)
        {
            if (values != null && values.Count > maxCount) throw new ArgumentOutOfRangeException(name, name + " may have at most " + maxCount + " entries (got " + values.Count + ").");
            return values;
        }

        /// <summary>
        /// Clamp a number to a range; NaN and infinity become the fallback.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="minimum">The smallest allowed value.</param>
        /// <param name="maximum">The largest allowed value.</param>
        /// <param name="fallback">The value used for NaN or infinity.</param>
        /// <returns>The clamped value.</returns>
        public static double Clamp(double value, double minimum, double maximum, double fallback)
        {
            if (!double.IsFinite(value)) return fallback;
            return Math.Clamp(value, minimum, maximum);
        }

        /// <summary>
        /// Clamp an optional number to a range; NaN and infinity become null.
        /// </summary>
        /// <param name="value">The value; null passes.</param>
        /// <param name="minimum">The smallest allowed value.</param>
        /// <param name="maximum">The largest allowed value.</param>
        /// <returns>The clamped value, or null.</returns>
        public static double? Clamp(double? value, double minimum, double maximum)
        {
            if (!value.HasValue || !double.IsFinite(value.Value)) return null;
            return Math.Clamp(value.Value, minimum, maximum);
        }

        /// <summary>
        /// Clamp an integer to a range.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="minimum">The smallest allowed value.</param>
        /// <param name="maximum">The largest allowed value.</param>
        /// <returns>The clamped value.</returns>
        public static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Clamp(value, minimum, maximum);
        }

        /// <summary>
        /// Return the value when it is a defined member of its enum, otherwise the fallback. Guards against integer values
        /// in request bodies that name no member.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="fallback">The value used when it is undefined.</param>
        /// <returns>The value or the fallback.</returns>
        public static TEnum Defined<TEnum>(TEnum value, TEnum fallback) where TEnum : struct, Enum
        {
            return Enum.IsDefined(value) ? value : fallback;
        }

        #endregion
    }
}
