using System.Text;

namespace DDCSwitch;

/// <summary>
/// Parses EDID (Extended Display Identification Data) blocks to extract monitor information.
/// </summary>
public static class EdidParser
{
    /// <summary>
    /// Parses manufacturer ID from EDID bytes.
    /// </summary>
    /// <param name="edid">EDID data (at least 2 bytes from offset 8)</param>
    /// <returns>3-letter manufacturer ID (e.g., "SAM", "DEL") or null if invalid</returns>
    public static string? ParseManufacturerId(byte[] edid)
    {
        if (edid.Length < 10) return null;
        
        try
        {
            // Manufacturer ID is stored in bytes 8-9 as 3 5-bit characters
            // Bit layout: |0|CHAR1|CHAR2|CHAR3| across 2 bytes
            ushort id = (ushort)((edid[8] << 8) | edid[9]);
            
            char char1 = (char)(((id >> 10) & 0x1F) + 'A' - 1);
            char char2 = (char)(((id >> 5) & 0x1F) + 'A' - 1);
            char char3 = (char)((id & 0x1F) + 'A' - 1);
            
            return $"{char1}{char2}{char3}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the full manufacturer name from 3-letter PNP ID.
    /// </summary>
    /// <param name="manufacturerId">3-letter manufacturer ID</param>
    /// <returns>Full company name or the ID itself if not found</returns>
    public static string GetManufacturerName(string? manufacturerId)
    {
        if (string.IsNullOrEmpty(manufacturerId)) return "Unknown";
        
        return manufacturerId switch
        {
            "AAC" => "AcerView",
            "ACI" => "Asus Computer Inc",
            "ACR" => "Acer Technologies",
            "ACT" => "Targa",
            "ADI" => "ADI Corporation",
            "AIC" => "AG Neovo",
            "AMW" => "AMW",
            "AOC" => "AOC International",
            "API" => "A Plus Info Corporation",
            "APP" => "Apple Computer",
            "ART" => "ArtMedia",
            "AST" => "AST Research",
            "AUO" => "AU Optronics",
            "BEL" => "Belkin",
            "BEN" => "BenQ Corporation",
            "BMM" => "BMM",
            "BNQ" => "BenQ Corporation",
            "BOE" => "BOE Technology",
            "CMO" => "Chi Mei Optoelectronics",
            "CPL" => "Compal Electronics",
            "CPQ" => "Compaq Computer Corporation",
            "CTX" => "CTX International",
            "DEC" => "Digital Equipment Corporation",
            "DEL" => "Dell Inc.",
            "DPC" => "Delta Electronics",
            "DWE" => "Daewoo Electronics",
            "ECS" => "ELITEGROUP Computer Systems",
            "EIZ" => "EIZO Corporation",
            "ELS" => "ELSA GmbH",
            "ENC" => "Eizo Nanao Corporation",
            "EPI" => "Envision Peripherals",
            "EPH" => "Epiphan Systems Inc.",
            "FUJ" => "Fujitsu Siemens Computers",
            "FUS" => "Fujitsu Siemens Computers",
            "GSM" => "LG Electronics",
            "GWY" => "Gateway 2000",
            "HEI" => "Hyundai Electronics Industries",
            "HIQ" => "Hyundai ImageQuest",
            "HIT" => "Hitachi",
            "HPN" => "HP Inc.",
            "HSD" => "Hannstar Display Corporation",
            "HSL" => "Hansol Electronics",
            "HTC" => "Hitachi",
            "HWP" => "HP Inc.",
            "IBM" => "IBM Corporation",
            "ICL" => "Fujitsu ICL",
            "IFS" => "InFocus Corporation",
            "IQT" => "Hyundai ImageQuest",
            "IVM" => "Iiyama North America",
            "KDS" => "KDS USA",
            "KFC" => "KFC Computek",
            "LEN" => "Lenovo",
            "LGD" => "LG Display",
            "LKM" => "ADLAS",
            "LNK" => "LINK Technologies",
            "LPL" => "LG Philips",
            "LTN" => "Lite-On Technology",
            "MAG" => "MAG InnoVision",
            "MAX" => "Maxdata Computer",
            "MEI" => "Panasonic Industry Company",
            "MEL" => "Mitsubishi Electronics",
            "MED" => "Matsushita Electric Industrial",
            "MS_" => "Panasonic Industry Company",
            "MSI" => "Micro-Star International",
            "MSH" => "Microsoft Corporation",
            "NAN" => "NANAO Corporation",
            "NEC" => "NEC Corporation",
            "NOK" => "Nokia",
            "NVD" => "Nvidia",
            "OPT" => "Optoma Corporation",
            "OQI" => "OPTIQUEST",
            "PBN" => "Packard Bell",
            "PCK" => "Daewoo Electronics",
            "PDC" => "Polaroid",
            "PGS" => "Princeton Graphic Systems",
            "PHL" => "Philips Consumer Electronics",
            "PIX" => "Pixelink",
            "PNR" => "Planar Systems",
            "PRT" => "Princeton Graphic Systems",
            "REL" => "Relisys",
            "SAM" => "Samsung Electric Company",
            "SAN" => "Sanyo Electric Co.",
            "SBI" => "Smarttech",
            "SEC" => "Seiko Epson Corporation",
            "SGI" => "Silicon Graphics",
            "SMC" => "Samtron",
            "SMI" => "Smile",
            "SNI" => "Siemens Nixdorf",
            "SNY" => "Sony Corporation",
            "SPT" => "Sceptre Tech Inc.",
            "SRC" => "Shamrock Technology",
            "STN" => "Samtron",
            "STP" => "Sceptre Tech Inc.",
            "TAT" => "Tatung Company of America",
            "TOS" => "Toshiba Corporation",
            "TRL" => "Royal Information Company",
            "TSB" => "Toshiba America Info Systems",
            "UNK" => "Unknown",
            "UNM" => "Unisys Corporation",
            "VSC" => "ViewSonic Corporation",
            "WTC" => "Wen Technology",
            "ZCM" => "Zenith Data Systems",
            _ => manufacturerId,
        };
    }

    /// <summary>
    /// Parses the model name from EDID descriptor blocks.
    /// </summary>
    /// <param name="edid">EDID data (at least 128 bytes)</param>
    /// <returns>Model name string or null if not found</returns>
    public static string? ParseModelName(byte[] edid)
    {
        if (edid.Length < 128) return null;
        
        // Check descriptor blocks at offsets 54, 72, 90, 108 (18 bytes each)
        for (int offset = 54; offset <= 108; offset += 18)
        {
            // Descriptor type 0xFC indicates monitor name
            if (edid[offset] == 0x00 && edid[offset + 1] == 0x00 && 
                edid[offset + 2] == 0x00 && edid[offset + 3] == 0xFC)
            {
                return ParseDescriptorString(edid, offset + 5);
            }
        }
        
        return null;
    }

    /// <summary>
    /// Parses the serial number from EDID descriptor blocks.
    /// </summary>
    /// <param name="edid">EDID data (at least 128 bytes)</param>
    /// <returns>Serial number string or null if not found</returns>
    public static string? ParseSerialNumber(byte[] edid)
    {
        if (edid.Length < 128) return null;
        
        // Check descriptor blocks at offsets 54, 72, 90, 108 (18 bytes each)
        for (int offset = 54; offset <= 108; offset += 18)
        {
            // Descriptor type 0xFF indicates serial number
            if (edid[offset] == 0x00 && edid[offset + 1] == 0x00 && 
                edid[offset + 2] == 0x00 && edid[offset + 3] == 0xFF)
            {
                return ParseDescriptorString(edid, offset + 5);
            }
        }
        
        return null;
    }

    /// <summary>
    /// Parses the product code from EDID.
    /// </summary>
    /// <param name="edid">EDID data (at least 12 bytes)</param>
    /// <returns>Product code as ushort or null if invalid</returns>
    public static ushort? ParseProductCode(byte[] edid)
    {
        if (edid.Length < 12) return null;
        
        try
        {
            // Product code is at bytes 10-11 (little-endian)
            return (ushort)(edid[10] | (edid[11] << 8));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the manufacture year from EDID.
    /// </summary>
    /// <param name="edid">EDID data (at least 18 bytes)</param>
    /// <returns>Manufacture year (e.g., 2023) or null if invalid</returns>
    public static int? ParseManufactureYear(byte[] edid)
    {
        if (edid.Length < 18) return null;
        
        try
        {
            // Manufacture year is at byte 17, stored as offset from 1990
            int year = edid[17] + 1990;
            return year >= 1990 && year <= 2100 ? year : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the manufacture week from EDID.
    /// </summary>
    /// <param name="edid">EDID data (at least 17 bytes)</param>
    /// <returns>Manufacture week (1-53) or null if invalid</returns>
    public static int? ParseManufactureWeek(byte[] edid)
    {
        if (edid.Length < 17) return null;
        
        try
        {
            // Manufacture week is at byte 16 (1-53, or 0xFF for unknown)
            int week = edid[16];
            return week >= 1 && week <= 53 ? week : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper to parse ASCII string from EDID descriptor block.
    /// </summary>
    private static string? ParseDescriptorString(byte[] edid, int offset)
    {
        try
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 13; i++)
            {
                byte b = edid[offset + i];
                if (b == 0x0A || b == 0x00) break; // Newline or null terminator
                if (b >= 0x20 && b <= 0x7E) // Printable ASCII
                {
                    sb.Append((char)b);
                }
            }
            
            string result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates EDID header (first 8 bytes should be: 00 FF FF FF FF FF FF 00).
    /// </summary>
    /// <param name="edid">EDID data (at least 8 bytes)</param>
    /// <returns>True if header is valid</returns>
    public static bool ValidateHeader(byte[] edid)
    {
        if (edid.Length < 8) return false;
        
        return edid[0] == 0x00 &&
               edid[1] == 0xFF &&
               edid[2] == 0xFF &&
               edid[3] == 0xFF &&
               edid[4] == 0xFF &&
               edid[5] == 0xFF &&
               edid[6] == 0xFF &&
               edid[7] == 0x00;
    }
}
