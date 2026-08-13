// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays TIA Portal connection state in an information card.
    /// </summary>
    public sealed class ConnectionStatusCard : InfoCard {
        /// <summary>
        /// Updates the card with connection state and detail text.
        /// </summary>
        /// <param name="connected">Whether TIA Portal is connected.</param>
        /// <param name="detail">Detail text shown as the card value.</param>
        public void SetConnected(bool connected, string detail) {
            this.SetContent(
                "TIA Portal connection",
                detail,
                connected ? "Connected" : "Disconnected",
                connected ? ThemeColors.Success : ThemeColors.Disabled);
        }
    }
}
