// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Argument.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Diagnostics;

    public static class Argument
    {
        /// <summary>
        /// Helper function to allow simple if check ArgumentNull exception.
        /// </summary>
        /// <typeparam name="T">parameter value to check for null.</typeparam>
        /// <param name="param">name of the parameter.</param>
        /// <example>
        /// DoSomething.<T>(this IEnumerable<T> list, Action<T> action)
        /// {
        ///     ArgumetValidator.NotNull(list,"list");
        ///     ArgumetValidator.NotNull(action,"action");
        ///
        /// </example>
        public static void NotNull(object param, string parameterName)
        {
            if (param == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        /// <summary>
        /// Helper function to allow simple if check validations and exception throwing.
        /// </summary>
        /// <param name="condition">Condition. if false, Throws ArgumentException.</param>
        /// <param name="message">Message for the ArgumentException.</param>
        /// <param name="parameterName">optional parameter name for the ArgumentException.</param>
        /// <example>
        /// DoSomething.<T>(this IEnumerable<T> list, Action<T> action)
        /// {
        ///     ArgumetValidator.IsTrue(list.Contains(s=>s==1), "Enumerable does not contain 1","list.");
        ///     ArgumetValidator.IsTrue(list.Contains(s=>s==2), "Enumerable does not contain 1");
        /// </example>
        public static void IsTrue(bool condition, string message, string parameterName = null)
        {
            if (!condition)
            {
                if (!string.IsNullOrEmpty(parameterName))
                {
                    throw new ArgumentException(message, parameterName);
                }
                else
                {
                    throw new ArgumentException(message);
                }
            }
        }

        /// <summary>
        /// Helper function to allow simple if check validations and exception throwing.
        /// </summary>
        /// <param name="condition">Condition. if true, Throws ArgumentException.</param>
        /// <param name="message">Message for the ArgumentException.</param>
        /// <param name="parameterName">optional parameter name for the ArgumentException.</param>
        /// <example>
        /// DoSomething.<T>(this IEnumerable<T> list, Action<T> action)
        /// {
        ///     ArgumetValidator.IsFalse(list.Contains(s=>s==1), "Enumerable contains 1","list.");
        /// </example>
        public static void IsFalse(bool condition, string message, string parameterName = null)
        {
            if (condition)
            {
                if (!string.IsNullOrEmpty(parameterName))
                {
                    throw new ArgumentException(message, parameterName);
                }
                else
                {
                    throw new ArgumentException(message);
                }
            }
        }

        /// <summary>
        /// Compares two types and returns if value is out of range.
        /// </summary>
        /// <typeparam name="T">numeric type (works for some other types but definitely works for numeric types.</typeparam>
        /// <param name="paramName">Name of parameter.</param>
        /// <param name="value">Value of parameter.</param>
        /// <param name="minInclusive">Minimum value, inclusive.</param>
        /// <param name="maxInclusive">Maximum value, exclusive.</param>
        /// <example>
        ///     someVal.InRange(minValue, maxValue, "SomeParam");.
        /// </example>
        public static void InRange<T>(IComparable<T> value, T maxExclusive, string paramName)
        {
            InRange<T>(value, minInclusive: default(T), maxExclusive: maxExclusive, paramName: paramName);
        }

        /// <summary>
        /// Compares two types and returns if value is out of range.
        /// </summary>
        /// <typeparam name="T">numeric type (works for some other types but definitely works for numeric types.</typeparam>
        /// <param name="paramName">Name of parameter.</param>
        /// <param name="value">Value of parameter.</param>
        /// <param name="minInclusive">Minimum value, inclusive.</param>
        /// <param name="maxExclusive">Maximum value, exclusive.</param>
        /// <example>
        ///     someVal.InRange(minValue, maxValue, "SomeParam");.
        /// </example>
        public static void InRange<T>(IComparable<T> value, T minInclusive, T maxExclusive, string paramName)
        {
            NotNull(value, paramName);

            if (value.CompareTo(minInclusive) < 0 || value.CompareTo(maxExclusive) >= 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    FormatRangeErrorMsg(value, minInclusive, maxExclusive));
            }
        }

        public static string FormatRangeErrorMsg<T>(IComparable<T> value, T minInclusive, T maxExclusive)
        {
            return string.Format("Value {2} should be in range [{0}..{1})", minInclusive, maxExclusive, value);
        }

        public static void NotNullOrEmpty(string name, string argName)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(argName + " cannot be null or empty");
            }
        }

        public static void NotNullOrWhiteSpace(string name, string argName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(argName, argName + " cannot be null or whitespace");
            }
        }

        public static void Requires<TException>(bool condition)
            where TException : Exception, new()
        {
            if (!condition)
            {
                throw new TException();
            }
        }

        public static void Requires<TException>(bool condition, string what)
            where TException : Exception, new()
        {
            if (!condition)
            {
                var type = typeof(TException);
                var ctor = type.GetConstructor(new[] { typeof(string) });
                if (ctor != null)
                {
                    var ex = ctor.Invoke(new object[] { what });
                    throw ex as TException;
                }
                else
                {
                    throw new TException();
                }
            }
        }

        [Conditional("DEBUG")]
        public static void RequiresDebugOnly<TException>(bool condition)
            where TException : Exception, new()
        {
            Requires<TException>(condition);
        }

        [Conditional("DEBUG")]
        public static void RequiresDebugOnly<TException>(bool condition, string what)
            where TException : Exception, new()
        {
            Requires<TException>(condition, what);
        }
    }
}