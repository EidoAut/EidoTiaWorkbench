// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Provides event data for a newly appended activity entry.
    /// </summary>
    public sealed class ActivityEntryEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityEntryEventArgs"/> class.
        /// </summary>
        /// <param name="entry">Activity entry that was added.</param>
        public ActivityEntryEventArgs(ActivityEntry entry) {
            this.Entry = entry;
        }

        /// <summary>
        /// Gets the activity entry that was added.
        /// </summary>
        public ActivityEntry Entry {
            get;
        }
    }
}
