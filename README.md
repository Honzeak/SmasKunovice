# SmasKunovice

SmasKunovice is a .NET desktop application for displaying and processing aviation-related data, implementing requirements based on the newly proposed Surface Movement Awereness System (SMAS) standard. The application is being developed as part of the research programme conducting a safety analysis for SMAS. The application is intended to be used by the air traffic controller at the Kunovice airport.

## Installation:
1. Download the latest release.
1. Unzip the archive.
1. Fill out your MQTT account credentials in the `appsettings.User.json` file.
1. Run the app by running the `SmasKunovice.Avalonia.exe` executable.

## Configuration

The application loads configuration from:

- `appsettings.json`
- `appsettings.User.json`

If you are running the app locally, make sure the required settings (e.g. MQTT credentials) are present in those files or in user secrets. For instance, you can set up replaying historical aviation data by providing path to a JSON log file.
