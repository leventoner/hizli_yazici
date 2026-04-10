using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

public class Program
{
    public static async Task Main()
    {
        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            // Try reading from .env manually
            if (File.Exists(".env"))
            {
                foreach(var line in File.ReadAllLines(".env"))
                {
                    if (line.Trim().StartsWith("GEMINI_API_KEY="))
                    {
                        apiKey = line.Substring("GEMINI_API_KEY=".Length).Trim().Trim('\"', '\'');
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("API KEY NOT FOUND");
            return;
        }

        using var client = new HttpClient();
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        
        try
        {
            var response = await client.GetStringAsync(url);
            Console.WriteLine(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
