// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Discovers classic WinCC HMI screen artifacts from the open TIA Portal project.
    /// </summary>
    /// <remarks>
    /// The service uses the public TIA Portal Openness V21 WinCC API for classic HMI targets.
    /// It intentionally does not use the Unified API because TP900 Comfort devices are classic
    /// WinCC panels, not Unified panels. Calls must run on the owning Openness STA thread.
    /// </remarks>
    public sealed class HmiInventoryService {
        private readonly TiaSessionService _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="HmiInventoryService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session used to access the open project.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is <see langword="null"/>.</exception>
        public HmiInventoryService(TiaSessionService session) {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Scans all classic HMI targets in the opened project and returns exportable screen items.
        /// </summary>
        /// <returns>Discovered HMI screen items ordered by target, folder path, and screen name.</returns>
        public List<ExportItem> ScanScreens() {
            return this.ScanExportables();
        }

        /// <summary>
        /// Scans all classic HMI targets in the opened project and returns all supported classic HMI artifacts.
        /// </summary>
        /// <returns>Discovered HMI items ordered by target, kind, folder path, and name.</returns>
        public List<ExportItem> ScanExportables() {
            var project = this._session.GetOpenProjectOnSta();
            var items = new List<ExportItem>();

            foreach (var device in project.Devices) {
                foreach (var target in FindHmiTargets(device)) {
                    var targetPath = device.Name + "/" + target.DeviceItem.Name;
                    var targetType = BuildTargetType(device, target.DeviceItem, target.HmiTarget);

                    CollectFolderArtifacts(target.HmiTarget.ScreenFolder, targetPath, string.Empty, targetType, ExportKind.HmiScreen, "Screens", "Folders", items);
                    CollectFolderArtifacts(target.HmiTarget.ScreenTemplateFolder, targetPath, string.Empty, targetType, ExportKind.HmiTemplate, "ScreenTemplates", "Folders", items);
                    CollectFolderArtifacts(target.HmiTarget.ScreenPopupFolder, targetPath, string.Empty, targetType, ExportKind.HmiPopup, "ScreenPopups", "Folders", items);
                    CollectFlatArtifacts(target.HmiTarget.ScreenSlideinFolder, targetPath, string.Empty, targetType, ExportKind.HmiSlideIn, "ScreenSlideins", items);
                    CollectFolderArtifacts(target.HmiTarget.TagFolder, targetPath, string.Empty, targetType, ExportKind.HmiTagTable, "TagTables", "Folders", items);
                    CollectCompositionArtifacts(target.HmiTarget.Connections, targetPath, string.Empty, targetType, ExportKind.HmiConnection, items);
                    CollectCompositionArtifacts(target.HmiTarget.TextLists, targetPath, string.Empty, targetType, ExportKind.HmiTextList, items);
                    CollectCompositionArtifacts(target.HmiTarget.GraphicLists, targetPath, string.Empty, targetType, ExportKind.HmiGraphicList, items);
                    CollectCompositionArtifacts(target.HmiTarget.Cycles, targetPath, string.Empty, targetType, ExportKind.HmiCycle, items);
                    CollectFolderArtifacts(target.HmiTarget.VBScriptFolder, targetPath, string.Empty, targetType, ExportKind.HmiVBScript, "VBScripts", "Folders", items);
                    CollectSingletonArtifact(target.HmiTarget.ScreenGlobalElements, targetPath, targetType, ExportKind.HmiGlobalElements, "Global elements", items);
                    CollectSingletonArtifact(target.HmiTarget.ScreenOverview, targetPath, targetType, ExportKind.HmiScreenOverview, "Screen overview", items);
                }
            }

            return items
                .OrderBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Kind)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Recursively collects normal classic HMI screens from a screen folder.
        /// </summary>
        /// <param name="folder">Screen folder to scan.</param>
        /// <param name="targetPath">Stable HMI target path used in repository layout.</param>
        /// <param name="folderPath">Relative HMI screen folder path.</param>
        /// <param name="targetType">Human-readable target type information.</param>
        /// <param name="items">Destination list for discovered screen items.</param>
        private static void CollectScreens(
            ScreenFolder folder,
            string targetPath,
            string folderPath,
            string targetType,
            List<ExportItem> items) {
            if (folder == null) {
                return;
            }

            foreach (var screen in folder.Screens) {
                items.Add(new ExportItem(
                    targetPath,
                    folderPath,
                    screen.Name,
                    ExportKind.HmiScreen,
                    BuildLogicalKey(targetPath, folderPath, screen.Name),
                    targetType));
            }

            foreach (var child in folder.Folders) {
                var childPath = CombineGroupPath(folderPath, child.Name);
                CollectScreens(child, targetPath, childPath, targetType, items);
            }
        }

        /// <summary>
        /// Collects classic HMI artifacts from a folder hierarchy using reflection-compatible property names.
        /// </summary>
        /// <param name="folder">Root folder object.</param>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="folderPath">Current relative folder path.</param>
        /// <param name="targetType">Human-readable target type.</param>
        /// <param name="kind">Artifact kind assigned to collected items.</param>
        /// <param name="itemProperty">Collection property containing artifacts.</param>
        /// <param name="folderProperty">Collection property containing child folders.</param>
        /// <param name="items">Destination list for discovered items.</param>
        private static void CollectFolderArtifacts(
            object folder,
            string targetPath,
            string folderPath,
            string targetType,
            ExportKind kind,
            string itemProperty,
            string folderProperty,
            List<ExportItem> items) {
            if (folder == null) {
                return;
            }

            foreach (var artifact in GetChildren(folder, itemProperty)) {
                var name = GetName(artifact);
                if (!string.IsNullOrWhiteSpace(name)) {
                    AddItem(items, targetPath, folderPath, name, kind, targetType, artifact);
                }
            }

            foreach (var child in GetChildren(folder, folderProperty)) {
                var childPath = CombineGroupPath(folderPath, GetName(child));
                CollectFolderArtifacts(child, targetPath, childPath, targetType, kind, itemProperty, folderProperty, items);
            }
        }

        /// <summary>
        /// Collects classic HMI artifacts from a flat composition exposed under a root object.
        /// </summary>
        /// <param name="root">Root object that exposes an artifact collection.</param>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="folderPath">Relative folder path stored in the item.</param>
        /// <param name="targetType">Human-readable target type.</param>
        /// <param name="kind">Artifact kind assigned to collected items.</param>
        /// <param name="itemProperty">Collection property containing artifacts.</param>
        /// <param name="items">Destination list for discovered items.</param>
        private static void CollectFlatArtifacts(
            object root,
            string targetPath,
            string folderPath,
            string targetType,
            ExportKind kind,
            string itemProperty,
            List<ExportItem> items) {
            if (root == null) {
                return;
            }

            foreach (var artifact in GetChildren(root, itemProperty)) {
                var name = GetName(artifact);
                if (!string.IsNullOrWhiteSpace(name)) {
                    AddItem(items, targetPath, folderPath, name, kind, targetType, artifact);
                }
            }
        }

        /// <summary>
        /// Collects classic HMI artifacts from an enumerable composition.
        /// </summary>
        /// <param name="composition">Openness composition to enumerate.</param>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="folderPath">Relative folder path stored in the item.</param>
        /// <param name="targetType">Human-readable target type.</param>
        /// <param name="kind">Artifact kind assigned to collected items.</param>
        /// <param name="items">Destination list for discovered items.</param>
        private static void CollectCompositionArtifacts(
            IEnumerable composition,
            string targetPath,
            string folderPath,
            string targetType,
            ExportKind kind,
            List<ExportItem> items) {
            if (composition == null) {
                return;
            }

            foreach (var artifact in composition) {
                var name = GetName(artifact);
                if (!string.IsNullOrWhiteSpace(name)) {
                    AddItem(items, targetPath, folderPath, name, kind, targetType, artifact);
                }
            }
        }

        /// <summary>
        /// Adds a singleton HMI artifact such as global screen elements when the object exists.
        /// </summary>
        /// <param name="artifact">Openness singleton object.</param>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="targetType">Human-readable target type.</param>
        /// <param name="kind">Artifact kind assigned to the item.</param>
        /// <param name="fallbackName">Name used when the Openness object has no Name property.</param>
        /// <param name="items">Destination list for discovered items.</param>
        private static void CollectSingletonArtifact(
            object artifact,
            string targetPath,
            string targetType,
            ExportKind kind,
            string fallbackName,
            List<ExportItem> items) {
            if (artifact == null) {
                return;
            }

            AddItem(items, targetPath, string.Empty, GetName(artifact) ?? fallbackName, kind, targetType, artifact);
        }

        /// <summary>
        /// Adds one classic HMI item using catalog capabilities.
        /// </summary>
        /// <param name="items">Destination list for discovered items.</param>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="folderPath">Relative folder path.</param>
        /// <param name="name">Artifact name.</param>
        /// <param name="kind">Artifact kind.</param>
        /// <param name="targetType">Human-readable target type.</param>
        /// <param name="artifact">Resolved Openness artifact used for item-level capability flags.</param>
        private static void AddItem(
            List<ExportItem> items,
            string targetPath,
            string folderPath,
            string name,
            ExportKind kind,
            string targetType,
            object artifact) {
            var descriptor = ArtifactCatalog.Get(kind);
            var canExport = descriptor.CanExport;
            var canImport = descriptor.CanImport;
            var unavailableReason = descriptor.Limitations;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if ((kind == ExportKind.HmiCycle || kind == ExportKind.HmiTagTable) && TryGetBooleanProperty(artifact, "IsSystemObject")) {
                canExport = false;
                canImport = false;
                unavailableReason = "System HMI artifacts are resolved as dependencies and are not exported as user artifacts.";
                metadata["IsSystemObject"] = "true";
            }

            items.Add(new ExportItem(
                targetPath,
                folderPath,
                name,
                kind,
                BuildLogicalKey(targetPath, kind, folderPath, name),
                targetType,
                canExport,
                canImport,
                unavailableReason,
                null,
                metadata));
        }

        /// <summary>
        /// Builds the stable logical key used to match an HMI screen across scans.
        /// </summary>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="folderPath">Relative HMI screen folder path.</param>
        /// <param name="screenName">Screen name inside the folder.</param>
        /// <returns>The logical key for the screen.</returns>
        private static string BuildLogicalKey(string targetPath, string folderPath, string screenName) {
            return targetPath + "||" + ExportKind.HmiScreen + "||" + folderPath.Replace("\\", "/") + "||" + screenName;
        }

        /// <summary>
        /// Builds the stable logical key used to match a classic HMI artifact across scans.
        /// </summary>
        /// <param name="targetPath">Stable HMI target path.</param>
        /// <param name="kind">Artifact kind included in the key.</param>
        /// <param name="folderPath">Relative HMI folder path.</param>
        /// <param name="name">Artifact name inside the folder or composition.</param>
        /// <returns>The logical key for the artifact.</returns>
        private static string BuildLogicalKey(string targetPath, ExportKind kind, string folderPath, string name) {
            return targetPath + "||" + kind + "||" + (folderPath ?? string.Empty).Replace("\\", "/") + "||" + name;
        }

        /// <summary>
        /// Builds target type metadata without requiring device-family-specific API calls.
        /// </summary>
        /// <param name="device">Parent device containing the HMI target.</param>
        /// <param name="deviceItem">Device item that exposes the HMI software container.</param>
        /// <param name="target">Classic WinCC HMI target.</param>
        /// <returns>A display string describing the target type.</returns>
        private static string BuildTargetType(Device device, DeviceItem deviceItem, HmiTarget target) {
            var article = TryGetAttribute(deviceItem, "ArticleNumber");
            var typeIdentifier = TryGetAttribute(deviceItem, "TypeIdentifier")
                                 ?? TryGetAttribute(device, "TypeIdentifier");

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(typeIdentifier)) {
                parts.Add(typeIdentifier);
            }

            if (!string.IsNullOrWhiteSpace(article)) {
                parts.Add(article);
            }

            parts.Add(target.GetType().FullName);
            return string.Join(" | ", parts.ToArray());
        }

        /// <summary>
        /// Finds classic HMI targets by walking all device items under a TIA device.
        /// </summary>
        /// <param name="device">Device whose nested items should be inspected.</param>
        /// <returns>The classic HMI targets discovered under the device.</returns>
        private static IEnumerable<HmiTargetReference> FindHmiTargets(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var item = stack.Pop();

                foreach (var child in item.DeviceItems) {
                    stack.Push(child);
                }

                var container = item.GetService<SoftwareContainer>();
                if (container != null && container.Software is HmiTarget) {
                    yield return new HmiTargetReference(item, (HmiTarget)container.Software);
                }
            }
        }

        /// <summary>
        /// Appends a child HMI screen folder name to an existing group path.
        /// </summary>
        /// <param name="currentPath">Current slash-separated group path.</param>
        /// <param name="childName">Child folder name to append.</param>
        /// <returns>The combined group path.</returns>
        private static string CombineGroupPath(string currentPath, string childName) {
            if (string.IsNullOrWhiteSpace(currentPath)) {
                return childName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(childName)) {
                return currentPath;
            }

            return currentPath + "/" + childName;
        }

        /// <summary>
        /// Reads an optional Openness attribute when the engineering object exposes it.
        /// </summary>
        /// <param name="engineeringObject">Openness engineering object to inspect.</param>
        /// <param name="attributeName">Attribute name to read.</param>
        /// <returns>The attribute value converted to text, or <see langword="null"/> when unavailable.</returns>
        private static string TryGetAttribute(object engineeringObject, string attributeName) {
            try {
                if (engineeringObject == null) {
                    return null;
                }

                var method = engineeringObject.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
                if (method == null) {
                    return null;
                }

                var value = method.Invoke(engineeringObject, new object[] { attributeName });
                return value == null ? null : Convert.ToString(value);
            } catch {
                // Attribute availability varies by device family and installed Openness components.
                return null;
            }
        }

        /// <summary>
        /// Reads a child collection property from an Openness object.
        /// </summary>
        /// <param name="obj">Object that may expose the collection.</param>
        /// <param name="propertyName">Public collection property name.</param>
        /// <returns>The collection value, or an empty enumerable when unavailable.</returns>
        private static IEnumerable GetChildren(object obj, string propertyName) {
            if (obj == null) {
                return Array.Empty<object>();
            }

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null) {
                return Array.Empty<object>();
            }

            var value = property.GetValue(obj, null);
            return value as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Reads the public Name property from an Openness object.
        /// </summary>
        /// <param name="obj">Object whose name should be read.</param>
        /// <returns>The object name, or <see langword="null"/> when no name is exposed.</returns>
        private static string GetName(object obj) {
            if (obj == null) {
                return null;
            }

            var property = obj.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            return property == null ? null : property.GetValue(obj, null) as string;
        }

        /// <summary>
        /// Reads an optional boolean property from an Openness object.
        /// </summary>
        /// <param name="obj">Object that may expose the requested property.</param>
        /// <param name="propertyName">Public boolean property name to read.</param>
        /// <returns><see langword="true"/> only when the property exists and is true.</returns>
        private static bool TryGetBooleanProperty(object obj, string propertyName) {
            if (obj == null) {
                return false;
            }

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool)) {
                return false;
            }

            return (bool)property.GetValue(obj, null);
        }

        /// <summary>
        /// Groups the device item and classic HMI target found during device traversal.
        /// </summary>
        private sealed class HmiTargetReference {
            /// <summary>
            /// Initializes a new instance of the <see cref="HmiTargetReference"/> class.
            /// </summary>
            /// <param name="deviceItem">Device item that owns the software container.</param>
            /// <param name="hmiTarget">Classic WinCC HMI target software.</param>
            public HmiTargetReference(DeviceItem deviceItem, HmiTarget hmiTarget) {
                this.DeviceItem = deviceItem;
                this.HmiTarget = hmiTarget;
            }

            /// <summary>
            /// Gets the device item that owns the HMI target.
            /// </summary>
            public DeviceItem DeviceItem {
                get;
            }

            /// <summary>
            /// Gets the classic WinCC HMI target.
            /// </summary>
            public HmiTarget HmiTarget {
                get;
            }
        }
    }
}
