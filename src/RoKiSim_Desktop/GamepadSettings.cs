using System.Collections.Generic;

namespace RoKiSim_Desktop
{
    public enum GamepadAction
    {
        None,
        MoveX,
        MoveY,
        MoveZ,
        MoveJ1,
        MoveJ2,
        MoveJ3,
        MoveJ4,
        MoveJ5,
        MoveJ6,
        MoveJ7,
        GripperOpen,
        GripperClose,
        HomePosition
    }

    public class GamepadSettings
    {
        private static GamepadSettings? _instance;
        public static GamepadSettings Instance => _instance ??= new GamepadSettings();

        // Dictionary to map input keys to actions
        // Keys: "Axis0", "Axis1", "Axis2", "Axis3", "Axis4", "Axis5"
        //       "Button0", "Button1", "Button2", "Button3", "Button4", "Button5"
        public Dictionary<string, GamepadAction> Mapping { get; set; } = new Dictionary<string, GamepadAction>();

        public GamepadSettings()
        {
            // Default mappings
            Mapping["Axis0"] = GamepadAction.MoveX;
            Mapping["Axis1"] = GamepadAction.MoveY;
            Mapping["Axis3"] = GamepadAction.MoveJ7;
            Mapping["Axis4"] = GamepadAction.MoveZ;
            
            Mapping["Button0"] = GamepadAction.HomePosition; // A
            Mapping["Button4"] = GamepadAction.GripperClose; // LB
            Mapping["Button5"] = GamepadAction.GripperOpen;  // RB
        }

        public GamepadAction GetActionForAxis(int axisIndex)
        {
            string key = $"Axis{axisIndex}";
            return Mapping.ContainsKey(key) ? Mapping[key] : GamepadAction.None;
        }

        public GamepadAction GetActionForButton(int buttonIndex)
        {
            string key = $"Button{buttonIndex}";
            return Mapping.ContainsKey(key) ? Mapping[key] : GamepadAction.None;
        }
    }
}
