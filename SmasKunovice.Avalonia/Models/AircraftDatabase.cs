using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SmasKunovice.Avalonia.Models;

public interface IAircraftDatabase
{
    AircraftRecord? GetByIcao24(string icao24);
}

public class AircraftDatabase : IAircraftDatabase
{
    private readonly Dictionary<string, AircraftRecord> _byIcao24;

    public AircraftDatabase(string aircraftCsvPath)
    {
        if (string.IsNullOrWhiteSpace(aircraftCsvPath))
            throw new ArgumentException("CSV path must be provided", nameof(aircraftCsvPath));

        if (!File.Exists(aircraftCsvPath))
            throw new FileNotFoundException("Aircraft CSV file not found", aircraftCsvPath);

        _byIcao24 = ParseCsv(aircraftCsvPath);
    }

    public AircraftRecord? GetByIcao24(string icao24)
    {
        AircraftRecord? record = null;
        if (!string.IsNullOrWhiteSpace(icao24))
            _byIcao24.TryGetValue(icao24, out record);

        return record;
    }


    private Dictionary<string, AircraftRecord> ParseCsv(string filePath)
    {
        var result = new Dictionary<string, AircraftRecord>(StringComparer.InvariantCultureIgnoreCase);

        // Read the entire file content. 
        // We use ReadAllText here because we need to handle multi-line fields 
        // that might break standard ReadLine() logic.
        // var fileContent = File.ReadAllText(filePath);
        using var reader = new StreamReader(filePath);

        var rows = ParseRawContent(reader);

        // Skip header row (index 0) and process data
        foreach (var fields in rows.Skip(1))
        {
            // Safety check: ensure we have enough columns (32 columns)
            // We use >= 32 because sometimes trailing empty commas create extra empty entries
            if (fields.Length < 32) continue; 

            var dto = new AircraftRecord
            {
                Icao24 = fields[0] ?? throw new ArgumentException("Key field (Icao24) is missing or empty."),
                Timestamp = fields[1],
                Acars = fields[2],
                Adsb = fields[3],
                Built = fields[4],
                CategoryDescription = fields[5],
                Country = fields[6],
                Engines = fields[7],
                FirstFlightDate = fields[8],
                FirstSeen = fields[9],
                IcaoAircraftClass = fields[10],
                LineNumber = fields[11],
                ManufacturerIcao = fields[12],
                ManufacturerName = fields[13],
                Model = fields[14],
                Modes = fields[15],
                NextReg = fields[16],
                Notes = fields[17],
                Operator = fields[18],
                OperatorCallsign = fields[19],
                OperatorIata = fields[20],
                OperatorIcao = fields[21],
                Owner = fields[22],
                PrevReg = fields[23],
                RegUntil = fields[24],
                Registered = fields[25],
                Registration = fields[26],
                SelCal = fields[27],
                SerialNumber = fields[28],
                Status = fields[29],
                Typecode = fields[30],
                Vdl = fields[31]
            };

            // Use Icao24 as key, handling potential duplicates safely
            if (!string.IsNullOrEmpty(dto.Icao24))
            {
                result.TryAdd(dto.Icao24, dto);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses raw CSV content, handling mixed single quotes and stripping newlines inside quotes.
    /// </summary>
    private IEnumerable<string?[]> ParseRawContent(StreamReader reader)
    {
        var currentRow = new List<string?>();
        var currentField = new StringBuilder();
            
        var insideQuotes = false;

        int charCode;
        while ((charCode = reader.Read()) != -1)
        {
            var c = (char)charCode;
            if (c == '\'')
            {
                insideQuotes = !insideQuotes;
                // We generally don't append the quotes themselves to the field data
                // based on standard CSV processing, effectively "unquoting" the value.
                continue; 
            }

            if (insideQuotes)
            {
                ProcessInQuotes(c, currentField);
            }
            else
            {
                switch (c)
                {
                    // NOT inside quotes
                    case ',':
                    {
                        var currentFieldString = currentField.ToString().Trim();
                        currentFieldString = string.IsNullOrWhiteSpace(currentFieldString) ? null : currentFieldString;
                        // End of field
                        currentRow.Add(currentFieldString); // Trim specifically for mixed cases like: ,'value',
                        currentField.Clear();
                        break;
                    }
                    case '\n':
                    {
                        // End of record
                        var currentFieldString = currentField.ToString().Trim();
                        currentFieldString = string.IsNullOrWhiteSpace(currentFieldString) ? null : currentFieldString;
                        currentRow.Add(currentFieldString);
                        currentField.Clear();
                        
                        // Add row to results
                        if (currentRow.Count > 0)
                        {
                            yield return currentRow.ToArray();
                            currentRow.Clear();
                        }

                        break;
                    }
                    case '\r':
                        // Ignore carriage returns outside quotes (usually part of CRLF line ending)
                        break;
                    default:
                        currentField.Append(c);
                        break;
                }
            }
        }

        // Flush final field/row if file didn't end with newline
        if (currentField.Length <= 0 && currentRow.Count <= 0)
            yield break;
        
        currentRow.Add(currentField.ToString().Trim());
        yield return currentRow.ToArray();
    }

    private static void ProcessInQuotes(char c, StringBuilder currentField)
    {
        // Logic: If inside quotes, ignore commas.
        // Special Logic: If inside quotes and we hit a newline, remove it (replace with space).
        if (c is '\n' or '\r')
        {
            // Check previous char to avoid double spaces if CRLF
            if (currentField.Length > 0 && currentField[currentField.Length - 1] != ' ')
            {
                currentField.Append(' ');
            }
        }
        else
        {
            currentField.Append(c);
        }
    }
}