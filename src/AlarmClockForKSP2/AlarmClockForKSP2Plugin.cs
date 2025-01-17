﻿using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using UnityEngine.UIElements;
using UitkForKsp2.API;
using KSP.Messages;
using AlarmClockForKSP2.Managers;
using KSP.Game;

namespace AlarmClockForKSP2;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class AlarmClockForKSP2Plugin : BaseSpaceWarpPlugin
{
    // Useful in case some other mod wants to use this mod a dependency
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    // AppBar button IDs
    internal const string ToolbarFlightButtonID = "BTN-AlarmClockForKSP2Flight";
    internal const string ToolbarOabButtonID = "BTN-AlarmClockForKSP2OAB";
    internal const string ToolbarKscButtonID = "BTN-AlarmClockForKSP2KSC";

    // Singleton instance of the plugin class
    public static AlarmClockForKSP2Plugin Instance { get; set; }

    // Window Controller Reference
    internal WindowController AlarmWindowController;
    internal AlertController AlertWindowController;

    internal bool GameStateValid = false;

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        MessageManager.InitializeMessageCenter();

        // Import uxml and set up the window
        VisualTreeAsset uxml = AssetManager.GetAsset<VisualTreeAsset>($"{ModGuid}/" + "alarmclock-resources/UI/MainWindow.uxml");

        WindowOptions windowOptions = new WindowOptions
        {
            WindowId = "AlarmClockForKSP2_Window",

            Parent = null,

            IsHidingEnabled = true,

            DisableGameInputForTextFields = true,

            MoveOptions = new MoveOptions
            {
                IsMovingEnabled = true,
                CheckScreenBounds = true,
            }
        };

        UIDocument alarmWindow = Window.Create(windowOptions, uxml);

        VisualTreeAsset alertuxml = AssetManager.GetAsset<VisualTreeAsset>($"{ModGuid}/" + "alarmclock-resources/UI/AlertWindow.uxml");

        WindowOptions alertWindowOptions = new WindowOptions
        {
            WindowId = "AlarmClockForKSP2_Alert",

            Parent = null,

            IsHidingEnabled = true,

            DisableGameInputForTextFields = true,

            MoveOptions = new MoveOptions
            {
                IsMovingEnabled = false,
                CheckScreenBounds = true,
            }
        };

        UIDocument alertWindow = Window.Create(alertWindowOptions, alertuxml);

        AlarmWindowController = alarmWindow.gameObject.AddComponent<WindowController>();
        AlertWindowController = alertWindow.gameObject.AddComponent<AlertController>();

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            ModName,
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
            isOpen => AlarmWindowController.IsWindowOpen = isOpen
        );

        // Register OAB AppBar Button
        Appbar.RegisterOABAppButton(
            ModName,
            ToolbarOabButtonID,
            AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
            isOpen => AlarmWindowController.IsWindowOpen = isOpen
        );

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(AlarmClockForKSP2Plugin).Assembly);

        // Try to get the currently active vessel, set its throttle to 100% and toggle on the landing gear
        try
        {
            var currentVessel = Vehicle.ActiveVesselVehicle;
            if (currentVessel != null)
            {
                currentVessel.SetMainThrottle(1.0f);
                currentVessel.SetGearState(true);
            }
        }
        catch (Exception){}

        // Fetch a configuration value or create a default one if it does not exist
        const string defaultValue = "my default value";
        var configValue = Config.Bind<string>("Settings section", "Option 1", defaultValue, "Option description");

        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Option 1: {configValue.Value}");
        Testing.SubscribeToMessages();

        PersistentDataManager.InititializePersistentDataManager(SpaceWarpPlugin.ModGuid);

        LinkManagersToMessages();

    }

    private void LinkManagersToMessages()
    {
        MessageManager.MessageCenter.PersistentSubscribe<GameStateChangedMessage>(HideWindowOnInvalidState);

        MessageManager.MessageCenter.PersistentSubscribe<ManeuverCreatedMessage>(SimulationManager.UpdateCurrentManeuver);
        MessageManager.MessageCenter.PersistentSubscribe<ManeuverMessageBase>(SimulationManager.UpdateCurrentManeuver);

        //Add if ManeuverMessageBase doesn't work
        //MessageManager.MessageCenter.PersistentSubscribe<ManeuverRemovedMessage>(SimulationManager.UpdateCurrentManeuver);
        //MessageManager.MessageCenter.PersistentSubscribe<ManeuverFinishedMessage>(SimulationManager.UpdateCurrentManeuver);

        MessageManager.MessageCenter.PersistentSubscribe<ActiveVesselDestroyedMessage>(SimulationManager.UpdateCurrentManeuver);
        MessageManager.MessageCenter.PersistentSubscribe<GameStateChangedMessage>(SimulationManager.UpdateCurrentManeuver);
        MessageManager.MessageCenter.PersistentSubscribe<VesselChangedMessage>(SimulationManager.UpdateCurrentManeuver);

        MessageManager.MessageCenter.PersistentSubscribe<SOIChangePredictedMessage>(SimulationManager.UpdateSOIChangePrediction);

    }

    public void HideWindowOnInvalidState(MessageCenterMessage obj)
    {
        GameStateValid = GameStateManager.IsGameStateValid();
        if (!GameStateValid)
        {
            AlarmWindowController.IsWindowOpen = false;
        }
        AlarmClockForKSP2Plugin.Instance.SWLogger.LogMessage($"Game state validity: {GameStateValid}");
    }

    public void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown("`") && GameStateValid)
        {
            AlarmWindowController.IsWindowOpen = !AlarmWindowController.IsWindowOpen;
        }
        if (GameManager.Instance?.Game?.UniverseModel is not null && GameManager.Instance?.Game?.ViewController?.TimeWarp is not null)
            TimeManager.Instance.Update();
    }

    public void OpenMainWindow()
    {
        AlarmWindowController.IsWindowOpen = true;
    }

    public void CreateAlert(string title)
    {
        AlertWindowController.DisplayAlert(title);
    }

}
