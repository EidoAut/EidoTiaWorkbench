// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OpennessInstallation.cs
// Describes installation evidence independently of adapter and live-test status.
//
// Developed by EIDO Automation
// =========================================================================
using System;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Describes installation evidence independently of adapter and live-test status.</summary>
    public sealed class OpennessInstallation {
        /// <summary>Gets or sets the parsed Siemens API major version; zero denotes unknown provenance.</summary>
        public int MajorVersion {
            get; set;
        }

        /// <summary>Gets or sets the directory containing the validated public API assemblies.</summary>
        public string ApiPath {
            get; set;
        }

        /// <summary>Gets or sets the registry, environment or filesystem evidence that produced this candidate.</summary>
        public string Source {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether assembly identities and architecture passed validation.</summary>
        public bool IsValid {
            get; set;
        }

        /// <summary>Gets or sets the discovery failure or validation explanation for display.</summary>
        public string Diagnostic {
            get; set;
        }

        /// <summary>Gets a value indicating whether the version belongs to the recognized V17 through V21 range.</summary>
        public bool IsRecognized => this.MajorVersion >= 17 && this.MajorVersion <= 21;

        /// <summary>Gets a value indicating whether an implementation exists for this API family.</summary>
        public bool HasAdapter => this.MajorVersion == 21;

        /// <summary>Gets a value indicating whether the validated candidate has an adapter in this x64 process.</summary>
        public bool CanUse => this.IsValid && this.HasAdapter && Environment.Is64BitProcess;

        /// <summary>Gets the supported engineering scope and explicit verification limitations.</summary>
        public string Capabilities => this.CanUse
            ? "V21 PLC and classic HMI catalog; per-artifact restrictions apply. Unified exchange unavailable. Live round-trip not certified."
            : "Engineering operations unavailable: " + (this.Diagnostic ?? "No verified adapter for this API family.");

        /// <inheritdoc />
        public override string ToString() => "V" + this.MajorVersion + " | " + (this.CanUse ? "Adapter available" : "Unsupported") + " | " + this.ApiPath;
    }
}
