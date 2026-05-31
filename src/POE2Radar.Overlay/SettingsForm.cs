using System.Drawing;
using POE2Radar.Core.Cheats;

namespace POE2Radar.Overlay;

public sealed class SettingsForm : Form
{
    private readonly CheatManager _cheats;
    private readonly RadarSettings _rs;
    private readonly Dictionary<string, CheckBox> _cheatBoxes = new();

    public SettingsForm(CheatManager cheats, RadarSettings rs)
    {
        _cheats = cheats; _rs = rs;
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "Settings (F9)";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        TopMost = true; StartPosition = FormStartPosition.Manual;
        Location = new Point(50, 50);
        BackColor = Color.FromArgb(35, 35, 45);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        ShowInTaskbar = false;

        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(35, 35, 45) };
        var y = 12;

        panel.Controls.Add(new Label
        {
            Text = "Cheats (F11 for full radar settings)",
            Location = new Point(10, y), AutoSize = true,
            ForeColor = Color.FromArgb(120, 180, 255),
            Font = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold),
        });
        y += 24;

        foreach (var (name, info) in _cheats.GetStatus())
        {
            var capName = name;
            var cb = new CheckBox
            {
                Text = info.ShortName + (info.Found ? "" : " (n/a)"),
                Location = new Point(14, y + 2), AutoSize = true,
                Checked = info.Active, Enabled = info.Found,
                ForeColor = info.Found ? Color.White : Color.Gray,
                FlatStyle = FlatStyle.Flat,
            };
            panel.Controls.Add(cb);
            _cheatBoxes[name] = cb;

            if (info.HasSlider)
            {
                var sp = new Panel { Location = new Point(90, y - 1), Size = new Size(230, 30), BackColor = SystemColors.Control };
                var tb = new TrackBar
                {
                    Location = new Point(0, 0), Size = new Size(200, 30),
                    Minimum = (int)info.Min, Maximum = (int)info.Max,
                    Value = Math.Clamp((int)info.Value, (int)info.Min, (int)info.Max),
                    TickStyle = TickStyle.None, SmallChange = 50, LargeChange = 200,
                    Enabled = info.Found,
                };
                sp.Controls.Add(tb);
                panel.Controls.Add(sp);
                var vl = new Label { Text = $"{(int)info.Value}", Location = new Point(325, y + 5), AutoSize = true, ForeColor = Color.FromArgb(130, 200, 255) };
                panel.Controls.Add(vl);
                tb.Scroll += (_, _) => { _cheats.SetConstantValue(capName, tb.Value); vl.Text = $"{tb.Value}"; if (!cb.Checked) cb.Checked = true; };
                cb.CheckedChanged += (_, _) => { if (cb.Checked) _cheats.SetConstantValue(capName, tb.Value); else _cheats.Toggle(capName); };
                y += 34;
            }
            else
            {
                cb.CheckedChanged += (_, _) => _cheats.Toggle(capName);
                y += 26;
            }
        }

        Controls.Add(panel);
        ClientSize = new Size(400, y + 20);
    }

    public void SyncState()
    {
        foreach (var (name, info) in _cheats.GetStatus())
            if (_cheatBoxes.TryGetValue(name, out var cb) && cb.Checked != info.Active) cb.Checked = info.Active;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
}
