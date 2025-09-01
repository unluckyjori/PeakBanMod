using System;

namespace PeakNetworkDisconnectorMod;

/// <summary>
/// Represents a banned player with their details and ban information
/// </summary>
[Serializable]
public class BannedPlayer
{
	/// <summary>
	/// The player's display name at time of ban
	/// </summary>
	public string PlayerName { get; set; }

	/// <summary>
	/// The player's Steam ID (may be "Unknown" if not available)
	/// </summary>
	public string SteamID { get; set; }

	/// <summary>
	/// Date and time when the ban was applied
	/// </summary>
	public string BanDate { get; set; }

	/// <summary>
	/// Reason for the ban (optional)
	/// </summary>
	public string Reason { get; set; }
}
