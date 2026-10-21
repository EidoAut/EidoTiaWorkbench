// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportPreflightResult.cs
// Collects issues found before invoking Siemens Openness import.
//
// Developed by EIDO Automation
// =========================================================================
using System.Collections.Generic;
using System.Linq;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Collects issues found before invoking Siemens Openness import.
    /// </summary>
    public sealed class ImportPreflightResult {
        private readonly List<ImportValidationIssue> _issues = new List<ImportValidationIssue>();

        /// <summary>
        /// Gets all validation issues in discovery order.
        /// </summary>
        public IReadOnlyList<ImportValidationIssue> Issues {
            get {
                return this._issues.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the number of validation errors.
        /// </summary>
        public int ErrorCount {
            get {
                return this._issues.Count(i => i.Severity == ImportValidationSeverity.Error);
            }
        }

        /// <summary>
        /// Gets the number of validation warnings.
        /// </summary>
        public int WarningCount {
            get {
                return this._issues.Count(i => i.Severity == ImportValidationSeverity.Warning);
            }
        }

        /// <summary>
        /// Gets a value indicating whether import should stop before Siemens Openness is called.
        /// </summary>
        public bool HasBlockingErrors {
            get {
                return this.ErrorCount > 0;
            }
        }

        /// <summary>
        /// Adds a validation issue.
        /// </summary>
        /// <param name="issue">Issue to append.</param>
        public void Add(ImportValidationIssue issue) {
            if (issue != null) {
                this._issues.Add(issue);
            }
        }
    }
}
