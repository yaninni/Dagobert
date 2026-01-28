using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dagobert.Windows;
using ECommons;

namespace Dagobert;

public sealed class Plugin : IDalamudPlugin
{
    public static Plugin Instance { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IMarketBoard MarketBoard { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;

#pragma warning disable CS8618
    public static Configuration Configuration { get; private set; }
    public static DalamudLinkPayload ConfigLinkPayload { get; private set; } = null!;
#pragma warning restore CS8618

    private readonly AutoPinch _autoPinch;
    private readonly SalesMonitor _salesMonitor;
    private readonly ContextMenuIntegration _contextMenuIntegration;

    public readonly WindowSystem WindowSystem = new("Dagobert");
    public ConfigWindow ConfigWindow { get; init; }
    public ItemInspector ItemInspector { get; init; }

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow();
        WindowSystem.AddWindow(ConfigWindow);
        ItemInspector = new ItemInspector();
        WindowSystem.AddWindow(ItemInspector);

        CommandManager.AddHandler("/dagobert", new CommandInfo(OnDagobertCommand)
        {
            HelpMessage = "Opens the Dagobert configuration window"
        });

        ConfigLinkPayload = ChatGui.AddChatLinkHandler(0, (id, _) => ToggleConfigUI());

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        ECommonsMain.Init(PluginInterface, this);
        _autoPinch = new AutoPinch();
        WindowSystem.AddWindow(_autoPinch);

        _salesMonitor = new SalesMonitor();
        _contextMenuIntegration = new ContextMenuIntegration();
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        
        _contextMenuIntegration.Dispose();
        WindowSystem.RemoveAllWindows();
        _autoPinch.Dispose();
        _salesMonitor.Dispose();
        CommandManager.RemoveHandler("/dagobert");
        ECommonsMain.Dispose();
        DiscordSender.Dispose();
    }

    private void OnDagobertCommand(string command, string args)
    {
        ToggleConfigUI();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
