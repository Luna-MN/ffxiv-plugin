using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Utility;
using Dalamud.Data;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Game.Text.SeStringHandling;

using SamplePlugin.Windows;
using System;
using static Dalamud.Plugin.Services.IChatGui;
using Lumina;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/pmycommand";
    private static IPluginLog _logger { get; set; } = null!;
    private readonly IChatGui _chatGui;
    private readonly static string _filePath = "Lunainfo.csv";
    private static string filePath { get; set; } = null!;


    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private readonly IChatGui chatGui;
    private readonly IPluginLog logger;
    private ConfigWindow ConfigWindow { get; init; }

    private MainWindow MainWindow { get; init; }

    // Implement the method to handle incoming chat messages
    OnMessageDelegate messageDelegate = (XivChatType type, int senderId, ref SeString sender, ref SeString message, ref bool isHandled) =>
    {
        // Here you can process the chat message
        // For example, log the message to a file or perform actions based on message content
        // This is a basic example that prints the message to the debug console

        _logger.Information($"Chat message received: {message} from {sender} in chat type {type}, enum {(int)type}");
        if(type == (XivChatType)2112)
        {
            // 2112 is the chat type for Duty kill times
            // Extract the last 5 characters of the string
            
            string time = message.ToString().Substring(message.ToString().Length - 5);
            string inputString = message.ToString();
            string keyword = "completion";
            int index = inputString.IndexOf(keyword);

            string extractedData = index >= 0 ? inputString.Substring(0, index) : inputString;

            //debug log
            _logger.Information($"this is a kill of {extractedData} time of time {time}");

            // read the file and check if the entry exists
            string[] lines = File.ReadAllLines(filePath);
            bool entryExists = false;

            // check if the entry exists
            foreach (string line in lines)
            {
                if (line.Contains(extractedData))
                {
                   entryExists = true;
                   lines[Array.IndexOf(lines, line) + 1] = (int.Parse(lines[Array.IndexOf(lines, line) + 1]) + 1).ToString();
                   if(int.Parse(lines[Array.IndexOf(lines, line) + 2]) > int.Parse(time))
                   {
                       lines[Array.IndexOf(lines, line) + 2] = time;
                   }
                   break;
                }
            }
            // if the entry does not exist, write it to the file
            if (!entryExists)
            {
                using (StreamWriter sw = File.AppendText(filePath))
                {
                    sw.WriteLine(extractedData);
                    sw.WriteLine(1);
                    sw.WriteLine(time);
                }
            }


        }
    };
    public Plugin(IPluginLog logger, IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();



        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");
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
            HelpMessage = "A useful message to display in /xlhelp"
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
    // Add this property to your Plugin class to access the ChatGui service

}
