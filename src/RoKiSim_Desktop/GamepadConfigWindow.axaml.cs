using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace RoKiSim_Desktop
{
    public partial class GamepadConfigWindow : Window
    {
        public GamepadConfigWindow()
        {
            InitializeComponent();
            PopulateComboBoxes();
            LoadSettings();

            var btnSave = this.FindControl<Button>("btnSave");
            if (btnSave != null)
                btnSave.Click += BtnSave_Click;
        }

        private void PopulateComboBoxes()
        {
            var actions = Enum.GetValues(typeof(GamepadAction)).Cast<GamepadAction>().Select(a => a.ToString()).ToArray();
            
            var combos = new[] { "cmbAxis0", "cmbAxis1", "cmbAxis3", "cmbAxis4", "cmbBtn0", "cmbBtn4", "cmbBtn5" };
            foreach(var c in combos)
            {
                var cmb = this.FindControl<ComboBox>(c);
                if (cmb != null)
                {
                    cmb.ItemsSource = actions;
                }
            }
        }

        private void LoadSettings()
        {
            SetCombo("cmbAxis0", "Axis0");
            SetCombo("cmbAxis1", "Axis1");
            SetCombo("cmbAxis3", "Axis3");
            SetCombo("cmbAxis4", "Axis4");
            SetCombo("cmbBtn0", "Button0");
            SetCombo("cmbBtn4", "Button4");
            SetCombo("cmbBtn5", "Button5");
        }

        private void SetCombo(string comboName, string key)
        {
            var cmb = this.FindControl<ComboBox>(comboName);
            if (cmb != null && GamepadSettings.Instance.Mapping.ContainsKey(key))
            {
                cmb.SelectedItem = GamepadSettings.Instance.Mapping[key].ToString();
            }
            else if (cmb != null)
            {
                cmb.SelectedItem = GamepadAction.None.ToString();
            }
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            SaveCombo("cmbAxis0", "Axis0");
            SaveCombo("cmbAxis1", "Axis1");
            SaveCombo("cmbAxis3", "Axis3");
            SaveCombo("cmbAxis4", "Axis4");
            SaveCombo("cmbBtn0", "Button0");
            SaveCombo("cmbBtn4", "Button4");
            SaveCombo("cmbBtn5", "Button5");

            this.Close();
        }

        private void SaveCombo(string comboName, string key)
        {
            var cmb = this.FindControl<ComboBox>(comboName);
            if (cmb != null && cmb.SelectedItem != null)
            {
                if (Enum.TryParse<GamepadAction>(cmb.SelectedItem.ToString(), out var action))
                {
                    GamepadSettings.Instance.Mapping[key] = action;
                }
            }
        }
    }
}
