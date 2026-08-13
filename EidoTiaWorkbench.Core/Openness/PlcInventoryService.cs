// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Discovers PLC software artifacts (UDTs, blocks, and optional tag tables) for S7-1200 and S7-1500 CPUs.
    /// </summary>
    /// <remarks>
    /// This implementation is intentionally reflection-friendly:
    /// - In Openness V21, the exact runtime types for TypeGroup (user/system) can vary by installation/project.
    /// - Some installations expose different property names for PLC tag table roots.
    ///
    /// Returned objects are plain DTOs suitable for UI binding.
    /// Call this on the Openness STA thread that owns the TiaPortal instance.
    /// </remarks>
    public sealed class PlcInventoryService {
        private readonly TiaSessionService _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlcInventoryService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session used to access the open project.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> is <see langword="null"/>.
        /// </exception>
        public PlcInventoryService(TiaSessionService session) {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Scans all PLC devices in the opened project and returns exportable items.
        /// </summary>
        /// <param name="profile">Predefined profile controlling what kinds of artifacts are included.</param>
        /// <param name="includeDbs">If false, DBs are excluded even when found.</param>
        /// <returns>Discovered export items ordered deterministically for UI display and processing.</returns>
        public List<ExportItem> ScanExportables(ExportProfile profile, bool includeDbs) {
            var project = this._session.GetOpenProjectOnSta();
            var items = new List<ExportItem>();

            foreach (var device in project.Devices) {
                foreach (var cpuItem in FindCpuDeviceItems(device)) {
                    // CpuPath format is used later to re-resolve PlcSoftware on export/import.
                    var cpuPath = device.Name + "/" + cpuItem.Name;

                    var plc = TryGetPlcSoftware(cpuItem);
                    if (plc == null) {
                        continue;
                    }

                    // Types (UDTs)
                    CollectTypes(plc.TypeGroup, cpuPath, "Types", items);

                    // Blocks (OB/FB/FC/DB)
                    CollectBlocks(plc.BlockGroup, cpuPath, "Blocks", items, includeDbs);

                    // Safety blocks (if present). Some F-CPUs expose a separate safety program with its own block group.
                    var safetyBlockGroup = TryGetSafetyBlockGroup(plc);
                    if (safetyBlockGroup != null) {
                        CollectBlocks(safetyBlockGroup, cpuPath, "SafetyBlocks", items, includeDbs);
                    }

                    // Tag tables (optional, profile-driven)
                    if (profile == ExportProfile.CorePlusTags || profile == ExportProfile.Full) {
                        var tagRoot = TryGetTagTableRoot(plc);
                        if (tagRoot != null) {
                            CollectTagTables(tagRoot, cpuPath, "TagTables", items);
                            CollectUserConstants(tagRoot, cpuPath, "TagTables", items);
                        }
                    }

                    if (profile == ExportProfile.Full) {
                        CollectWatchTables(TryGetPropertyValue(plc, "WatchAndForceTableGroup"), cpuPath, "WatchTables", items);
                        CollectForceTables(TryGetPropertyValue(plc, "WatchAndForceTableGroup"), cpuPath, "ForceTables", items);
                        CollectTechnologyObjects(TryGetPropertyValue(plc, "TechnologicalObjectGroup"), cpuPath, "TechnologyObjects", items);
                        CollectUnsupported(TryGetPropertyValue(plc, "ExternalSourceGroup"), cpuPath, "ExternalSources", ExportKind.PlcExternalSource, "ExternalSources", items);
                        CollectUnsupported(TryGetPropertyValue(plc, "PlcAlarmTextlistGroup"), cpuPath, "AlarmTextLists", ExportKind.PlcAlarmTextList, "PlcAlarmUserTextlists", items);
                    }
                }
            }

            // Return a stable ordering for UI and deterministic processing.
            return items
                .OrderBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Kind)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // -------------------------
        // CPU discovery
        // -------------------------

        /// <summary>
        /// Finds all device items that expose a SoftwareContainer (typically CPUs).
        /// Uses a stack-based traversal to avoid recursion.
        /// </summary>
        /// <param name="device">TIA hardware device whose nested items should be traversed.</param>
        /// <returns>Device items that expose a software container and may represent PLC CPUs.</returns>
        private static IEnumerable<DeviceItem> FindCpuDeviceItems(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var di = stack.Pop();

                foreach (var child in di.DeviceItems) {
                    stack.Push(child);
                }

                // CPU detection heuristic: any device item that has a software container.
                if (di.GetService<SoftwareContainer>() != null) {
                    yield return di;
                }
            }
        }

        /// <summary>
        /// Returns PlcSoftware from a CPU device item (or null when not a PLC CPU).
        /// </summary>
        /// <param name="cpuDeviceItem">Device item that may own PLC software.</param>
        /// <returns>The PLC software instance, or <see langword="null"/> when the item is not a PLC CPU.</returns>
        private static PlcSoftware TryGetPlcSoftware(DeviceItem cpuDeviceItem) {
            var swContainer = cpuDeviceItem.GetService<SoftwareContainer>();
            if (swContainer != null && swContainer.Software is PlcSoftware) {
                return (PlcSoftware)swContainer.Software;
            }

            return null;
        }

        /// <summary>
        /// Best-effort discovery of a safety program block group via reflection.
        /// On standard CPUs this returns null.
        /// </summary>
        /// <param name="plc">PLC software instance to inspect for safety program properties.</param>
        /// <returns>The safety block group, or <see langword="null"/> when no safety program is available.</returns>
        private static PlcBlockGroup TryGetSafetyBlockGroup(PlcSoftware plc) {
            if (plc == null) {
                return null;
            }

            try {
                // Property name may vary across Openness versions.
                // We try common ones and then look for a BlockGroup on the safety program object.
                var spProp = plc.GetType().GetProperty("SafetyProgram", BindingFlags.Instance | BindingFlags.Public)
                             ?? plc.GetType().GetProperty("PlcSafetyProgram", BindingFlags.Instance | BindingFlags.Public);

                var sp = spProp?.GetValue(plc, null);
                if (sp == null) {
                    return null;
                }

                var bgProp = sp.GetType().GetProperty("BlockGroup", BindingFlags.Instance | BindingFlags.Public);
                if (bgProp?.GetValue(sp, null) is PlcBlockGroup bg) {
                    return bg;
                }
            } catch {
                // Best-effort only: absence/reflection failures should not break inventory.
            }

            return null;
        }

        // -------------------------
        // Types (UDTs) traversal (reflection)
        // -------------------------

        /// <summary>
        /// Collects UDTs (PlcType) from the TypeGroup hierarchy using reflection.
        /// </summary>
        /// <param name="typeGroupRoot">Root type group exposed by the PLC software.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used only while traversing the type hierarchy.</param>
        /// <param name="items">Mutable scan result list receiving discovered UDT items.</param>
        private static void CollectTypes(object typeGroupRoot, string cpuPath, string prefix, List<ExportItem> items) {
            if (typeGroupRoot == null) {
                return;
            }

            TraverseGenericGroup(
                root: typeGroupRoot,
                groupPath: prefix, // Root name is implicit; keep paths relative to root for stable re-resolution.
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, "Types"),
                addItem: (groupPath, objName) => {
                    items.Add(new ExportItem(
                        cpuPath,
                        TrimRootPath(groupPath, "Types"),
                        objName,
                        ExportKind.Udt,
                        BuildLogicalKey(cpuPath, ExportKind.Udt, groupPath, objName)));
                });
        }

        // -------------------------
        // Blocks traversal (strongly typed)
        // -------------------------

        /// <summary>
        /// Collects blocks from a PlcBlockGroup tree (including nested groups).
        /// </summary>
        /// <param name="group">Root or nested block group to scan.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used while trimming Openness root folders.</param>
        /// <param name="items">Mutable scan result list receiving discovered block items.</param>
        /// <param name="includeDbs">Whether DBs should be included in the scan result.</param>
        private static void CollectBlocks(PlcBlockGroup group, string cpuPath, string prefix, List<ExportItem> items, bool includeDbs) {
            if (group == null) {
                return;
            }

            // IMPORTANT:
            // Do NOT include the root group's display name (e.g. "Program blocks") in stored paths.
            // In Openness, the root group is implicit; including its name makes later resolution fail.
            var basePath = prefix;

            foreach (var b in group.Blocks) {
                var kind = MapBlockKindByRuntimeType(b, includeDbs, out bool include);
                if (!include) {
                    continue;
                }

                items.Add(new ExportItem(
                    cpuPath,
                    TrimRootPath(basePath, prefix),
                    b.Name,
                    kind,
                    BuildLogicalKey(cpuPath, kind, basePath, b.Name)));
            }

            foreach (var sub in group.Groups) {
                var subPath = basePath + "/" + sub.Name;
                CollectBlocksRecursive(sub, cpuPath, prefix, subPath, items, includeDbs);
            }
        }

        /// <summary>
        /// Recursive helper to collect blocks from nested groups.
        /// </summary>
        /// <param name="group">Current nested block group being scanned.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used while trimming Openness root folders.</param>
        /// <param name="currentPath">Current logical group path including nested block groups.</param>
        /// <param name="items">Mutable scan result list receiving discovered block items.</param>
        /// <param name="includeDbs">Whether DBs should be included in the scan result.</param>
        private static void CollectBlocksRecursive(PlcBlockGroup group, string cpuPath, string prefix, string currentPath, List<ExportItem> items, bool includeDbs) {
            if (group == null) {
                return;
            }

            foreach (var b in group.Blocks) {
                var kind = MapBlockKindByRuntimeType(b, includeDbs, out bool include);
                if (!include) {
                    continue;
                }

                items.Add(new ExportItem(
                    cpuPath,
                    TrimRootPath(currentPath, prefix),
                    b.Name,
                    kind,
                    BuildLogicalKey(cpuPath, kind, currentPath, b.Name)));
            }

            foreach (var sub in group.Groups) {
                var subPath = currentPath + "/" + sub.Name;
                CollectBlocksRecursive(sub, cpuPath, prefix, subPath, items, includeDbs);
            }
        }

        /// <summary>
        /// Classifies blocks without taking a compile-time dependency on concrete Openness derived types.
        /// Some installations/API variants do not expose all derived classes in referenced assemblies,
        /// so we classify by runtime type name and fall back to common naming conventions.
        /// </summary>
        /// <param name="block">PLC block to classify.</param>
        /// <param name="includeDbs">Whether DBs are allowed in the scan result.</param>
        /// <param name="include">Outputs whether the classified block should be included.</param>
        /// <returns>The export kind inferred from runtime type name or Siemens block naming convention.</returns>
        private static ExportKind MapBlockKindByRuntimeType(PlcBlock block, bool includeDbs, out bool include) {
            include = true;

            // 1) Prefer runtime type name (works across versions even when derived types aren't referenced).
            var typeName = block?.GetType().Name ?? string.Empty;

            // Order matters: FunctionBlock contains "Function".
            if (typeName.IndexOf("OrganizationBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("OB", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.Ob;
            }

            if (typeName.IndexOf("FunctionBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("FB", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.Fb;
            }

            if (typeName.IndexOf("DataBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("InstanceDB", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("DB", StringComparison.OrdinalIgnoreCase)) {
                if (!includeDbs) {
                    include = false;
                    return ExportKind.Db;
                }

                return ExportKind.Db;
            }

            // Plain function (FC) is often named "Function".
            if (typeName.Equals("Function", StringComparison.OrdinalIgnoreCase)
                || (typeName.IndexOf("Function", StringComparison.OrdinalIgnoreCase) >= 0
                    && typeName.IndexOf("FunctionBlock", StringComparison.OrdinalIgnoreCase) < 0)) {
                return ExportKind.Fc;
            }

            // 2) Fallback: common Siemens naming convention (OB1/FC10/FB2/DB3...).
            var n = (block?.Name ?? string.Empty).Trim();
            if (n.Length >= 2) {
                var prefix = new string(n.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
                if (prefix == "OB") {
                    return ExportKind.Ob;
                }

                if (prefix == "FB") {
                    return ExportKind.Fb;
                }

                if (prefix == "FC") {
                    return ExportKind.Fc;
                }

                if (prefix == "DB") {
                    if (!includeDbs) {
                        include = false;
                        return ExportKind.Db;
                    }

                    return ExportKind.Db;
                }
            }

            // Unknown/other block kinds (technology/safety variants): include them so they show up.
            include = true;
            return ExportKind.Fc;
        }

        // -------------------------
        // Tag tables traversal (reflection)
        // -------------------------

        /// <summary>
        /// Attempts to locate the tag table root group in a version-tolerant way.
        /// Different Openness versions use different property names.
        /// </summary>
        /// <param name="plc">PLC software instance to inspect for tag table root properties.</param>
        /// <returns>The tag table root group object, or <see langword="null"/> when unavailable.</returns>
        private static object TryGetTagTableRoot(PlcSoftware plc) {
            var prop = plc.GetType().GetProperty("PlcTagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) {
                    return val;
                }
            }

            prop = plc.GetType().GetProperty("TagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) {
                    return val;
                }
            }

            return null;
        }

        /// <summary>
        /// Collects PLC tag tables from the tag table group hierarchy using reflection.
        /// </summary>
        /// <param name="tagRoot">Root tag table group exposed by the PLC software.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used only while traversing tag table groups.</param>
        /// <param name="items">Mutable scan result list receiving discovered tag table items.</param>
        private static void CollectTagTables(object tagRoot, string cpuPath, string prefix, List<ExportItem> items) {
            if (tagRoot == null) {
                return;
            }

            TraverseGenericGroup(
                root: tagRoot,
                groupPath: prefix, // Root name is implicit; keep paths relative to root for stable re-resolution.
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, "TagTables"),
                addItem: (groupPath, objName) => {
                    items.Add(new ExportItem(
                        cpuPath,
                        TrimRootPath(groupPath, "TagTables"),
                        objName,
                        ExportKind.PlcTagTable,
                        BuildLogicalKey(cpuPath, ExportKind.PlcTagTable, groupPath, objName)));
                });
        }

        /// <summary>
        /// Collects PLC user constants from each PLC tag table.
        /// </summary>
        /// <param name="tagRoot">Root tag table group exposed by the PLC software.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used while traversing tag tables.</param>
        /// <param name="items">Mutable scan result list receiving discovered constant items.</param>
        private static void CollectUserConstants(object tagRoot, string cpuPath, string prefix, List<ExportItem> items) {
            TraverseGenericGroup(
                root: tagRoot,
                groupPath: prefix,
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, "TagTables"),
                addItem: (groupPath, tableName) => {
                    var table = ResolveByNameInGroup(tagRoot, groupPath, "TagTables", tableName);
                    foreach (var constant in GetChildren(table, "UserConstants")) {
                        var name = GetName(constant);
                        if (string.IsNullOrWhiteSpace(name)) {
                            continue;
                        }

                        var constantPath = TrimRootPath(groupPath, prefix);
                        if (!string.IsNullOrWhiteSpace(tableName)) {
                            constantPath = string.IsNullOrWhiteSpace(constantPath) ? tableName : constantPath + "/" + tableName;
                        }

                        items.Add(new ExportItem(
                            cpuPath,
                            constantPath,
                            name,
                            ExportKind.PlcUserConstant,
                            BuildLogicalKey(cpuPath, ExportKind.PlcUserConstant, constantPath, name)));
                    }
                });
        }

        /// <summary>
        /// Collects PLC watch tables from the watch and force table hierarchy.
        /// </summary>
        /// <param name="root">Root watch and force table group.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used during traversal.</param>
        /// <param name="items">Mutable scan result list receiving discovered watch table items.</param>
        private static void CollectWatchTables(object root, string cpuPath, string prefix, List<ExportItem> items) {
            CollectGenericExportable(root, cpuPath, prefix, "WatchTables", ExportKind.PlcWatchTable, items);
        }

        /// <summary>
        /// Collects PLC force tables from the watch and force table hierarchy.
        /// </summary>
        /// <param name="root">Root watch and force table group.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used during traversal.</param>
        /// <param name="items">Mutable scan result list receiving discovered force table items.</param>
        private static void CollectForceTables(object root, string cpuPath, string prefix, List<ExportItem> items) {
            CollectGenericExportable(root, cpuPath, prefix, "ForceTables", ExportKind.PlcForceTable, items);
        }

        /// <summary>
        /// Collects PLC technology objects represented by technological instance DBs.
        /// </summary>
        /// <param name="root">Root technology object group.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used during traversal.</param>
        /// <param name="items">Mutable scan result list receiving discovered technology object items.</param>
        private static void CollectTechnologyObjects(object root, string cpuPath, string prefix, List<ExportItem> items) {
            CollectGenericExportable(root, cpuPath, prefix, "TechnologicalObjects", ExportKind.TechnologicalObject, items);
        }

        /// <summary>
        /// Collects export-capable items from a generic Openness group hierarchy.
        /// </summary>
        /// <param name="root">Root group object.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used during traversal.</param>
        /// <param name="itemProperty">Public collection property that contains artifacts.</param>
        /// <param name="kind">Kind assigned to discovered items.</param>
        /// <param name="items">Mutable scan result list receiving discovered items.</param>
        private static void CollectGenericExportable(object root, string cpuPath, string prefix, string itemProperty, ExportKind kind, List<ExportItem> items) {
            if (root == null) {
                return;
            }

            TraverseGenericGroup(
                root: root,
                groupPath: prefix,
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, itemProperty),
                addItem: (groupPath, objName) => {
                    var relativeGroup = TrimRootPath(groupPath, prefix);
                    items.Add(new ExportItem(
                        cpuPath,
                        relativeGroup,
                        objName,
                        kind,
                        BuildLogicalKey(cpuPath, kind, relativeGroup, objName)));
                });
        }

        /// <summary>
        /// Collects discoverable but unsupported items so the UI can explain the limitation.
        /// </summary>
        /// <param name="root">Root group object.</param>
        /// <param name="cpuPath">Stable CPU path stored in each discovered artifact.</param>
        /// <param name="prefix">Logical root prefix used during traversal.</param>
        /// <param name="kind">Unsupported artifact kind.</param>
        /// <param name="itemProperty">Public collection property that contains artifacts.</param>
        /// <param name="items">Mutable scan result list receiving discovered items.</param>
        private static void CollectUnsupported(object root, string cpuPath, string prefix, ExportKind kind, string itemProperty, List<ExportItem> items) {
            var descriptor = ArtifactCatalog.Get(kind);
            if (root == null) {
                return;
            }

            TraverseGenericGroup(
                root: root,
                groupPath: prefix,
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, itemProperty),
                addItem: (groupPath, objName) => {
                    var relativeGroup = TrimRootPath(groupPath, prefix);
                    items.Add(new ExportItem(
                        cpuPath,
                        relativeGroup,
                        objName,
                        kind,
                        BuildLogicalKey(cpuPath, kind, relativeGroup, objName),
                        null,
                        false,
                        false,
                        descriptor.Limitations,
                        null,
                        null));
                });
        }

        // -------------------------
        // Generic traversal helpers
        // -------------------------

        /// <summary>
        /// Traverses an Openness group tree using reflection-friendly accessors.
        /// </summary>
        /// <param name="root">Root group object used as the traversal starting point.</param>
        /// <param name="groupPath">Logical group path associated with the root node.</param>
        /// <param name="getChildren">Accessor returning nested child groups for a group object.</param>
        /// <param name="getItems">Accessor returning artifact objects for a group object.</param>
        /// <param name="addItem">Callback that receives group path and object name for each discovered artifact.</param>
        private static void TraverseGenericGroup(
            object root,
            string groupPath,
            Func<object, IEnumerable> getChildren,
            Func<object, IEnumerable> getItems,
            Action<string, string> addItem) {
            var stack = new Stack<Tuple<object, string>>();
            stack.Push(Tuple.Create(root, groupPath));

            while (stack.Count > 0) {
                var node = stack.Pop();
                var g = node.Item1;
                var path = node.Item2;

                // Items (Types/TagTables/etc.)
                foreach (var it in getItems(g)) {
                    var name = GetName(it);
                    if (!string.IsNullOrWhiteSpace(name)) {
                        addItem(path, name);
                    }
                }

                // Child groups
                foreach (var child in getChildren(g)) {
                    var childName = GetName(child) ?? "Group";
                    stack.Push(Tuple.Create(child, path + "/" + childName));
                }
            }
        }

        /// <summary>
        /// Reads a child collection property from an object using reflection.
        /// Returns an empty enumerable if the property is missing.
        /// </summary>
        /// <param name="obj">Openness object that may expose the requested child collection.</param>
        /// <param name="propertyName">Public property name to read.</param>
        /// <returns>The collection value as <see cref="IEnumerable"/>, or an empty collection when unavailable.</returns>
        private static IEnumerable GetChildren(object obj, string propertyName) {
            if (obj == null) {
                return Array.Empty<object>();
            }

            var p = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (p == null) {
                return Array.Empty<object>();
            }

            var val = p.GetValue(obj, null);
            return val as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Reads one public property value by name.
        /// </summary>
        /// <param name="obj">Object that may expose the property.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The property value, or <see langword="null"/> when unavailable.</returns>
        private static object TryGetPropertyValue(object obj, string propertyName) {
            if (obj == null) {
                return null;
            }

            var p = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return p == null ? null : p.GetValue(obj, null);
        }

        /// <summary>
        /// Resolves an item by name in a generic group path and item collection.
        /// </summary>
        /// <param name="root">Root group object.</param>
        /// <param name="groupPath">Group path captured during traversal.</param>
        /// <param name="itemProperty">Item collection property name.</param>
        /// <param name="name">Item name to find.</param>
        /// <returns>The resolved item, or <see langword="null"/> when not found.</returns>
        private static object ResolveByNameInGroup(object root, string groupPath, string itemProperty, string name) {
            var group = ResolveGenericGroup(root, TrimRootPath(groupPath, string.Empty));
            if (group == null) {
                return null;
            }

            foreach (var item in GetChildren(group, itemProperty)) {
                if (string.Equals(GetName(item), name, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a generic Openness group by path.
        /// </summary>
        /// <param name="root">Root group object.</param>
        /// <param name="groupPath">Slash-delimited group path.</param>
        /// <returns>The resolved group, or <see langword="null"/> when missing.</returns>
        private static object ResolveGenericGroup(object root, string groupPath) {
            if (root == null || string.IsNullOrWhiteSpace(groupPath)) {
                return root;
            }

            var current = root;
            foreach (var part in groupPath.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                var next = GetChildren(current, "Groups")
                    .Cast<object>()
                    .FirstOrDefault(g => string.Equals(GetName(g), part, StringComparison.OrdinalIgnoreCase));
                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Reads the "Name" property from an Openness object via reflection.
        /// </summary>
        /// <param name="obj">Openness object that may expose a Name property.</param>
        /// <returns>The object name, or <see langword="null"/> when no readable name is available.</returns>
        private static string GetName(object obj) {
            if (obj == null) {
                return null;
            }

            var p = obj.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            return p == null ? null : p.GetValue(obj, null) as string;
        }

        /// <summary>
        /// Trims a known root token from a stored group path (root is considered implicit).
        /// </summary>
        /// <param name="full">Full logical group path collected during traversal.</param>
        /// <param name="root">Root token to remove because Openness treats it as implicit.</param>
        /// <returns>The group path relative to the real Openness root.</returns>
        private static string TrimRootPath(string full, string root) {
            if (string.IsNullOrWhiteSpace(full)) {
                return string.Empty;
            }

            var normalized = full.Replace("\\", "/").Trim('/');

            if (string.IsNullOrWhiteSpace(root)) {
                return normalized;
            }

            var r = root.Replace("\\", "/").Trim('/');

            // Root group name is implicit in Openness. Treat exact-match as empty.
            if (string.Equals(normalized, r, StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }

            var prefix = r + "/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return normalized.Substring(prefix.Length);
            }

            return normalized;
        }

        /// <summary>
        /// Builds a stable logical key that can be used to restore checked state in the UI.
        /// </summary>
        /// <param name="cpuPath">Stable CPU path identifying the PLC target.</param>
        /// <param name="kind">Artifact kind included in the key.</param>
        /// <param name="groupPath">Logical group path included in the key.</param>
        /// <param name="name">Artifact name included in the key.</param>
        /// <returns>A stable logical key using forward slashes for hierarchy separators.</returns>
        private static string BuildLogicalKey(string cpuPath, ExportKind kind, string groupPath, string name) {
            // Use forward slashes to keep keys stable across OS separator differences.
            return cpuPath + "||" + kind + "||" + groupPath.Replace("\\", "/") + "||" + name;
        }
    }
}
