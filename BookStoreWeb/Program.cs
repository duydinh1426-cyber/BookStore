namespace BookStoreWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            var options = new DefaultFilesOptions();
            options.DefaultFileNames.Clear();
            options.DefaultFileNames.Add("html/index.html");

            app.UseDefaultFiles(options);
            app.UseStaticFiles();

            app.Run();
        }
    }
}
