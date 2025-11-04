using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TOW2Trainer.Input;
using Button = System.Windows.Controls.Button;

namespace TOW2Trainer.UI
{
    public partial class MainWindow : Window
    {
        private readonly Logic.TOW2Logic Logic;
        private GlobalKeyboardHook kbHook;
        private readonly float[] FlySpeedMults = new float[4] { 1f, 2f, 4f, 0.5f };
        private bool shouldAcceptKeystrokes = true;
        private readonly Dictionary<string, Key> defaultKeybinds = new Dictionary<string, Key>()
        {
            { "god", Key.F1 },
            { "noclip", Key.F2 },
            { "speed", Key.F3 },
            { "store", Key.F6 },
            { "teleport", Key.F7 },
            { "volumes", Key.F10 }
        };
        private Dictionary<string, Key> keybinds = [];
        private Dictionary<Key, Action> keybindActions;


        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;
            InitializeKeyboardHook();
            Logic = new Logic.TOW2Logic();
            DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = new TimeSpan(10 * 10000) };
            timer.Tick += UIUpdateTick;
            timer.Start();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This enables dragging the window from anywhere
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void closeBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.ShouldAbort = true;
            kbHook.unhook();
            Application.Current.Shutdown();
        }

        public void SetKeybinds(Dictionary<string, Key> newKeybinds)
        {
            kbHook.HookedKeys.Clear();
            keybindActions.Clear();
            string keybindStore = "";

            foreach (KeyValuePair<string, Key> keybind in newKeybinds)
            {
                switch (keybind.Key)
                {
                    case "god":
                        keybindActions.Add(keybind.Value, () => godBtn_Click(null, null));
                        SetKeybindText(godBtn, keybind.Value);
                        keybindStore += "god,";
                        break;
                    case "noclip":
                        keybindActions.Add(keybind.Value, () => noclipBtn_Click(null, null));
                        SetKeybindText(noclipBtn, keybind.Value);
                        keybindStore += "noclip,";
                        break;
                    case "speed":
                        keybindActions.Add(keybind.Value, () => flySpeedBtn_Click(null, null));
                        SetKeybindText(flySpeedBtn, keybind.Value);
                        keybindStore += "speed,";
                        break;
                    case "store":
                        keybindActions.Add(keybind.Value, () => saveBtn_Click(null, null));
                        SetKeybindText(saveBtn, keybind.Value);
                        keybindStore += "store,";
                        break;
                    case "teleport":
                        keybindActions.Add(keybind.Value, () => teleBtn_Click(null, null));
                        SetKeybindText(teleBtn, keybind.Value);
                        keybindStore += "teleport,";
                        break;
                    case "volumes":
                        keybindActions.Add(keybind.Value, () => volumesBtn_Click(null, null));
                        SetKeybindText(toggleVolumesBtn, keybind.Value);
                        keybindStore += "volumes,";
                        break;
                    default:
                        break;
                }
                kbHook.HookedKeys.Add(keybind.Value);
                keybindStore += (int)keybind.Value + ",";
            }
            keybindStore = keybindStore.Substring(0, keybindStore.LastIndexOf(","));
            keybinds = newKeybinds;
            try
            {
                File.WriteAllText("TOW2Trainer_Keybinds.cfg", keybindStore);
            }
            catch (UnauthorizedAccessException)
            {
                _ = MessageBox.Show("Keybindings could not be saved.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void InitializeKeyboardHook()
        {
            kbHook = new GlobalKeyboardHook();
            kbHook.KeyDown += HandleKeyDown;
            keybindActions = new Dictionary<Key, Action>();
            string keybindStore = "";
            if (File.Exists("TOW2Trainer_Keybinds.cfg"))
            {
                try
                {
                    keybindStore = File.ReadAllText("TOW2Trainer_Keybinds.cfg");
                }
                catch (Exception)
                {
                    MessageBox.Show("Keybindings could not be read.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }

                string[] keybindArray = keybindStore.Split(',');

                if (keybindArray.Length == defaultKeybinds.Count * 2)
                {
                    Dictionary<string, Key> savedKeybinds = new Dictionary<string, Key>();
                    for (int i = 0; i < defaultKeybinds.Count * 2; i += 2)
                    {
                        savedKeybinds.Add(keybindArray[i], (Key)int.Parse(keybindArray[i + 1]));
                    }
                    SetKeybinds(savedKeybinds);
                    return;
                }
            }
            SetKeybinds(defaultKeybinds);
        }

        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (!shouldAcceptKeystrokes)
            {
                return;
            }
            if (keybindActions.ContainsKey(e.Key))
            {
                keybindActions[e.Key]();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Logic.ShouldAbort = true;
            kbHook.unhook();
        }

        private void UIUpdateTick(object sender, EventArgs e)
        {
            positionBlock.Text =
                  (Logic.XPos / 100).ToString("0.00") + "\n"
                + (Logic.YPos / 100).ToString("0.00") + "\n"
                + (Logic.ZPos / 100).ToString("0.00");
            speedBlock.Text = Logic.Vel.ToString("0.00") + " m/s";
            zSpeedBlock.Text = Logic.ZVel.ToString("0.00") + " m/s";

            SetLabel(Logic.ShouldGod, godLabel);
            SetLabel(Logic.ShouldNoclip, noclipLabel);
            flySpeedLabel.Content = Logic.FlySpeedMult.ToString("0.0") + "x";

            toggleVolumesBtn.Visibility = (Logic.IsHooked && File.Exists(Logic.ExternalModuleName)) 
                ? Visibility.Visible : Visibility.Hidden;

            unlockConsoleBtn.Visibility = (toggleVolumesBtn.Visibility == Visibility.Visible 
                && !Logic.ConsoleEnabled) ? Visibility.Visible : Visibility.Hidden;
        }

        private void SetLabel(bool state, System.Windows.Controls.Label label)
        {
            label.Content = state ? "ON" : "OFF";
            label.Foreground = state ? Brushes.Green : Brushes.Red;
        }

        private void SetKeybindText(Button button, Key key)
        {
            string text = (string)button.Content;
            string keyName = key.ToString();
            if ((int)key >= 48 && (int)key <= 57)
            {
                keyName = keyName.Replace("D", "");
            }
            button.Content = Regex.Replace(text, "\\[.*\\]", "[" + keyName + "]");
        }

        private void teleBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.ShouldTeleport = true;
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.ShouldStore = true;
        }

        private void noclipBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.ShouldNoclip = !Logic.ShouldNoclip;
        }

        private void godBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.ShouldGod = !Logic.ShouldGod;
        }

        private void flySpeedBtn_Click(object sender, RoutedEventArgs e)
        {
            float old = Logic.FlySpeedMult;
            Logic.FlySpeedMult = FlySpeedMults[(Array.IndexOf(FlySpeedMults, old) + 1) % FlySpeedMults.Length];
        }

        private void volumesBtn_Click(object sender, RoutedEventArgs e)
        {
            if(toggleVolumesBtn.Visibility != Visibility.Hidden)
                Logic.ToggleVolumes();
        }


        private void editKeybindBtn_Click(object sender, RoutedEventArgs e)
        {
            var oldTopmost = this.Topmost;
            this.Topmost = false;
            shouldAcceptKeystrokes = false;

            KeybindWindow kbWindow = new KeybindWindow(this, keybinds);
            _ = kbWindow.ShowDialog();

            this.Topmost = oldTopmost;
            shouldAcceptKeystrokes = true;
        }

        private void topmostBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            topmostBtn.Content = this.Topmost ? "don't stay on top" : "stay on top";
        }

        private void speedLabel_Click(object sender, RoutedEventArgs e)
        {
            zSpeedLabel.Visibility = 1 - zSpeedLabel.Visibility;
            zSpeedBlock.Visibility = zSpeedLabel.Visibility;
        }

        private void unlockConsoleBtn_Click(object sender, RoutedEventArgs e)
        {
            Logic.UnlockConsole();
        }
    }
}
