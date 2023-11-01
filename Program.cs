using HttpClientGenerator;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please, input swagger.json URL");
            return;
        }

        var url = args[0];
        var swaggerContent = await DownloadSwaggerJson(url);
        var fileName = "swagger.json";
        File.WriteAllText(fileName, swaggerContent);

        var generator = new SwaggerToCSharpGenerator(fileName);
        generator.Generate();
    }

    public static async Task<string> DownloadSwaggerJson(string url)
    {
        using (var httpClient = new HttpClient())
        {
            return await httpClient.GetStringAsync(url);
        }
    }
}
