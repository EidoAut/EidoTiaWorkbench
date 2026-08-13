// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Represents one operational message displayed in the activity log.
    /// </summary>
    public sealed class ActivityEntry {
        /// <summary>
        /// Gets or sets the local timestamp when the activity was recorded.
        /// </summary>
        public DateTime Time {
            get; set;
        }

        /// <summary>
        /// Gets or sets the severity used to render the activity.
        /// </summary>
        public ActivitySeverity Severity {
            get; set;
        }

        /// <summary>
        /// Gets or sets the operation that emitted the activity.
        /// </summary>
        public string Operation {
            get; set;
        }

        /// <summary>
        /// Gets or sets the artifact associated with the activity, when applicable.
        /// </summary>
        public string Artifact {
            get; set;
        }

        /// <summary>
        /// Gets or sets the target path or device associated with the activity.
        /// </summary>
        public string Target {
            get; set;
        }

        /// <summary>
        /// Gets or sets the user-facing activity message.
        /// </summary>
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets optional technical detail such as exception text.
        /// </summary>
        public string Detail {
            get; set;
        }
    }
}
