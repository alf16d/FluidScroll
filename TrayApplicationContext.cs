using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FluidScroll
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly FluidScrollEngine _engine;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _autoStartMenuItem;
        private AppSettings _settings;
        private SettingsForm? _settingsForm;

        public TrayApplicationContext()
        {
            _settings = AppSettings.Load();
            _engine = new FluidScrollEngine();
            _settings.ApplyTo(_engine);
            try
            {
                _engine.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"FluidScroll could not start smooth scrolling.\n\n{ex.Message}",
                    "FluidScroll",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            _enabledMenuItem = new ToolStripMenuItem("Smooth scroll")
            {
                Checked = _settings.SmoothScrollEnabled,
                CheckOnClick = false
            };
            _enabledMenuItem.Click += (_, _) => ToggleSmoothScroll();

            _autoStartMenuItem = new ToolStripMenuItem("Auto start")
            {
                Checked = StartupManager.IsEnabled,
                CheckOnClick = false
            };
            _autoStartMenuItem.Click += (_, _) => ToggleAutoStart();

            var settingsMenuItem = new ToolStripMenuItem("Settings");
            settingsMenuItem.Click += (_, _) => ShowSettings();

            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (_, _) => ExitThread();

            var menu = new ContextMenuStrip
            {
                RenderMode = ToolStripRenderMode.System
            };
            menu.Items.Add(settingsMenuItem);
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add(_autoStartMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitMenuItem);

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Icon = LoadTrayIcon(),
                Text = "FluidScroll",
                Visible = true
            };
            _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        }

        private void ToggleSmoothScroll()
        {
            _settings.SmoothScrollEnabled = !_settings.SmoothScrollEnabled;
            _settings.ApplyTo(_engine);
            _settings.Save();
            RefreshMenuState();
        }

        private void ToggleAutoStart()
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled);
            RefreshMenuState();
        }

        private void ShowSettings()
        {
            if (_settingsForm != null)
            {
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new SettingsForm(_settings, StartupManager.IsEnabled);
            _settingsForm.SettingsSaved += OnSettingsSaved;
            _settingsForm.FormClosed += (_, _) =>
            {
                if (_settingsForm != null)
                {
                    _settingsForm.SettingsSaved -= OnSettingsSaved;
                    _settingsForm = null;
                }
            };
            _settingsForm.Show();
        }

        private void OnSettingsSaved(object? sender, SettingsSavedEventArgs e)
        {
            _settings = e.Settings.Clone();
            _settings.ApplyTo(_engine);
            _settings.Save();
            StartupManager.SetEnabled(e.AutoStartEnabled);
            RefreshMenuState();
        }

        private void RefreshMenuState()
        {
            _enabledMenuItem.Checked = _settings.SmoothScrollEnabled;
            _autoStartMenuItem.Checked = StartupManager.IsEnabled;
        }

        protected override void ExitThreadCore()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _settingsForm?.Close();
            _engine.Dispose();
            base.ExitThreadCore();
        }

        private static Icon LoadTrayIcon()
        {
            string bundledIcon = Path.Combine(AppContext.BaseDirectory, "imgs", "icon_256.ico");
            if (File.Exists(bundledIcon))
            {
                return new Icon(bundledIcon);
            }

            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                Icon? extractedIcon = Icon.ExtractAssociatedIcon(processPath);
                if (extractedIcon != null)
                {
                    return extractedIcon;
                }
            }

            return SystemIcons.Application;
        }
    }
}
