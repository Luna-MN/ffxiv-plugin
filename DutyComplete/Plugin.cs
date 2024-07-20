using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using DutyComplete.Windows;
using System;
using static Dalamud.Plugin.Services.IChatGui;

namespace DutyComplete;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/DC";
    private static IPluginLog _logger { get; set; } = null!;
    private static IChatGui _chatGui { get; set; } = null!;
    private readonly static string _filePath = "Lunainfo.csv";
    private static string filePath { get; set; } = null!;
    private static string[] entries { get; set; } = null!;
    private static bool dataWritten { get; set; } = false;
    private static string time { get; set; } = null!;
    private static string timePB { get; set; } = null!;
    private static string kills { get; set; } = null!;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("DutyComplete");
    private readonly IChatGui chatGui;
    private readonly IPluginLog logger;
    private ConfigWindow ConfigWindow { get; init; }

    private MainWindow MainWindow { get; init; }

    // Here is the modified code to remove spaces from time

    // Implement the method to handle incoming chat messages
    OnMessageDelegate messageDelegate = (XivChatType type, int senderId, ref SeString sender, ref SeString message, ref bool isHandled) =>
    {
        // Here you can process the chat message
        // For example, log the message to a file or perform actions based on message content
        // This is a basic example that prints the message to the debug console

        _logger.Information($"Chat message received: {message} from {sender} in chat type {type}, enum {(int)type}");
        if (type == (XivChatType)2112 && message.TextValue.Contains("completion"))
        {
            // 2112 is the chat type for Duty kill times
            // Extract the last 5 characters of the string

            time = message.ToString().Substring(message.ToString().Length - 6, 5);
            time = time.Replace(":", ".").Replace(" ", "");

            string inputString = message.ToString();
            string keyword = "completion";
            int index = inputString.IndexOf(keyword);

            string extractedData = index >= 0 ? inputString.Substring(0, index) : inputString;
            //debug log
            _logger.Information($"this is a kill of {extractedData} time of time {time}");

            // read the file and check if the entry exists
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line = sr.ReadLine();
                if (line == null || line == "")
                {
                    dataWritten = false;
                }
                else
                { 
                    dataWritten = true;
                    entries = line.Split(',');
                }
            }
            bool entryExists = false;

            // check if the entry exists
            if (dataWritten)
            {
                foreach (string line in entries)
                {
                    if (line.Contains(extractedData))
                    {
                        
                        kills = (float.Parse(entries[Array.IndexOf(entries, line) + 1]) + 1).ToString();
                        entries[Array.IndexOf(entries, line) + 1] = kills;
                        if (float.Parse(entries[Array.IndexOf(entries, line) + 2]) > float.Parse(time))
                        {
                            entries[Array.IndexOf(entries, line) + 2] = time;
                            timePB = time;
                        }
                        else
                        {
                            timePB = entries[Array.IndexOf(entries, line) + 2];
                        }
                        entryExists = true;
                    }
                }
                DeleteAllEntries();
                CreateFileWithEntries(entries);
            }
            // if the entry does not exist, write it to the file
            if (!entryExists)
            {
                using (StreamWriter sw = File.AppendText(filePath))
                {
                    sw.Write($",{extractedData},{1},{time}");
                    dataWritten = true;
                }
                kills = "1";
                timePB = time;
            }
            _chatGui.Print($"Duty completed, fastest completion: {timePB.Replace(".", ":")}, you've killed this: {kills} times", "Complete");
        }
        

    };
    public Plugin(IPluginLog logger, IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();



        // you might normally want to embed resources and load them from the manifest stream
        filePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, _filePath);
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        // create the config file if it doesn't exist
        if (!File.Exists(filePath))
        {
            _logger.Information("Creating the file");
            File.Create(filePath).Close();
        }
     
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Plugin to track duty completions!"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        _chatGui = ChatGui!;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Subscribe to the Chat event
        _chatGui.ChatMessage += messageDelegate;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        _logger?.Information($"Command '{command}' invoked with arguments: {args}");
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    // Add this method to your Plugin class
    private static void DeleteAllEntries()
    {
        // Delete the file if it exists
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    private static void CreateFileWithEntries(string[] Entries)
    {
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            using(StreamWriter sw = File.AppendText(filePath))
            {
                foreach (string entry in Entries)
                {
                    sw.Write($"{entry},");
                }
            }
        }
    }
    // Add this property to your Plugin class to access the ChatGui service

}
