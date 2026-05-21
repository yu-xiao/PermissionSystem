using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Xml.Linq;

namespace PermissionSystem.Application.Excels;

public sealed class ExcelService : IExcelService
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public Task<byte[]> ExportAsync<T>(ExportRequest<T> request, CancellationToken cancellationToken = default)
        where T : class
    {
        var columns = GetColumns<T>();
        var rows = new List<IReadOnlyList<string?>>
        {
            columns.Select(column => column.Header).ToList()
        };

        rows.AddRange(request.Items.Select(item =>
            columns.Select(column => FormatValue(column.Property.GetValue(item))).ToList()));

        return Task.FromResult(CreateWorkbook(request.SheetName, rows));
    }

    public Task<ImportResult<T>> ImportAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var columns = GetColumns<T>();
        var columnMap = columns.ToDictionary(column => column.Header, StringComparer.OrdinalIgnoreCase);
        var rows = ReadRows(stream);
        var items = new List<T>();
        var errors = new List<ImportError>();

        if (rows.Count == 0)
        {
            return Task.FromResult(new ImportResult<T>
            {
                TotalRows = 0,
                SuccessRows = 0,
                FailedRows = 0,
                Items = items,
                Errors = errors
            });
        }

        var headers = rows[0];
        var headerIndexes = headers
            .Select((header, index) => new { Header = header, Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToDictionary(item => item.Header.Trim(), item => item.Index, StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var item = new T();
            var rowErrors = new List<ImportError>();

            foreach (var column in columnMap.Values)
            {
                headerIndexes.TryGetValue(column.Header, out var cellIndex);
                var hasHeader = headerIndexes.ContainsKey(column.Header);
                var rawValue = hasHeader && cellIndex < row.Count ? row[cellIndex] : null;

                if (column.Required && string.IsNullOrWhiteSpace(rawValue))
                {
                    rowErrors.Add(new ImportError
                    {
                        RowNumber = rowIndex + 1,
                        ColumnName = column.Header,
                        Message = "Value is required.",
                        RawValue = rawValue
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                if (!TryConvert(rawValue, column.Property.PropertyType, out var convertedValue, out var message))
                {
                    rowErrors.Add(new ImportError
                    {
                        RowNumber = rowIndex + 1,
                        ColumnName = column.Header,
                        Message = message,
                        RawValue = rawValue
                    });
                    continue;
                }

                column.Property.SetValue(item, convertedValue);
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            items.Add(item);
        }

        return Task.FromResult(new ImportResult<T>
        {
            TotalRows = rows.Count - 1,
            SuccessRows = items.Count,
            FailedRows = errors.Select(error => error.RowNumber).Distinct().Count(),
            Items = items,
            Errors = errors
        });
    }

    public Task<byte[]> CreateTemplateAsync<T>(string sheetName = "Template", CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var headers = GetColumns<T>().Select(column => (string?)column.Header).ToList();
        return Task.FromResult(CreateWorkbook(sheetName, [headers]));
    }

    private static byte[] CreateWorkbook(string sheetName, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", CreateContentTypesXml());
            WriteEntry(archive, "_rels/.rels", CreateRootRelationshipsXml());
            WriteEntry(archive, "xl/workbook.xml", CreateWorkbookXml(sheetName));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationshipsXml());
            WriteEntry(archive, "xl/styles.xml", CreateStylesXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", CreateWorksheetXml(rows));
        }

        return memoryStream.ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadRows(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Excel worksheet was not found.");

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        var result = new List<IReadOnlyList<string>>();

        foreach (var row in document.Descendants(MainNs + "row"))
        {
            var values = new Dictionary<int, string>();
            var maxIndex = -1;

            foreach (var cell in row.Elements(MainNs + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                var columnIndex = string.IsNullOrWhiteSpace(reference)
                    ? maxIndex + 1
                    : GetColumnIndex(reference);
                maxIndex = Math.Max(maxIndex, columnIndex);
                values[columnIndex] = GetCellValue(cell, sharedStrings);
            }

            var rowValues = Enumerable.Range(0, maxIndex + 1)
                .Select(index => values.TryGetValue(index, out var value) ? value : string.Empty)
                .ToList();
            result.Add(rowValues);
        }

        return result;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(MainNs + "si")
            .Select(item => string.Concat(item.Descendants(MainNs + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
        {
            return cell.Element(MainNs + "is")?.Element(MainNs + "t")?.Value ?? string.Empty;
        }

        var value = cell.Element(MainNs + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(value, out var sharedStringIndex) && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var index = 0;
        foreach (var letter in letters)
        {
            index = (index * 26) + (letter - 'A' + 1);
        }

        return index - 1;
    }

    private static string CreateWorksheetXml(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var sheetData = new XElement(MainNs + "sheetData");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            var rowElement = new XElement(MainNs + "row", new XAttribute("r", rowNumber));
            var row = rows[rowIndex];

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                var cellReference = $"{GetColumnName(columnIndex)}{rowNumber}";
                rowElement.Add(new XElement(
                    MainNs + "c",
                    new XAttribute("r", cellReference),
                    new XAttribute("t", "inlineStr"),
                    new XElement(
                        MainNs + "is",
                        new XElement(MainNs + "t", row[columnIndex] ?? string.Empty))));
            }

            sheetData.Add(rowElement);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "worksheet", sheetData)).ToString(SaveOptions.DisableFormatting);
    }

    private static string CreateWorkbookXml(string sheetName)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                MainNs + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", RelationshipNs),
                new XElement(
                    MainNs + "sheets",
                    new XElement(
                        MainNs + "sheet",
                        new XAttribute("name", SanitizeSheetName(sheetName)),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(RelationshipNs + "id", "rId1"))))).ToString(SaveOptions.DisableFormatting);
    }

    private static string CreateContentTypesXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """;
    }

    private static string CreateRootRelationshipsXml()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                PackageRelationshipNs + "Relationships",
                new XElement(
                    PackageRelationshipNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml")))).ToString(SaveOptions.DisableFormatting);
    }

    private static string CreateWorkbookRelationshipsXml()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                PackageRelationshipNs + "Relationships",
                new XElement(
                    PackageRelationshipNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(
                    PackageRelationshipNs + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml")))).ToString(SaveOptions.DisableFormatting);
    }

    private static string CreateStylesXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
              <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
            </styleSheet>
            """;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string GetColumnName(int index)
    {
        var dividend = index + 1;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static IReadOnlyList<ExcelColumn> GetColumns<T>()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new { Property = property, Attribute = property.GetCustomAttribute<ExcelColumnAttribute>() })
            .Where(item => item.Attribute is not null && item.Property.CanRead && item.Property.CanWrite)
            .OrderBy(item => item.Attribute!.Order)
            .ThenBy(item => item.Property.Name)
            .Select(item => new ExcelColumn(
                item.Property,
                item.Attribute!.Header,
                item.Attribute.Required))
            .ToList();
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static bool TryConvert(string rawValue, Type propertyType, out object? value, out string message)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var trimmedValue = rawValue.Trim();

        try
        {
            if (targetType == typeof(string))
            {
                value = trimmedValue;
            }
            else if (targetType == typeof(Guid))
            {
                value = Guid.Parse(trimmedValue);
            }
            else if (targetType == typeof(bool))
            {
                value = ParseBoolean(trimmedValue);
            }
            else if (targetType == typeof(DateTime))
            {
                value = DateTime.Parse(trimmedValue, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(DateTimeOffset))
            {
                value = DateTimeOffset.Parse(trimmedValue, CultureInfo.InvariantCulture);
            }
            else if (targetType.IsEnum)
            {
                value = Enum.Parse(targetType, trimmedValue, ignoreCase: true);
            }
            else
            {
                value = Convert.ChangeType(trimmedValue, targetType, CultureInfo.InvariantCulture);
            }

            message = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException)
        {
            value = null;
            message = $"Value cannot be converted to {targetType.Name}.";
            return false;
        }
    }

    private static bool ParseBoolean(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "enabled" => true,
            "false" or "0" or "no" or "n" or "disabled" => false,
            _ => bool.Parse(value)
        };
    }

    private static string SanitizeSheetName(string sheetName)
    {
        var invalidCharacters = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = invalidCharacters.Aggregate(sheetName, (current, character) => current.Replace(character, ' '));
        sanitized = SecurityElement.Escape(sanitized.Trim()) ?? "Sheet1";
        return string.IsNullOrWhiteSpace(sanitized) ? "Sheet1" : sanitized[..Math.Min(31, sanitized.Length)];
    }

    private sealed record ExcelColumn(PropertyInfo Property, string Header, bool Required);
}
