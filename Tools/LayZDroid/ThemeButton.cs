namespace LayZDroid;

// Windows paints disabled button captions black on this dark theme unless we supply the contrast.
public sealed class ThemeButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Enabled){base.OnPaint(e);return;}
        using var background=new SolidBrush(Color.FromArgb(30,23,45));
        e.Graphics.FillRectangle(background,ClientRectangle);
        using var border=new Pen(Color.FromArgb(66,53,87));
        e.Graphics.DrawRectangle(border,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.FromArgb(185,176,202),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
