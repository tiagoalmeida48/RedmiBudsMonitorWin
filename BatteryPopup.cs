using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace RedmiBudsMonitor;

/// <summary>
/// Popup dark que aparece sobre o ícone na bandeja ao clicar.
/// Some automaticamente ao perder o foco.
/// </summary>
internal sealed class BatteryPopup : Form
{
    // ── Estado ────────────────────────────────────────────────────────────────

    private string _leftStr  = "--";
    private string _caseStr  = "--";
    private string _rightStr = "--";
    private byte   _leftPct  = 0xFF;
    private byte   _casePct  = 0xFF;
    private byte   _rightPct = 0xFF;

    // ── Construção ────────────────────────────────────────────────────────────

    public BatteryPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        DoubleBuffered  = true;
        BackColor       = Color.FromArgb(28, 28, 28); // Cor base de fundo transparente
        Width           = 220;
        Height          = 155;
        StartPosition   = FormStartPosition.Manual;
        Padding         = new Padding(0);
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void UpdateData(
        string leftStr, string caseStr, string rightStr,
        byte leftPct,   byte casePct,   byte rightPct)
    {
        _leftStr  = leftStr;  _caseStr  = caseStr;  _rightStr  = rightStr;
        _leftPct  = leftPct;  _casePct  = casePct;  _rightPct  = rightPct;
        if (Visible) Invalidate();
    }

    public void ToggleNearTray()
    {
        if (Visible) { Hide(); return; }

        // Posiciona no canto inferior direito da área de trabalho com respiro de tela
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - Width - 14, area.Bottom - Height - 14);
        Show();
        Activate();
    }

    // ── Some ao perder foco ───────────────────────────────────────────────────

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    // ── Pintura ───────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        // Fundo e borda (Estilo Apple/iOS com curve accentuado)
        using var bgBrush   = new SolidBrush(Color.FromArgb(28, 28, 30)); // #1C1C1E padrão macOS dark
        using var borderPen = new Pen(Color.FromArgb(60, 60, 65), 1);
        using var path      = GetRoundRect(0, 0, Width - 1, Height - 1, 14); // Curva mais redonda
        
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        // Título compacto
        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 235, 245));
        g.DrawString("Redmi Buds 5", titleFont, titleBrush, 16f, 12f);

        // Linha super discreta
        using var sepPen = new Pen(Color.FromArgb(45, 45, 50), 1);
        g.DrawLine(sepPen, 16, 38, Width - 16, 38);

        // Itens
        DrawItemRow(g, 0, true,  "Esquerdo", _leftStr,  _leftPct, isRight: false);
        DrawItemRow(g, 1, false, "Caixinha", _caseStr,  _casePct, isRight: false);
        DrawItemRow(g, 2, true,  "Direito",  _rightStr, _rightPct, isRight: true);
    }

    private void DrawItemRow(Graphics g, int index, bool isEarbud, string name, string value, byte pct, bool isRight)
    {
        float y = 46f + (index * 34f); 

        // 1. Fundo do Ícone em círculo sutil
        using var iconBgBrush = new SolidBrush(Color.FromArgb(44, 44, 46));
        g.FillEllipse(iconBgBrush, 16f, y, 26f, 26f);

        // 2. Desenho do Ícone (transladado para o centro do círculo: x=29, y=13 a partir do topo do círculo)
        var st = g.Save();
        g.TranslateTransform(29f, y + 13f);
        if (isEarbud)
            DrawEarbud(g, isRight);
        else
            DrawCase(g);
        g.Restore(st);

        // 3. Nome
        using var nameFont  = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        using var nameBrush = new SolidBrush(Color.FromArgb(200, 200, 205));
        var nsz = g.MeasureString(name, nameFont);
        g.DrawString(name, nameFont, nameBrush, 50f, y + (26f - nsz.Height) / 2f);

        // 4. Valor (bateria)
        using var valueFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var valueBrush = new SolidBrush(PercentColor(pct));
        var vsz = g.MeasureString(value, valueFont);
        g.DrawString(value, valueFont, valueBrush, Width - 16f - vsz.Width, y + (26f - vsz.Height) / 2f);
    }

    // Cria iconografia minimalista do Fone (haste + ponta redonda)
    private void DrawEarbud(Graphics g, bool isRight)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235)); // Cinza claro polido
        
        // Espelhamento lógico
        int dir = isRight ? 1 : -1;
        
        // Corpo principal (haste arredondada)
        float stemX = isRight ? 0f : -3.5f;
        g.FillPath(brush, GetRoundRect(stemX, -7f, 3.5f, 13f, 1.5f));

        // Cabeça (pontinha do in-ear)
        float headX = isRight ? -4f : 0f;
        g.FillEllipse(brush, headX, -7f, 6.5f, 7.5f);
        
        // Ponto de respiro/microfone sutil (para dar detalhe de "premium")
        using var darkBrush = new SolidBrush(Color.FromArgb(28, 28, 30));
        g.FillEllipse(darkBrush, stemX + 1f, -4f, 1.5f, 1.5f);
    }

    // Cria iconografia minimalista da Caixinha (Estojo)
    private void DrawCase(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235));
        
        // Base redondinha
        g.FillPath(brush, GetRoundRect(-6.5f, -2f, 13f, 8.5f, 3f));
        
        // Tampa arqueada em cima 
        using var lidPen = new Pen(brush, 1.5f);
        g.DrawArc(lidPen, -6.5f, -6f, 13f, 11f, 180, 180);
        
        // LED da caixinha
        using var darkBrush = new SolidBrush(Color.FromArgb(28, 28, 30));
        g.FillRectangle(darkBrush, -2f, 3f, 4f, 1f);
    }

    private System.Drawing.Drawing2D.GraphicsPath GetRoundRect(float x, float y, float width, float height, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ── Utilitários ───────────────────────────────────────────────────────────

    internal static Color PercentColor(byte pct) => pct switch
    {
        > 100 => Color.FromArgb(110, 110, 110),
        >= 50 => Color.FromArgb(72,  199, 116), // Verde Apple 
        >= 20 => Color.FromArgb(255, 159,  10), // Laranja Apple
        _     => Color.FromArgb(255,  69,  58), // Vermelho Apple
    };
}

