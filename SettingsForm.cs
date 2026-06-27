using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FluidScroll
{
    internal sealed class SettingsSavedEventArgs : EventArgs
    {
        public SettingsSavedEventArgs(AppSettings settings, bool autoStartEnabled)
        {
            Settings = settings;
            AutoStartEnabled = autoStartEnabled;
        }

        public AppSettings Settings { get; }
        public bool AutoStartEnabled { get; }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly CheckBox _smoothScrollCheckBox = new();
        private readonly CheckBox _autoStartCheckBox = new();
        private readonly TrackBar _boostTrackBar = new();
        private readonly TrackBar _accelTrackBar = new();
        private readonly TrackBar _catchupTrackBar = new();
        private readonly TrackBar _frictionTrackBar = new();
        private readonly TrackBar _responseTrackBar = new();
        private readonly Label _boostValueLabel = new();
        private readonly Label _accelValueLabel = new();
        private readonly Label _catchupValueLabel = new();
        private readonly Label _frictionValueLabel = new();
        private readonly Label _responseValueLabel = new();

        public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;

        public SettingsForm(AppSettings settings, bool autoStartEnabled)
        {
            Text = "FluidScroll Settings";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 650);
            MinimumSize = new Size(520, 610);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(243, 243, 243);
            Font = new Font("Segoe UI Variable Text", 9F, FontStyle.Regular, GraphicsUnit.Point);

            BuildLayout();
            ApplySettingsToControls(settings, autoStartEnabled);
            WireValueUpdates();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(24),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            Controls.Add(root);

            var titleLabel = new Label
            {
                Text = "FluidScroll Settings",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 32, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(titleLabel, 0, 0);

            var generalPanel = CreatePanel();
            var generalLayout = CreateSectionLayout(2);
            generalPanel.Controls.Add(generalLayout);
            AddCheckRow(generalLayout, 0, _smoothScrollCheckBox, "Smooth scroll");
            AddCheckRow(generalLayout, 1, _autoStartCheckBox, "Start with Windows");
            root.Controls.Add(generalPanel, 0, 1);

            var scrollPanel = CreatePanel();
            var scrollLayout = CreateSectionLayout(5);
            scrollPanel.Controls.Add(scrollLayout);
            AddSliderRow(scrollLayout, 0, "Speed boost", _boostTrackBar, _boostValueLabel);
            AddSliderRow(scrollLayout, 1, "Acceleration", _accelTrackBar, _accelValueLabel);
            AddSliderRow(scrollLayout, 2, "Catch-up", _catchupTrackBar, _catchupValueLabel);
            AddSliderRow(scrollLayout, 3, "Friction", _frictionTrackBar, _frictionValueLabel);
            AddSliderRow(scrollLayout, 4, "Response window", _responseTrackBar, _responseValueLabel);
            root.Controls.Add(scrollPanel, 0, 2);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 16, 0, 0),
                BackColor = BackColor
            };

            var saveButton = CreateButton("Save", true);
            saveButton.Click += (_, _) => SaveAndClose();

            var cancelButton = CreateButton("Cancel", false);
            cancelButton.Click += (_, _) => Close();

            var defaultsButton = CreateButton("Defaults", false);
            defaultsButton.Click += (_, _) => ApplySettingsToControls(new AppSettings(), _autoStartCheckBox.Checked);

            actions.Controls.Add(saveButton);
            actions.Controls.Add(cancelButton);
            actions.Controls.Add(defaultsButton);
            root.Controls.Add(actions, 0, 3);
        }

        private static ModernPanel CreatePanel()
        {
            return new ModernPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(16),
                BackColor = Color.White,
                BorderColor = Color.FromArgb(225, 225, 225),
                CornerRadius = 8
            };
        }

        private static TableLayoutPanel CreateSectionLayout(int rows)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = rows,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < rows; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            }

            return layout;
        }

        private void AddCheckRow(TableLayoutPanel parent, int row, CheckBox checkBox, string text)
        {
            checkBox.Text = text;
            checkBox.AutoSize = false;
            checkBox.Dock = DockStyle.Fill;
            checkBox.FlatStyle = FlatStyle.System;
            checkBox.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular);
            checkBox.ForeColor = Color.FromArgb(32, 32, 32);
            parent.Controls.Add(checkBox, 0, row);
        }

        private void AddSliderRow(TableLayoutPanel parent, int row, string text, TrackBar trackBar, Label valueLabel)
        {
            var rowLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 4)
            };
            rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            rowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            rowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(32, 32, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };

            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular);
            valueLabel.ForeColor = Color.FromArgb(96, 96, 96);
            valueLabel.TextAlign = ContentAlignment.MiddleRight;

            trackBar.Dock = DockStyle.Fill;
            trackBar.TickStyle = TickStyle.None;
            trackBar.Margin = new Padding(0, 0, 0, 0);

            rowLayout.Controls.Add(label, 0, 0);
            rowLayout.Controls.Add(valueLabel, 1, 0);
            rowLayout.Controls.Add(trackBar, 0, 1);
            rowLayout.SetColumnSpan(trackBar, 2);

            parent.Controls.Add(rowLayout, 0, row);
        }

        private Button CreateButton(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = false,
                Size = new Size(104, 34),
                Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular),
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 1;
            button.BackColor = primary ? Color.FromArgb(0, 95, 184) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(32, 32, 32);
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 95, 184) : Color.FromArgb(210, 210, 210);
            return button;
        }

        private void ApplySettingsToControls(AppSettings settings, bool autoStartEnabled)
        {
            _smoothScrollCheckBox.Checked = settings.SmoothScrollEnabled;
            _autoStartCheckBox.Checked = autoStartEnabled;

            ConfigureTrackBar(_boostTrackBar, 5, 50, (int)Math.Round(settings.ScrollBoost * 10));
            ConfigureTrackBar(_accelTrackBar, 10, 100, (int)Math.Round(settings.AccelMaxX * 10));
            ConfigureTrackBar(_catchupTrackBar, 10, 200, (int)Math.Round(settings.CatchupPerMs * 10000));
            ConfigureTrackBar(_frictionTrackBar, 9900, 9999, (int)Math.Round(settings.FrictionPerMs * 10000));
            ConfigureTrackBar(_responseTrackBar, 20, 200, (int)Math.Round(settings.AccelDeltaMs));
            UpdateValueLabels();
        }

        private static void ConfigureTrackBar(TrackBar trackBar, int minimum, int maximum, int value)
        {
            trackBar.Minimum = minimum;
            trackBar.Maximum = maximum;
            trackBar.Value = Math.Clamp(value, minimum, maximum);
        }

        private void WireValueUpdates()
        {
            _boostTrackBar.ValueChanged += (_, _) => UpdateValueLabels();
            _accelTrackBar.ValueChanged += (_, _) => UpdateValueLabels();
            _catchupTrackBar.ValueChanged += (_, _) => UpdateValueLabels();
            _frictionTrackBar.ValueChanged += (_, _) => UpdateValueLabels();
            _responseTrackBar.ValueChanged += (_, _) => UpdateValueLabels();
        }

        private void UpdateValueLabels()
        {
            _boostValueLabel.Text = $"{_boostTrackBar.Value / 10.0:0.0}x";
            _accelValueLabel.Text = $"{_accelTrackBar.Value / 10.0:0.0}x";
            _catchupValueLabel.Text = $"{_catchupTrackBar.Value / 10000.0:0.0000}";
            _frictionValueLabel.Text = $"{_frictionTrackBar.Value / 10000.0:0.0000}";
            _responseValueLabel.Text = $"{_responseTrackBar.Value} ms";
        }

        private void SaveAndClose()
        {
            var settings = new AppSettings
            {
                SmoothScrollEnabled = _smoothScrollCheckBox.Checked,
                ScrollBoost = _boostTrackBar.Value / 10.0,
                AccelMaxX = _accelTrackBar.Value / 10.0,
                CatchupPerMs = _catchupTrackBar.Value / 10000.0,
                FrictionPerMs = _frictionTrackBar.Value / 10000.0,
                AccelDeltaMs = _responseTrackBar.Value
            };

            SettingsSaved?.Invoke(this, new SettingsSavedEventArgs(settings, _autoStartCheckBox.Checked));
            Close();
        }

        private sealed class ModernPanel : Panel
        {
            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public Color BorderColor { get; set; } = Color.FromArgb(225, 225, 225);

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public int CornerRadius { get; set; } = 8;

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = CreateRoundRect(ClientRectangle, CornerRadius);
                using var fillBrush = new SolidBrush(BackColor);
                using var borderPen = new Pen(BorderColor);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                Region?.Dispose();
                Region = new Region(CreateRoundRect(ClientRectangle, CornerRadius));
            }

            private static GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
            {
                int diameter = radius * 2;
                var path = new GraphicsPath();
                Rectangle arc = new(bounds.Location, new Size(diameter, diameter));

                path.AddArc(arc, 180, 90);
                arc.X = bounds.Right - diameter - 1;
                path.AddArc(arc, 270, 90);
                arc.Y = bounds.Bottom - diameter - 1;
                path.AddArc(arc, 0, 90);
                arc.X = bounds.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
