# SmasKunovice

SmasKunovice is a .NET desktop application for displaying and processing aviation-related data, implementing requirements based on the newly proposed Surface Movement Awereness System (SMAS) standard. The application is being developed as part of the research programme conducting a safety analysis for SMAS. The application is intended to be used by the air traffic controller at the Kunovice airport. The application is mainly intended to be used for assisting with air traffic control purposes.

## Installation:

1. Download the latest release (installer file).
   * Installer with the `_local` suffix installs both a local broker service and client app configured to connect to the local broker
   * Installer with the `_remote` suffix installs only the client app and connects to a pre-defined remote MQTT broker
1. Run the installer
   * You will get prompted for credentials. Make sure you **input correct credentials** for authentication whe installing in **remote broker mode**.
1. Run the app via the `SMAS Kunovice` shortcut on your desktop.

## Configuration

The application behaviour can be configured by editing the following files:

- `appsettings.json`
- `appsettings.User.json`

Use the `appsettings.User.json` to change credentials for connecting to the MQTT broker.

Use the `appsettings.json` to change the behaviour of the application. The property detials are described below:

### ClientAdapterOptions

Controls how the application connects to the MQTT broker and processes incoming drone telemetry data streams.

* `Host` - The network address (IP address or domain name) of the MQTT broker.
* `Port` - The port number of the MQTT broker.
* `HeartbeatTopic` - The MQTT subscription topic used to listen for drone heartbeat signals (default: dronetag/heartbeat).
* `OdidTopic` - The MQTT subscription topic used to receive aircraft information (default: dronetag/odid).
* `LogReceivedMessages` - Set to `true` to enable verbose recording of all inbound ODID messages for diagnostic. Keep `false` during normal production use to save disk space.
* `isBatchedData` - Set to `true` to configure client for receiving batched ODID messages.
* `isCompressedData` - Set to `true` to configure client for receiving compressed ODID messages (gzip), which are then decompressed.
* `ClientSourceLogFilePath`
  * Leave blank to process ODID messages from live MQTT endpoint configured by `Host` and `Port`.
  * Set to an existing file path to stream and process ODID messages from that file (compatible with logs produced by the client when `LogReceivedMessages` is set to `true`)

### ApplicationSettings

* `LoggingLevel` - Sets the minimum severity threshold for logging. Values include `Information`, `Warning`, `Error`, or `Debug`.
