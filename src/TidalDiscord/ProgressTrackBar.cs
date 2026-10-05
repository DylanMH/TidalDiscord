using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Lightweight double-buffered progress track bar for the
// StatusForm's now-playing section. Draws a rounded track,
// a filled portion, and a position knob. Safe with unknown
// duration (draws an empty track).
public class ProgressTrackBar : Control
{
    private static readonly Color TrackColor =
        Color.FromArgb(225, 228, 232);

    private static readonly Color FillColor =
        Color.FromArgb(0, 150, 180);

    private static readonly Color KnobColor =
        Color.White;

    private static readonly Color KnobBorderColor =
        Color.FromArgb(0, 130, 160);

    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public bool IsPaused { get; private set; }

    public ProgressTrackBar()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void SetProgress(
        TimeSpan position,
        TimeSpan duration,
        bool isPaused)
    {
        if (position == Position &&
            duration == Duration &&
            isPaused == IsPaused)
        {
            return;
        }

        Position = position;
        Duration = duration;
        IsPaused = isPaused;

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int trackHeight = 4;
        int trackY = (Height - trackHeight) / 2;
        int knobRadius = 5;

        // Track
        using (var trackBrush = new SolidBrush(TrackColor))
        {
            g.FillRoundedRectangle(
                trackBrush,
                new Rectangle(0, trackY, Width, trackHeight),
                trackHeight);
        }

        if (Duration <= TimeSpan.Zero || Width <= knobRadius * 2)
        {
            return;
        }

        double fraction =
            Math.Clamp(
                Position.TotalSeconds / Duration.TotalSeconds,
                0.0,
                1.0);

        int fillWidth =
            Math.Max(
                trackHeight,
                (int)Math.Round(Width * fraction));

        using (var fillBrush = new SolidBrush(
                   IsPaused
                       ? ControlPaint.Light(FillColor, 0.4f)
                       : FillColor))
        {
            g.FillRoundedRectangle(
                fillBrush,
                new Rectangle(0, trackY, fillWidth, trackHeight),
                trackHeight);
        }

        // Knob
        int knobX =
            Math.Clamp(
                fillWidth,
                knobRadius,
                Width - knobRadius);

        using (var knobBrush = new SolidBrush(KnobColor))
        using (var knobPen =
                   new Pen(KnobBorderColor, 1.5f))
        {
            var knobRect =
                new Rectangle(
                    knobX - knobRadius,
                    Height / 2 - knobRadius,
                    knobRadius * 2,
                    knobRadius * 2);

            g.FillEllipse(knobBrush, knobRect);
            g.DrawEllipse(knobPen, knobRect);
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(
        this Graphics g,
        Brush brush,
        Rectangle bounds,
        int radius)
    {
        if (radius * 2 > bounds.Width ||
            radius * 2 > bounds.Height)
        {
            g.FillRectangle(brush, bounds);
            return;
        }

        using var path = new GraphicsPath();

        int d = radius * 2;

        path.AddArc(
            bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(
            bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(
            bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(
            bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        g.FillPath(brush, path);
    }
}
