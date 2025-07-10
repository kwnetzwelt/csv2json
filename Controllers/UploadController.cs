using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;

namespace csv_to_json.Controllers;

public class UploadController : Controller
{
    // GET: Displays the HTML form for uploading the file.
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // POST: Handles the CSV upload, converts rows to JSON, and returns a ZIP archive.
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // Optional: Limit to 10 MB
    public async Task<IActionResult> UploadCsvFile(IFormFile csvFile, string outputFormat)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Ensure the uploaded file has a CSV extension.
        if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV files are accepted.");
        }

        using var memoryStream = new MemoryStream();
        await csvFile.CopyToAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        using var zipStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            using var reader = new StreamReader(memoryStream);
            var headers = ParseLine(reader);

            if (headers == null)
            {
                return BadRequest("Invalid CSV format.");
            }

            string? line;
            int fileIndex = 1;

            // Process CSV rows and convert them to the selected output format.
            while (!reader.EndOfStream
                  )
            {
                var values = ParseLine(reader);
                if (values == null || values.Count == 0)
                {
                    continue; // Skip blank or malformed rows
                }

                var content = string.Empty;
                if(values == null) continue;
                if (outputFormat == "json")
                {
                    var jsonObject = headers.Zip(values, (header, value) => new { header, value })
                                            .ToDictionary(x => x.header, x => x.value);

                    content = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (outputFormat == "txt")
                {
                    content = string.Join(Environment.NewLine, headers.Zip(values, (header, value) => $"{header}: {value}"));
                }

                // Add file to the ZIP archive.
                var fileExtension = outputFormat == "json" ? "json" : "txt";
                var zipEntry = zipArchive.CreateEntry($"row_{fileIndex}.{fileExtension}", CompressionLevel.Fastest);
                fileIndex++;

                using var entryStream = zipEntry.Open();
                using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync(content);
            }
        }

        // Reset the ZIP stream for returning as a file.
        zipStream.Seek(0, SeekOrigin.Begin);
        var zipFileName = $"{Path.GetFileNameWithoutExtension(csvFile.FileName)}.zip";
        return File(zipStream.ToArray(), "application/zip", zipFileName);
    }

    private List<string>? ParseLine(StreamReader reader)
    {
        // Store parsed line data.
        List<string>? lineContent = null;

        // Do not let TextFieldParser close the StreamReader.
        using var textReader = new StringReader(reader.ReadLine() ?? string.Empty);
        using (var parser = new TextFieldParser(textReader))
        {
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            try
            {
                if (!parser.EndOfData)
                {
                    lineContent = parser.ReadFields()?.ToList();
                }
            }
            catch (MalformedLineException ex)
            {
                // Handle malformed line or keep error logging.
                Console.WriteLine($"Malformed CSV line exception: {ex.Message}");
            }
        }

        return lineContent;
    }


}