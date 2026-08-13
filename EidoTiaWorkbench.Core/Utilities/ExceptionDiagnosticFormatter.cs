// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Builds exhaustive exception diagnostics without collapsing wrapper exceptions.
    /// </summary>
    public static class ExceptionDiagnosticFormatter {
        private static readonly HashSet<string> CommonExceptionPropertyNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Data",
                "HelpLink",
                "HResult",
                "InnerException",
                "Message",
                "Source",
                "StackTrace",
                "TargetSite",
            };

        /// <summary>
        /// Formats an exception and every inner exception recursively for durable logs.
        /// </summary>
        /// <param name="exception">Exception to format.</param>
        /// <returns>Diagnostic text containing type, message, HRESULT, source, target, stack, data, and ToString output.</returns>
        public static string Format(Exception exception) {
            if (exception == null) {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendException(builder, exception, 0);
            return builder.ToString();
        }

        /// <summary>
        /// Appends one exception and recursively appends its inner exception chain.
        /// </summary>
        /// <param name="builder">Builder receiving diagnostic text.</param>
        /// <param name="exception">Exception to append.</param>
        /// <param name="depth">Current inner exception depth.</param>
        private static void AppendException(StringBuilder builder, Exception exception, int depth) {
            if (exception == null) {
                return;
            }

            builder.AppendLine("Exception depth: " + depth);
            builder.AppendLine("Type: " + exception.GetType().FullName);
            builder.AppendLine("Message: " + exception.Message);
            builder.AppendLine("HResult: 0x" + exception.HResult.ToString("X8"));
            builder.AppendLine("Source: " + (exception.Source ?? string.Empty));
            builder.AppendLine("TargetSite: " + FormatTargetSite(exception.TargetSite));
            builder.AppendLine("StackTrace:");
            builder.AppendLine(exception.StackTrace ?? string.Empty);
            builder.AppendLine("PublicProperties:");
            AppendPublicProperties(builder, exception);
            builder.AppendLine("Data:");
            AppendData(builder, exception.Data);
            builder.AppendLine("ToString:");
            builder.AppendLine(exception.ToString());

            if (exception is TargetInvocationException && exception.InnerException != null) {
                builder.AppendLine("TargetInvocationException.InnerException:");
            } else if (exception.InnerException != null) {
                builder.AppendLine("InnerException:");
            }

            AppendException(builder, exception.InnerException, depth + 1);
        }

        /// <summary>
        /// Formats the member that threw the exception when reflection exposes it.
        /// </summary>
        /// <param name="targetSite">Target site to format.</param>
        /// <returns>Readable method identity, or an empty string when unavailable.</returns>
        private static string FormatTargetSite(MethodBase targetSite) {
            if (targetSite == null) {
                return string.Empty;
            }

            return targetSite.DeclaringType == null
                ? targetSite.Name
                : targetSite.DeclaringType.FullName + "." + targetSite.Name;
        }

        /// <summary>
        /// Appends non-standard public exception properties exposed by Openness exception types.
        /// </summary>
        /// <param name="builder">Builder receiving diagnostic text.</param>
        /// <param name="exception">Exception whose public properties should be inspected.</param>
        private static void AppendPublicProperties(StringBuilder builder, Exception exception) {
            var appended = false;
            foreach (var property in exception.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
                if (CommonExceptionPropertyNames.Contains(property.Name) ||
                    property.GetIndexParameters().Length != 0 ||
                    !IsSimpleDiagnosticValue(property.PropertyType)) {
                    continue;
                }

                object value;
                try {
                    value = property.GetValue(exception, null);
                } catch (Exception ex) {
                    value = "<getter failed: " + ex.GetType().Name + ": " + ex.Message + ">";
                }

                builder.AppendLine("  " + property.Name + " = " + Convert.ToString(value));
                appended = true;
            }

            if (!appended) {
                builder.AppendLine("  <empty>");
            }
        }

        /// <summary>
        /// Determines whether a property value can be logged safely as a single diagnostic line.
        /// </summary>
        /// <param name="type">Property type to evaluate.</param>
        /// <returns>True when the value is a scalar diagnostic value.</returns>
        private static bool IsSimpleDiagnosticValue(Type type) {
            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(Guid) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Uri);
        }

        /// <summary>
        /// Appends exception data values without assuming serializable keys or values.
        /// </summary>
        /// <param name="builder">Builder receiving diagnostic text.</param>
        /// <param name="data">Exception data dictionary.</param>
        private static void AppendData(StringBuilder builder, IDictionary data) {
            if (data == null || data.Count == 0) {
                builder.AppendLine("  <empty>");
                return;
            }

            foreach (DictionaryEntry entry in data) {
                builder.AppendLine("  " + Convert.ToString(entry.Key) + " = " + Convert.ToString(entry.Value));
            }
        }
    }
}
