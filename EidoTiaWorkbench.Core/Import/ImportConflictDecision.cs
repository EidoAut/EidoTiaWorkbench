// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportConflictDecision.cs
// Requires an explicit replacement decision before backup or any engineering mutation.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>Requires an explicit replacement decision before backup or any engineering mutation.</summary>
    public static class ImportConflictDecision {
        /// <summary>Returns false for Skip; Ask fails closed until the user selects Replace or Skip.</summary>
        /// <param name="exists">Whether the target currently contains the artifact.</param>
        /// <param name="policy">Explicit policy selected for this operation.</param>
        /// <returns>Whether importing is permitted before any backup or mutation.</returns>
        /// <exception cref="InvalidOperationException">An existing artifact still requires an Ask decision.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The policy is not a defined value.</exception>
        public static bool ShouldImport(bool exists, ImportConflictPolicy policy) {
            if (!Enum.IsDefined(typeof(ImportConflictPolicy), policy)) {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            if (!exists) {
                return true;
            }

            if (policy == ImportConflictPolicy.Skip) {
                return false;
            }

            if (policy == ImportConflictPolicy.Ask) {
                throw new InvalidOperationException(
                    "Existing artifact requires an explicit Replace or Skip decision. No replacement was performed.");
            }

            return true;
        }
    }
}
