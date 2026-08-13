// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;
using System.Linq;

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Provides the central artifact capability catalog for Openness V21.
    /// </summary>
    public static class ArtifactCatalog {
        private static readonly IReadOnlyDictionary<ExportKind, ArtifactDescriptor> DescriptorsByKind =
            BuildDescriptors().ToDictionary(d => d.Kind);

        /// <summary>
        /// Gets all descriptors known to this version of the application.
        /// </summary>
        public static IReadOnlyCollection<ArtifactDescriptor> All {
            get {
                return DescriptorsByKind.Values.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the descriptor for a specific artifact kind.
        /// </summary>
        /// <param name="kind">Artifact kind to look up.</param>
        /// <returns>The matching descriptor.</returns>
        public static ArtifactDescriptor Get(ExportKind kind) {
            ArtifactDescriptor descriptor;
            if (DescriptorsByKind.TryGetValue(kind, out descriptor)) {
                return descriptor;
            }

            return DescriptorsByKind[ExportKind.Unsupported];
        }

        /// <summary>
        /// Attempts to resolve a descriptor from a repository category and domain.
        /// </summary>
        /// <param name="domain">Expected artifact domain.</param>
        /// <param name="category">Repository category segment.</param>
        /// <returns>The matching descriptor, or <see langword="null"/> when no mapping exists.</returns>
        public static ArtifactDescriptor TryGetByCategory(ArtifactDomain domain, string category) {
            return DescriptorsByKind.Values.FirstOrDefault(d =>
                d.Domain == domain &&
                string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates the fixed descriptor list confirmed against the installed Openness V21 public API.
        /// </summary>
        /// <returns>Descriptor list used by the application.</returns>
        private static IEnumerable<ArtifactDescriptor> BuildDescriptors() {
            yield return D(ExportKind.Udt, ArtifactDomain.Plc, "PLC data type", "Types", true, true, true, true, 0, "PlcType.Export / PlcTypeComposition.Import", string.Empty);
            yield return D(ExportKind.PlcTagTable, ArtifactDomain.Plc, "PLC tag table", "TagTables", true, true, true, true, 10, "PlcTagTable.Export / PlcTagTableComposition.Import", string.Empty);
            yield return D(ExportKind.PlcUserConstant, ArtifactDomain.Plc, "PLC user constant", "UserConstants", true, true, true, false, 11, "PlcUserConstant.Export / PlcUserConstantComposition.Import", "Scanned when the tag table root exposes user constants.");
            yield return D(ExportKind.Ob, ArtifactDomain.Plc, "Organization block", "Blocks", true, true, true, true, 20, "OB.Export / PlcBlockComposition.Import", string.Empty);
            yield return D(ExportKind.Fb, ArtifactDomain.Plc, "Function block", "Blocks", true, true, true, true, 21, "FB.Export / PlcBlockComposition.Import", string.Empty);
            yield return D(ExportKind.Fc, ArtifactDomain.Plc, "Function", "Blocks", true, true, true, true, 22, "FC.Export / PlcBlockComposition.Import", string.Empty);
            yield return D(ExportKind.Db, ArtifactDomain.Plc, "Data block", "Blocks", true, true, true, true, 23, "DataBlock.Export / PlcBlockComposition.Import", "Instance DB import can still depend on the owning FB and technology context.");
            yield return D(ExportKind.PlcWatchTable, ArtifactDomain.Plc, "PLC watch table", "WatchTables", true, true, true, true, 60, "PlcWatchTable.Export / PlcWatchTableComposition.Import", string.Empty);
            yield return D(ExportKind.PlcForceTable, ArtifactDomain.Plc, "PLC force table", "ForceTables", true, true, true, true, 61, "PlcForceTable.Export / PlcForceTableComposition.Import", string.Empty);
            yield return D(ExportKind.TechnologicalObject, ArtifactDomain.Plc, "Technology object", "TechnologyObjects", true, true, true, true, 70, "TechnologicalInstanceDB.Export / TechnologicalInstanceDBComposition.Import", "Represents Openness TechnologicalInstanceDB objects.");
            yield return D(ExportKind.PlcExternalSource, ArtifactDomain.Plc, "External source", "ExternalSources", ".scl", ArtifactFormat.Scl, true, false, false, true, 80, "PlcExternalSourceComposition.CreateFromFile / PlcExternalSource.GenerateBlocksFromSource", "The installed public API exposes source creation/generation but no direct export of existing source text.");
            yield return D(ExportKind.PlcAlarmTextList, ArtifactDomain.Plc, "PLC alarm text list", "AlarmTextLists", ".xlsx", ArtifactFormat.Xlsx, true, false, false, false, 90, "PlcAlarmTextListProvider.ExportToXlsx / ImportFromXlsx", "The public API exposes provider-level XLSX exchange, not a per-list XML artifact flow in this version.");

            yield return D(ExportKind.HmiTagTable, ArtifactDomain.HmiClassic, "HMI tag table", "Tags", true, true, true, true, 100, "TagTable.Export / TagTableComposition.Import", string.Empty);
            yield return D(ExportKind.HmiConnection, ArtifactDomain.HmiClassic, "HMI connection", "Connections", true, true, true, false, 101, "Connection.Export / ConnectionComposition.Import", string.Empty);
            yield return D(ExportKind.HmiTextList, ArtifactDomain.HmiClassic, "HMI text list", "TextLists", true, true, true, false, 102, "TextList.Export / TextListComposition.Import", string.Empty);
            yield return D(ExportKind.HmiGraphicList, ArtifactDomain.HmiClassic, "HMI graphic list", "GraphicLists", true, true, true, false, 103, "GraphicList.Export / GraphicListComposition.Import", string.Empty);
            yield return D(ExportKind.HmiCycle, ArtifactDomain.HmiClassic, "HMI cycle", "Cycles", true, true, true, false, 104, "Cycle.Export / CycleComposition.Import", string.Empty);
            yield return D(ExportKind.HmiVBScript, ArtifactDomain.HmiClassic, "HMI VB script", "Scripts", true, true, true, true, 105, "VBScript.Export / VBScriptComposition.Import", string.Empty);
            yield return D(ExportKind.HmiTemplate, ArtifactDomain.HmiClassic, "HMI template", "Templates", true, true, true, true, 110, "ScreenTemplate.Export / ScreenTemplateComposition.Import", string.Empty);
            yield return D(ExportKind.HmiPopup, ArtifactDomain.HmiClassic, "HMI popup", "Popups", true, true, true, true, 111, "ScreenPopup.Export / ScreenPopupComposition.Import", string.Empty);
            yield return D(ExportKind.HmiSlideIn, ArtifactDomain.HmiClassic, "HMI slide-in", "SlideIns", true, true, true, false, 112, "ScreenSlidein.Export / ScreenSlideinComposition.Import", "The V21 slide-in root exposes a flat composition.");
            yield return D(ExportKind.HmiGlobalElements, ArtifactDomain.HmiClassic, "HMI global screen elements", "GlobalElements", true, true, true, false, 113, "ScreenGlobalElements.Export / HmiTarget.ImportScreenGlobalElements", "Singleton artifact imported at target level.");
            yield return D(ExportKind.HmiScreenOverview, ArtifactDomain.HmiClassic, "HMI screen overview", "ScreenOverview", true, true, true, false, 114, "ScreenOverview.Export / HmiTarget.ImportScreenOverview", "Singleton artifact imported at target level.");
            yield return D(ExportKind.HmiScreen, ArtifactDomain.HmiClassic, "HMI screen", "Screens", true, true, true, true, 120, "Screen.Export / ScreenComposition.Import", string.Empty);
            yield return D(ExportKind.HmiScreenFolder, ArtifactDomain.HmiClassic, "HMI screen folder", "ScreenFolders", false, false, false, true, 121, "UI grouping only", "Folders are grouping nodes; contained screens are exported and imported individually.");

            yield return D(ExportKind.UnifiedTagTable, ArtifactDomain.HmiUnified, "Unified tag table", "Tags", ".hmi.yml", ArtifactFormat.Directory, false, false, false, true, 200, "WinCC Unified directory exchange", "Unified directory exchange is not enabled in the classic HMI XML pipeline.");
            yield return D(ExportKind.UnifiedScriptModule, ArtifactDomain.HmiUnified, "Unified script module", "Scripts", ".hmi.yml", ArtifactFormat.Directory, false, false, false, true, 210, "WinCC Unified directory exchange", "Unified directory exchange is not enabled in the classic HMI XML pipeline.");
            yield return D(ExportKind.UnifiedTextList, ArtifactDomain.HmiUnified, "Unified text list", "TextLists", ".hmi.yml", ArtifactFormat.Directory, false, false, false, false, 220, "WinCC Unified directory exchange", "Unified directory exchange is not enabled in the classic HMI XML pipeline.");
            yield return D(ExportKind.UnifiedGraphicList, ArtifactDomain.HmiUnified, "Unified graphic list", "GraphicLists", ".hmi.yml", ArtifactFormat.Directory, false, false, false, false, 221, "WinCC Unified directory exchange", "Unified directory exchange is not enabled in the classic HMI XML pipeline.");

            yield return D(ExportKind.HardwareCax, ArtifactDomain.Hardware, "Hardware CAx", "Cax", ".aml", ArtifactFormat.Aml, false, false, false, false, 300, "CaxProvider.Export / CaxProvider.Import", "Project/device-level transfer, not a selectable artifact in this version.");
            yield return D(ExportKind.Unsupported, ArtifactDomain.Project, "Unsupported artifact", "Unsupported", ".xml", ArtifactFormat.Unsupported, false, false, false, false, 999, string.Empty, "Used for repository paths that cannot be mapped safely.");
        }

        /// <summary>
        /// Creates one XML descriptor.
        /// </summary>
        /// <param name="kind">Stable kind.</param>
        /// <param name="domain">Artifact domain.</param>
        /// <param name="name">Display name.</param>
        /// <param name="category">Repository category.</param>
        /// <param name="scan">Whether scanning is enabled.</param>
        /// <param name="export">Whether export is enabled.</param>
        /// <param name="import">Whether import is enabled.</param>
        /// <param name="groups">Whether group creation is enabled.</param>
        /// <param name="order">Import order.</param>
        /// <param name="api">API description.</param>
        /// <param name="limitations">Limitations text.</param>
        /// <returns>A descriptor using the XML extension.</returns>
        private static ArtifactDescriptor D(
            ExportKind kind,
            ArtifactDomain domain,
            string name,
            string category,
            bool scan,
            bool export,
            bool import,
            bool groups,
            int order,
            string api,
            string limitations) {
            return D(kind, domain, name, category, ".xml", ArtifactFormat.Xml, scan, export, import, groups, order, api, limitations);
        }

        /// <summary>
        /// Creates one descriptor with an explicit format.
        /// </summary>
        /// <param name="kind">Stable kind.</param>
        /// <param name="domain">Artifact domain.</param>
        /// <param name="name">Display name.</param>
        /// <param name="category">Repository category.</param>
        /// <param name="extension">Primary file extension.</param>
        /// <param name="format">Payload format.</param>
        /// <param name="scan">Whether scanning is enabled.</param>
        /// <param name="export">Whether export is enabled.</param>
        /// <param name="import">Whether import is enabled.</param>
        /// <param name="groups">Whether group creation is enabled.</param>
        /// <param name="order">Import order.</param>
        /// <param name="api">API description.</param>
        /// <param name="limitations">Limitations text.</param>
        /// <returns>A descriptor with the requested extension and format.</returns>
        private static ArtifactDescriptor D(
            ExportKind kind,
            ArtifactDomain domain,
            string name,
            string category,
            string extension,
            ArtifactFormat format,
            bool scan,
            bool export,
            bool import,
            bool groups,
            int order,
            string api,
            string limitations) {
            return new ArtifactDescriptor(kind, domain, name, category, extension, format, scan, export, import, groups, order, api, limitations);
        }
    }
}
