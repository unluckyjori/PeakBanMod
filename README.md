
---
**Disclaimer**: This mod uses packet flooding when Aggressive Enforcement Mode is enabled. Packet flooding sends more data than the normal game in the attempt to disconnect them, which can stress Photon servers and may violate the Terms of Service. Use at your own risk.
---

# PeakBanMod

A powerful BepInEx mod for PEAK that gives room hosts complete control over player management and anti-cheat enforcement.

## Features

### Host-Only Ban System
- **Host Control**: Only room hosts can access ban management
- **Persistent Bans**: Ban list survives game restarts
- **Steam ID Integration**: Accurate player identification using Steam IDs
- **Ban by Name or ID**: Flexible banning options

### Anti-Cheat Integration
- **Auto-Detection**: Automatically detects known cheating tools
- **External Mod Support**: Integrates with PEAKER and PeakAntiCheat mods
- **Smart Enforcement**: Two enforcement modes for different situations

### User Interface
- **In-Game Menu**: Press F10 (configurable) to open ban management
- **Real-Time Player List**: See all players with their Steam IDs
- **One-Click Actions**: Ban/unban players with a single click
- **Visual Indicators**: Clear status indicators for banned/targeted players

### Enforcement Modes

#### Passive Mode
- Blocks network from them
- Prevents further gameplay disruption
- Allows manual intervention by the host

#### Aggressive Mode
- Uses network flooding
- Immediate removal from the game
- Maximum security for competitive play

## How to Use

### Basic Usage
1. **Host a Room**: Create or join a room as the host
2. **Open Menu**: Press **F10** (or your configured key) to open the ban menu
3. **Manage Players**: View all players, their Steam IDs, and ban status
4. **Take Action**: Click "BAN" next to any player to ban them

### Configuration

Access the configuration file at:
```
PEAK/BepInEx/config/com.icemods.hostbanmod.cfg
```

#### Key Settings
- **Toggle Keybind**: Change the key to open the menu (default: F10)
- **Enforcement Mode**: Choose between Passive and Aggressive
- **Auto-Detect Hacks**: Enable/disable automatic cheat detection
- **Performance Settings**: Adjust check intervals for better performance

## Ban List Management

### Viewing Banned Players
- Scroll through the banned players list in the UI
- See ban dates, reasons, and Steam IDs
- Click "UNBAN" to remove bans

### Ban List File
Bans are stored in:
```
PEAK/BepInEx/config/peak_host_banlist.json
```

This file persists across game sessions and can be edited manually if needed.

## Anti-Cheat Integration

### Supported Mods
- **PEAKER**: Advanced anti-cheat with comprehensive detection
- **PeakAntiCheat**: Specialized PEAK anti-cheat system

### How It Works
1. The mod detects if compatible anti-cheat mods are installed
2. Integrates with their detection systems
3. Uses their data for more accurate banning
4. Provides unified management through the PeakBanMod interface

## Troubleshooting

### Common Issues

#### Menu Won't Open
- **Solution**: Ensure you're the room host
- **Check**: Only hosts can access the ban management interface

#### Bans Not Working
- **Solution**: Verify you're connected to a Photon room
- **Check**: The mod only works in multiplayer rooms

#### Performance Issues
- **Solution**: Adjust check intervals in config
- **Settings**: Increase `BanCheckInterval` and `SteamUpdateInterval`

#### Integration Not Working
- **Solution**: Ensure anti-cheat mods are properly installed
- **Check**: Look for "Detected" status in the integration section

## Safety & Fair Play

### Important Notes
- **Host Responsibility**: Use this mod responsibly as a host
- **False Positives**: Some detections may have false positives
- **Manual Review**: Always verify bans before applying them
- **Community Guidelines**: Follow your community's rules regarding mod usage

### Best Practices
1. **Communicate**: Let players know about active moderation
2. **Document**: Keep ban reasons for transparency
3. **Review**: Regularly review and clean up old bans
4. **Update**: Keep the mod updated for latest features and fixes

## Credits

**icetypes**
- For making the [original mod](https://thunderstore.io/c/peak/p/IceMods/PeakBanMod/)

## Support

- **Issues**: [GitHub Issues](https://github.com/your-repo/PeakBanMod/issues)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

