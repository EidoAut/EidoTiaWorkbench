// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ReflectionCache.cs
// Lightweight reflection cache used to speed up repeated property lookups. Why this exists: - TIA Openness
// APIs often require reflection to remain version-tolerant. - Reflection calls (Type.GetProperty) are
// relatively expensive. - Many services repeatedly query the same properties (e.g. "Name", "Groups",
// "Types"). This cache: - Stores PropertyInfo instances keyed by type + property name. - Uses a thread-safe
// ConcurrentDictionary. - Avoids repeated reflection cost across export/import scans.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Lightweight reflection cache used to speed up repeated property lookups.
    ///
    /// Why this exists:
    /// - TIA Openness APIs often require reflection to remain version-tolerant.
    /// - Reflection calls (Type.GetProperty) are relatively expensive.
    /// - Many services repeatedly query the same properties (e.g. "Name", "Groups", "Types").
    ///
    /// This cache:
    /// - Stores PropertyInfo instances keyed by type + property name.
    /// - Uses a thread-safe ConcurrentDictionary.
    /// - Avoids repeated reflection cost across export/import scans.
    /// </summary>
    internal static class ReflectionCache {
        /// <summary>
        /// Cached PropertyInfo instances.
        ///
        /// Key format:
        ///   "&lt;AssemblyQualifiedTypeName&gt;::&lt;PropertyName&gt;"
        ///
        /// StringComparer.Ordinal is used for deterministic, case-sensitive keys.
        /// </summary>
        private static readonly ConcurrentDictionary<string, PropertyInfo> _props =
            new ConcurrentDictionary<string, PropertyInfo>(StringComparer.Ordinal);

        /// <summary>
        /// Retrieves a public instance property from a type, using cache if available.
        /// </summary>
        /// <param name="type">Target runtime type.</param>
        /// <param name="name">Property name.</param>
        /// <returns>
        /// Cached PropertyInfo if found; otherwise null if the property does not exist.
        /// </returns>
        public static PropertyInfo GetProperty(Type type, string name) {
            if (type == null || string.IsNullOrEmpty(name)) {
                return null;
            }

            // Assembly-qualified name ensures uniqueness across loaded assemblies.
            var key = type.AssemblyQualifiedName + "::" + name;

            // GetOrAdd ensures thread-safe lazy initialization.
            return _props.GetOrAdd(
                key,
                _ => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public));
        }
    }
}
