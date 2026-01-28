# Dagobert

A Final Fantasy XIV Dalamud plugin for retainer market board management, market board data.

## Overview

Dagobert is a comprehensive market board plugin that helps players manage their retainer listings efficiently. It provides price undercutting, and detailed item inspection capabilities.

## Features

### Auto Pinch (Automatic Price Adjustment)
- **Retainer List Automation**: Automatically process all retainers with a single click
- **Smart Price Undercutting**: Automatically adjusts prices to stay competitive
  - Fixed amount undercutting (e.g., -1 gil)
  - Percentage-based undercutting
  - Configurable maximum undercut percentage
- **HQ Item Support**: Separate handling for High-Quality items
- **Stack Size Matching**: Option to only compete with similar stack sizes
- **Outlier Protection**: Smart detection and avoidance of price baiting
- **Self-Undercut Prevention**: Option to avoid undercutting your own listings

### Humanization Features
- **Delay Strategies**: Multiple randomization algorithms (Uniform, Gaussian, LogNormal, Bimodal)
- **Reaction Delays**: Simulates human reaction times
- **Fatigue Simulation**: Gradual slowing over time for realism
- **Fitts Law Simulation**: Mouse movement timing based on distance
- **Misclick Simulation**: Random occasional misclicks with recovery
- **Comparative Doubt**: Simulated hesitation when comparing prices
- **Pricing Personalities**:
  - Standard: Normal undercutting behavior
  - Clean Numbers: Rounds to aesthetically pleasing numbers
  - Polite Match: Matches prices instead of undercutting

### Sales Monitoring =- TODO
- **Real-time Sale Detection**:
- **Multi-language Support**:
- **Discord Integration**:
- **Statistics Tracking**:
 Not implemented yet fully

### Item Management
- **Per-Item Configuration**:
  - Minimum price enforcement
  - Ignore specific items
  - Match-only mode (no undercutting)
- **Context Menu Integration**: Right-click any item in your inventory to configure or inspect
- **Item Inspector**: Detailed item information including:
  - Market board data from Universalis
  - Cross-world pricing
  - Cross-datacenter pricing
  - Recipe information
  - Gathering locations (this is not fully implemented)
  - Gil shop availability (this is not fully implemented)
  - Price history and trends
  - Easy to use search bar (currently a bit broken)

### Safety Features
- **Emergency Stop**: Automatic abort on incoming tell messages
- **Mouse Movement Detection**: Aborts if significant mouse movement detected
- **Retainer Selection**: Enable/disable specific retainers for automation
- **Timeout Protection**: Task timeouts to prevent stuck operations

### Visual Monitor
- **Real-time Activity Log**: Visual feedback of all plugin actions

### Configuration Options
- **Timing Controls**:
  - Market board delay range
  - Window keep-open duration
- **UI Preferences**:
  - Show/hide error messages in chat
  - Show/hide price adjustment notifications
  - Show/hide retainer names
  - Randomness graph visualization
- **Webhook Settings**: Discord integration for notifications

## Commands

- `/dagobert` - Opens the configuration window

## File Structure

```
Dagobert/
├── AddonInteractor.cs      # UI interaction with game addons
├── AutoPinch.cs            # Main automation logic
├── Communicator.cs         # Chat message handling
├── Configuration.cs        # Plugin configuration
├── ContextMenuIntegration.cs # Right-click menu integration
├── DiscordSender.cs        # Discord webhook integration
├── GarlandClient.cs        # Garland Tools API client
├── Humanizer.cs            # Humanization delay calculations
├── LuminaDataProvider.cs   # Game data indexing
├── MarketBoardHandler.cs   # Market board data processing
├── NewPriceEventArgs.cs    # Price update events
├── OverlayRenderer.cs      # UI overlay rendering
├── Plugin.cs               # Main plugin entry point
├── RandomDelayGenerator.cs # Delay randomization
├── RetainerStats.cs        # Per-retainer statistics
├── SalesMonitor.cs         # Sale detection and tracking
├── StatisticsAnalyzer.cs   # Advanced statistics
├── UniversalisClient.cs    # Universalis API client
├── Utilities/
│   ├── ImGuiUtils.cs       # ImGui helper functions
│   ├── ItemUtils.cs        # Item-related utilities
│   ├── MathUtils.cs        # Mathematical utilities
│   ├── StringUtils.cs      # String manipulation utilities
│   └── UIConsts.cs         # UI constants
├── Windows/
│   ├── ConfigWindow.cs     # Configuration UI
│   ├── ItemInspector.cs    # Item inspection UI
│   └── VisualMonitor.cs    # Activity visualization UI
```

## Dependencies

- Dalamud (FFXIV plugin framework)
- ECommons (Common utilities library)
- FFXIVClientStructs (Game structure definitions)
- Lumina (Game data access)

## API Integrations

- **Universalis**: Real-time market board data
- **Garland Tools**: (not fully implemented, currently only using it to create some links in the discord report)

## License

This project is licensed under the terms specified in the repository.

## Disclaimer

This plugin is for educational purposes. Use at your own risk. Automation plugins may violate the Terms of Service of Final Fantasy XIV.
