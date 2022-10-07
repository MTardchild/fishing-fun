using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FishingFun
{
    public partial class KeyBindChooser : UserControl
    {
        public ConsoleKey Key { get; set; } = ConsoleKey.D4;

        private static string Filename = "keybind.txt";

        public EventHandler KeyChanged;

        public KeyBindChooser()
        {
            KeyChanged += (s, e) => { };

            InitializeComponent();
            ReadConfiguration();
        }

        private void ReadConfiguration()
        {
            try
            {
                if (File.Exists(Filename))
                {
                    var contents = File.ReadAllText(Filename);
                    Key = (ConsoleKey)int.Parse(contents);
                    KeyBind.Text = GetCastKeyText(this.Key);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                Key = ConsoleKey.D4;
                KeyBind.Text = GetCastKeyText(this.Key);
            }
        }

        private void WriteConfiguration()
        {
            File.WriteAllText(Filename, ((int)Key).ToString());
        }

        private void CastKey_Focus(object sender, RoutedEventArgs e)
        {
            KeyBind.Text = "";
        }

        private void KeyBind_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key.ToString();
            ProcessKeybindText(key);
        }

        private void ProcessKeybindText(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                ConsoleKey ck;
                if (Enum.TryParse<ConsoleKey>(key, out ck))
                {
                    this.Key = ck;
                    WriteConfiguration();
                    KeyChanged?.Invoke(this, null);
                    return;
                }
            }
            KeyBind.Text = "";
        }

        private string GetCastKeyText(ConsoleKey ck)
        {
            string keyText = ck.ToString();
            if (keyText.Length == 1) { return keyText; }
            if (keyText.StartsWith("D") && keyText.Length == 2)
            {
                return keyText.Substring(1, 1);
            }
            return "?";
        }
    }
}