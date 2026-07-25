using System.Text.Json;
using iTunesRPC;

var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
var config = LoadConfig(configPath);

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.Run(new TrayAppContext(config, configPath));

static AppConfig LoadConfig(string path)
{
    if (!File.Exists(path))
    {
        var defaultConfig = new AppConfig();
        File.WriteAllText(path, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }

    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
}
