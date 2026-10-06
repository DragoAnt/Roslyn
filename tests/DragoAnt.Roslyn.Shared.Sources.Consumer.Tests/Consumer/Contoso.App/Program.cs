namespace Contoso.App;

public static class Api
{
    public static string Select(string path) => path;
}

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine(Api.Select("orders total"));
        System.Console.WriteLine(GeneratedNames.All);
    }
}
