using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AZ5Launcher
{
    public class TargetSlot
    {
        public string Path = "";
        public string Label = "";

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(Label))
                return Label.ToUpper();

            if (string.IsNullOrEmpty(Path))
                return "ASSIGN TARGET";

            if (Directory.Exists(Path))
            {
                string dirName = System.IO.Path.GetFileName(Path);
                return string.IsNullOrEmpty(dirName) ? Path.ToUpper() : dirName.ToUpper();
            }

            try
            {
                return System.IO.Path.GetFileNameWithoutExtension(Path).ToUpper();
            }
            catch
            {
                return Path.ToUpper();
            }
        }
    }

    public class Program : Form
    {
        private Bitmap bgImage;
        private Bitmap cachedCasingImage;
        private Bitmap cachedPressedCasingImage;
        private TargetSlot[] targetSlots = new TargetSlot[4];
        private int activeSlotIndex = 0;

        private TargetSlot ActiveSlot
        {
            get { return targetSlots[activeSlotIndex]; }
        }

        private bool isPressed = false;

        // Base virtual canvas layout (Width 460, Height 455)
        private const int BASE_WIDTH = 460;
        private const int BASE_HEIGHT = 455;
        private const int CASING_X = 140;
        private const int CASING_Y = 60;
        private const int CASING_WIDTH = 320;
        private const int CASING_HEIGHT = 310;

        // Dragging state variables
        private bool isDragging = false;
        private Point dragStartCursor = Point.Empty;
        private Point mouseDownCursor = Point.Empty;
        private readonly int dragThreshold = 4; // pixels

        // Corner button geometries (on casing: X = CASING_X..CASING_X+320, Y = 60..370)
        private readonly Rectangle helpButtonRect = new Rectangle(CASING_X + 13, CASING_Y + 12, 22, 22);       // Top Left (153, 72)
        private readonly Rectangle closeButtonRect = new Rectangle(CASING_X + 285, CASING_Y + 12, 22, 22);    // Top Right (425, 72)
        private readonly Rectangle radiationButtonRect = new Rectangle(CASING_X + 13, CASING_Y + 276, 22, 22);// Bottom Left (153, 336)
        private readonly Rectangle arrowButtonRect = new Rectangle(CASING_X + 285, CASING_Y + 276, 22, 22);   // Bottom Right (425, 336)

        // Nameplate on bottom of casing image
        private readonly Rectangle nameplateRect = new Rectangle(CASING_X + 38, CASING_Y + 273, 244, 28);

        // Hover states for corner buttons & nameplate
        private bool isHoveringClose = false;
        private bool isHoveringHelp = false;
        private bool isHoveringRadiation = false;
        private bool isHoveringArrow = false;
        private bool isHoveringNameplate = false;
        private bool isHoveringLeftAutoClose = false;

        // Auto-Close on launch state (default: unchecked / false)
        private bool autoClose = false;

        // Bottom path drawer animation state
        private bool isCardExpanded = false;
        private float cardProgress = 0.0f; // 0.0f = hidden, 1.0f = fully visible
        private float currentArrowAngle = 0.0f; // 0.0f = down, 180.0f = up
        private Stopwatch cardStopwatch = new Stopwatch();
        private float animCardStart = 0.0f;
        private float animCardTarget = 0.0f;
        private readonly int textCardHiddenY = CASING_Y + 280;
        private readonly int textCardExpandedY = CASING_Y + 308;
        private readonly Rectangle textCardRect = new Rectangle(CASING_X + 10, CASING_Y + 308, 300, 30);

        // Top help drawer animation state
        private bool isHelpExpanded = false;
        private float helpProgress = 0.0f; // 0.0f = hidden, 1.0f = fully visible
        private float currentRadiationAngle = 0.0f; // 0.0f = upright, 360.0f = full 360 spin
        private Stopwatch helpStopwatch = new Stopwatch();
        private float animHelpStart = 0.0f;
        private float animHelpTarget = 0.0f;
        private readonly int helpCardHiddenY = CASING_Y + 5; // 65
        private readonly int helpCardExpandedY = 6;
        private readonly Rectangle helpCardRect = new Rectangle(CASING_X + 10, 6, 300, 56);

        // Left target presets drawer animation state
        private bool isLeftExpanded = false;
        private float leftProgress = 0.0f; // 0.0f = hidden, 1.0f = fully visible
        private float currentLeftArrowAngle = 0.0f; // 0.0f = facing left, 180.0f = facing right
        private Stopwatch leftStopwatch = new Stopwatch();
        private float animLeftStart = 0.0f;
        private float animLeftTarget = 0.0f;
        private readonly int leftDrawerHiddenX = CASING_X - 8; // 132
        private readonly int leftDrawerExpandedX = 8;
        private readonly int leftDrawerWidth = 136;
        private readonly int leftDrawerY = 70;
        private readonly int leftDrawerHeight = 290;
        private int hoveringSlotIndex = -1; // 0..3 or -1

        // Win32 constants to block Alt+Enter and window resizing/maximizing
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_SIZE = 0xF000;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Enter && (keyData & Keys.Alt) == Keys.Alt)
            {
                return true; // Completely suppress Alt+Enter
            }
            if ((keyData & Keys.KeyCode) == Keys.Return && (keyData & Keys.Alt) == Keys.Alt)
            {
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Alt && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSKEYDOWN)
            {
                int vk = (int)m.WParam;
                if (vk == (int)Keys.Enter || vk == (int)Keys.Return)
                {
                    return; // Swallows Alt+Enter at the window message loop
                }
            }
            if (m.Msg == WM_SYSCOMMAND)
            {
                int cmd = ((int)m.WParam & 0xFFF0);
                if (cmd == SC_MAXIMIZE || cmd == SC_SIZE)
                {
                    return; // Block maximize and sizing commands
                }
            }
            base.WndProc(ref m);
        }

        // Shared animation timer
        private Timer animTimer;
        private const float ANIM_DURATION_MS = 250.0f;

        private ContextMenuStrip contextMenu;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Program());
        }

        public Program()
        {
            for (int i = 0; i < 4; i++)
            {
                targetSlots[i] = new TargetSlot();
            }

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(BASE_WIDTH, BASE_HEIGHT);
            this.MinimumSize = new Size(BASE_WIDTH, BASE_HEIGHT);
            this.MaximumSize = new Size(BASE_WIDTH, BASE_HEIGHT);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.KeyPreview = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, false);
            this.Text = "AZ-5 Button";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Load icon
            try
            {
                string iconPath = Path.Combine(baseDir, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            // Load background image from embedded resource
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("az5_button.png"))
                {
                    if (stream != null)
                    {
                        using (var temp = new Bitmap(stream))
                        {
                            bgImage = new Bitmap(temp);
                        }
                    }
                }
            }
            catch { }

            // Fallback to file if resource loading fails
            string bgPath = Path.Combine(baseDir, "az5_button.png");
            if (bgImage == null && File.Exists(bgPath))
            {
                try
                {
                    using (var temp = new Bitmap(bgPath))
                    {
                        bgImage = new Bitmap(temp);
                    }
                }
                catch { }
            }

            PrepareCasingImages();

            // Set up animation timer
            animTimer = new Timer();
            animTimer.Interval = 15; // ~60 FPS
            animTimer.Tick += AnimTimer_Tick;

            // Load settings (including multi-slot and scale persistence)
            LoadSettings();

            // Set up main context menu
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Assign Target File...", null, (s, e) => AssignTargetFile(activeSlotIndex));
            contextMenu.Items.Add("Assign Target Folder...", null, (s, e) => AssignTargetFolder(activeSlotIndex));
            
            var renameItem = new ToolStripMenuItem("Rename Target Label...", null, (s, e) => RenameLabel(activeSlotIndex));
            var clearItem = new ToolStripMenuItem("Clear Target", null, (s, e) => ClearTarget(activeSlotIndex));
            var autoCloseItem = new ToolStripMenuItem("Auto-Close on Launch", null, (s, e) => ToggleAutoClose());
            contextMenu.Opening += (s, e) => {
                bool hasTarget = !string.IsNullOrEmpty(ActiveSlot.Path) || !string.IsNullOrEmpty(ActiveSlot.Label);
                renameItem.Enabled = hasTarget;
                clearItem.Enabled = hasTarget;
                autoCloseItem.Checked = autoClose;
            };
            contextMenu.Items.Add(renameItem);
            contextMenu.Items.Add(clearItem);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add(autoCloseItem);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            this.ContextMenuStrip = contextMenu;
        }

        private void PrepareCasingImages()
        {
            if (bgImage == null) return;

            try
            {
                // Create 320x310 normal image
                Bitmap normal = new Bitmap(CASING_WIDTH, CASING_HEIGHT, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(normal))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(bgImage, new Rectangle(0, 0, CASING_WIDTH, CASING_HEIGHT), 0, 0, bgImage.Width, bgImage.Height, GraphicsUnit.Pixel, ia);
                    }
                }

                // Threshold alpha channel: any pixel with alpha < 128 becomes pure transparent (0).
                // Any pixel with alpha >= 128 becomes 100% opaque (255).
                // Windows TransparencyKey requires exact RGB matching (255, 0, 255).
                // Fractional alpha blending against Magenta creates pink fringe artifacts.
                // Thresholding guarantees 0 intermediate blended pixels!
                for (int y = 0; y < CASING_HEIGHT; y++)
                {
                    for (int x = 0; x < CASING_WIDTH; x++)
                    {
                        Color c = normal.GetPixel(x, y);
                        if (c.A < 128)
                        {
                            normal.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                        }
                        else
                        {
                            normal.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                        }
                    }
                }

                // Create 320x310 pressed image (0.85 brightness) with exact same clean alpha
                Bitmap pressed = new Bitmap(CASING_WIDTH, CASING_HEIGHT, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for (int y = 0; y < CASING_HEIGHT; y++)
                {
                    for (int x = 0; x < CASING_WIDTH; x++)
                    {
                        Color c = normal.GetPixel(x, y);
                        if (c.A == 0)
                        {
                            pressed.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                        }
                        else
                        {
                            int r = (int)(c.R * 0.85f);
                            int g = (int)(c.G * 0.85f);
                            int b = (int)(c.B * 0.85f);
                            pressed.SetPixel(x, y, Color.FromArgb(255, r, g, b));
                        }
                    }
                }

                cachedCasingImage = normal;
                cachedPressedCasingImage = pressed;
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            int casingTop = CASING_Y + yOffset;
            int casingBottom = CASING_Y + CASING_HEIGHT + yOffset;
            int casingLeft = CASING_X + xOffset;

            // 1. Draw Left Target Drawer (layer underneath left edge of casing)
            if (leftProgress > 0.001f)
            {
                DrawLeftDrawer(g);
            }

            // 2. Draw Top Help Card (layer underneath top edge of casing)
            if (helpProgress > 0.001f)
            {
                DrawHelpCard(g);
            }

            // 3. Draw Bottom Target Path Card (layer underneath bottom edge of casing)
            if (cardProgress > 0.001f)
            {
                DrawTextCard(g);
            }

            // 4. Draw the button casing image (ON TOP OF all drawers)
            if (cachedCasingImage != null)
            {
                Bitmap imgToDraw = isPressed ? cachedPressedCasingImage : cachedCasingImage;
                g.DrawImage(imgToDraw, new Rectangle(casingLeft, casingTop, CASING_WIDTH, CASING_HEIGHT), 0, 0, CASING_WIDTH, CASING_HEIGHT, GraphicsUnit.Pixel);
            }
            else if (bgImage != null)
            {
                if (isPressed)
                {
                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][] {
                            new float[] { 0.85f, 0, 0, 0, 0 },
                            new float[] { 0, 0.85f, 0, 0, 0 },
                            new float[] { 0, 0, 0.85f, 0, 0 },
                            new float[] { 0, 0, 0, 1.0f, 0 },
                            new float[] { 0, 0, 0, 0, 1 }
                        });
                        ia.SetColorMatrix(matrix);
                        g.DrawImage(bgImage, new Rectangle(casingLeft, casingTop, CASING_WIDTH, CASING_HEIGHT), 0, 0, bgImage.Width, bgImage.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                else
                {
                    g.DrawImage(bgImage, casingLeft, casingTop, CASING_WIDTH, CASING_HEIGHT);
                }
            }
            else
            {
                g.Clear(Color.Gray);
                g.FillEllipse(Brushes.Red, casingLeft + 160 - 65, casingTop + 155 - 65, 130, 130);
            }

            // 5. Contact shadow along top edge of casing onto help card
            if (helpProgress > 0.001f)
            {
                int currentHelpY = GetCurrentHelpY();
                int helpTop = currentHelpY + yOffset;
                if (helpTop < casingTop)
                {
                    int shadowH = Math.Min(6, casingTop - helpTop);
                    using (var shadowBrush = new LinearGradientBrush(
                        new Rectangle(helpCardRect.X + xOffset, casingTop - shadowH, helpCardRect.Width, shadowH),
                        Color.Transparent,
                        Color.FromArgb(90, 0, 0, 0),
                        LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(shadowBrush, helpCardRect.X + xOffset, casingTop - shadowH, helpCardRect.Width, shadowH);
                    }
                }
            }

            // 6. Contact shadow along bottom edge of casing onto target path card
            if (cardProgress > 0.001f)
            {
                int currentY = GetCurrentCardY();
                int cardBottom = currentY + textCardRect.Height + yOffset;
                if (cardBottom > casingBottom)
                {
                    int shadowH = Math.Min(6, cardBottom - casingBottom);
                    using (var shadowBrush = new LinearGradientBrush(
                        new Rectangle(textCardRect.X + xOffset, casingBottom, textCardRect.Width, shadowH),
                        Color.FromArgb(90, 0, 0, 0),
                        Color.Transparent,
                        LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(shadowBrush, textCardRect.X + xOffset, casingBottom, textCardRect.Width, shadowH);
                    }
                }
            }

            // 7. Contact shadow along left edge of casing onto left drawer
            if (leftProgress > 0.001f)
            {
                int currentLeftX = GetCurrentLeftX();
                int drawerRight = currentLeftX + leftDrawerWidth + xOffset;
                if (drawerRight > casingLeft)
                {
                    int shadowW = Math.Min(8, drawerRight - casingLeft);
                    int shadowX = casingLeft - shadowW;
                    using (var shadowBrush = new LinearGradientBrush(
                        new Rectangle(shadowX, leftDrawerY + 8 + yOffset, shadowW, leftDrawerHeight - 16),
                        Color.Transparent,
                        Color.FromArgb(70, 0, 0, 0),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(shadowBrush, shadowX, leftDrawerY + 8 + yOffset, shadowW, leftDrawerHeight - 16);
                    }
                }
            }

            // 8. Target name sitting directly at the bottom of the casing image
            DrawNameplate(g);

            // 9. Draw all 4 corner action buttons on the casing
            DrawCloseButton(g);
            DrawRadiationButton(g);
            DrawLeftArrowButton(g);
            DrawArrowButton(g);
        }

        private void DrawNameplate(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            Rectangle rect = new Rectangle(nameplateRect.X + xOffset, nameplateRect.Y + yOffset, nameplateRect.Width, nameplateRect.Height);

            // Background of nameplate (vintage eggshell instrument badge)
            Color plateBg = isHoveringNameplate ? Color.FromArgb(246, 248, 244) : Color.FromArgb(236, 238, 232);
            using (Brush b = new SolidBrush(plateBg))
            using (Pen borderPen = new Pen(Color.FromArgb(145, 125, 115), 1))
            {
                g.FillRectangle(b, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            // Subtle inner border
            using (Pen innerPen = new Pen(Color.FromArgb(50, 0, 0, 0), 1))
            {
                g.DrawRectangle(innerPen, rect.X + 2, rect.Y + 2, rect.Width - 5, rect.Height - 5);
            }

            // Target Name Text: e.g. CHROME or ASSIGN TARGET
            string displayName = ActiveSlot.GetDisplayName();
            using (Font font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, displayName, font, rect, Color.FromArgb(25, 28, 35),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawCloseButton(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            Rectangle rect = new Rectangle(closeButtonRect.X + xOffset, closeButtonRect.Y + yOffset, closeButtonRect.Width, closeButtonRect.Height);

            using (Brush brush = new SolidBrush(isHoveringClose ? Color.FromArgb(230, 50, 50) : Color.FromArgb(160, 180, 180, 180)))
            {
                g.FillEllipse(brush, rect);
            }

            using (Pen pen = new Pen(Color.White, 2))
            {
                int pad = 6;
                g.DrawLine(pen, rect.X + pad, rect.Y + pad, rect.Right - pad, rect.Bottom - pad);
                g.DrawLine(pen, rect.Right - pad, rect.Y + pad, rect.X + pad, rect.Bottom - pad);
            }
        }

        private void DrawRadiationButton(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            Rectangle rect = new Rectangle(helpButtonRect.X + xOffset, helpButtonRect.Y + yOffset, helpButtonRect.Width, helpButtonRect.Height);

            Color bgColor = (isHoveringHelp || isHelpExpanded) ? Color.FromArgb(235, 175, 15) : Color.FromArgb(160, 180, 180, 180);
            using (Brush brush = new SolidBrush(bgColor))
            {
                g.FillEllipse(brush, rect);
            }

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            Color symbolColor = (isHoveringHelp || isHelpExpanded) ? Color.FromArgb(30, 30, 30) : Color.White;

            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(currentRadiationAngle);

            using (Brush symBrush = new SolidBrush(symbolColor))
            {
                float rOuter = 6.5f;
                float dOuter = rOuter * 2f;
                g.FillPie(symBrush, -rOuter, -rOuter, dOuter, dOuter, 60f, 60f);
                g.FillPie(symBrush, -rOuter, -rOuter, dOuter, dOuter, 180f, 60f);
                g.FillPie(symBrush, -rOuter, -rOuter, dOuter, dOuter, 300f, 60f);

                float rGap = 3.2f;
                float dGap = rGap * 2f;
                using (Brush gapBrush = new SolidBrush(bgColor))
                {
                    g.FillEllipse(gapBrush, -rGap, -rGap, dGap, dGap);
                }

                float rDot = 1.5f;
                float dDot = rDot * 2f;
                g.FillEllipse(symBrush, -rDot, -rDot, dDot, dDot);
            }

            g.Restore(state);
        }

        private void DrawLeftArrowButton(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            Rectangle rect = new Rectangle(radiationButtonRect.X + xOffset, radiationButtonRect.Y + yOffset, radiationButtonRect.Width, radiationButtonRect.Height);

            Color bgColor = (isHoveringRadiation || isLeftExpanded) ? Color.FromArgb(70, 80, 95) : Color.FromArgb(160, 180, 180, 180);
            using (Brush brush = new SolidBrush(bgColor))
            {
                g.FillEllipse(brush, rect);
            }

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(currentLeftArrowAngle);

            using (Pen pen = new Pen(Color.White, 2.0f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                PointF[] pts = new PointF[] {
                    new PointF(2.0f, -4.5f),
                    new PointF(-2.5f, 0f),
                    new PointF(2.0f, 4.5f)
                };
                g.DrawLines(pen, pts);
            }

            g.Restore(state);
        }

        private void DrawArrowButton(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            Rectangle rect = new Rectangle(arrowButtonRect.X + xOffset, arrowButtonRect.Y + yOffset, arrowButtonRect.Width, arrowButtonRect.Height);

            Color bgColor = (isHoveringArrow || isCardExpanded) ? Color.FromArgb(70, 80, 95) : Color.FromArgb(160, 180, 180, 180);
            using (Brush brush = new SolidBrush(bgColor))
            {
                g.FillEllipse(brush, rect);
            }

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(currentArrowAngle);

            using (Pen pen = new Pen(Color.White, 2.0f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                PointF[] pts = new PointF[] {
                    new PointF(-4.5f, -2.0f),
                    new PointF(0f, 2.5f),
                    new PointF(4.5f, -2.0f)
                };
                g.DrawLines(pen, pts);
            }

            g.Restore(state);
        }

        private int GetCurrentLeftX()
        {
            return (int)Math.Round(leftDrawerHiddenX + (leftDrawerExpandedX - leftDrawerHiddenX) * leftProgress);
        }

        private void DrawLeftDrawer(Graphics g)
        {
            int currentX = GetCurrentLeftX();
            Rectangle drawerRect = new Rectangle(currentX, leftDrawerY, leftDrawerWidth, leftDrawerHeight);

            // Left drawer chassis with rounded left edge (SmoothingMode.None prevents antialiasing against Magenta)
            SmoothingMode prevMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (GraphicsPath path = new GraphicsPath())
            {
                int r = 12;
                int d = r * 2;
                path.AddArc(drawerRect.X, drawerRect.Y, d, d, 180, 90);
                path.AddLine(drawerRect.X + r, drawerRect.Y, drawerRect.Right, drawerRect.Y);
                path.AddLine(drawerRect.Right, drawerRect.Y, drawerRect.Right, drawerRect.Bottom);
                path.AddLine(drawerRect.Right, drawerRect.Bottom, drawerRect.X + r, drawerRect.Bottom);
                path.AddArc(drawerRect.X, drawerRect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                using (Brush b = new SolidBrush(Color.FromArgb(248, 249, 251)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(170, 180, 190), 1))
                {
                    g.DrawPath(p, path);
                }
            }
            g.SmoothingMode = prevMode;

            // Draw 4 target slots
            for (int i = 0; i < 4; i++)
            {
                DrawSlotCard(g, i, currentX);
            }

            // Draw Auto-Close? checkbox at bottom of left drawer
            DrawLeftAutoClose(g, currentX);
        }

        private Rectangle GetSlotRect(int index, int leftX)
        {
            int sy = leftDrawerY + 10 + index * 59;
            return new Rectangle(leftX + 6, sy, leftDrawerWidth - 12, 54);
        }

        private Rectangle GetLeftAutoCloseRect(int leftX)
        {
            return new Rectangle(leftX + 6, leftDrawerY + 248, leftDrawerWidth - 12, 30);
        }

        private void DrawLeftAutoClose(Graphics g, int leftX)
        {
            Rectangle acRect = GetLeftAutoCloseRect(leftX);
            Color acBg = isHoveringLeftAutoClose ? Color.FromArgb(238, 243, 250) : Color.White;
            Rectangle cardBounds = new Rectangle(acRect.X, acRect.Y, acRect.Width - 1, acRect.Height - 1);
            using (GraphicsPath path = CreateRoundedRectanglePath(cardBounds, 6))
            using (Brush b = new SolidBrush(acBg))
            using (Pen p = new Pen(isHoveringLeftAutoClose ? Color.FromArgb(140, 170, 210) : Color.FromArgb(220, 225, 230), 1))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }

            int boxSize = 14;
            Rectangle boxRect = new Rectangle(acRect.X + 8, acRect.Y + (acRect.Height - boxSize) / 2, boxSize, boxSize);
            Rectangle textRect = new Rectangle(acRect.X + 28, acRect.Y, acRect.Width - 30, acRect.Height);

            DrawCheckBox(g, boxRect, autoClose, isHoveringLeftAutoClose);

            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, "Auto-Close?", font, textRect, Color.FromArgb(255, 50, 60, 75),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateTopRoundedPath(Rectangle rect, int r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - 1 - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right - 1, rect.Y + r, rect.Right - 1, rect.Bottom);
            path.AddLine(rect.Right - 1, rect.Bottom, rect.X, rect.Bottom);
            path.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + r);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateBottomRoundedPath(Rectangle rect, int r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r * 2;
            path.AddLine(rect.X, rect.Y, rect.Right - 1, rect.Y);
            path.AddLine(rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom - 1 - r);
            path.AddArc(rect.Right - 1 - d, rect.Bottom - 1 - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - 1 - d, d, d, 90, 90);
            path.AddLine(rect.X, rect.Bottom - 1 - r, rect.X, rect.Y);
            path.CloseFigure();
            return path;
        }

        private void DrawCheckBox(Graphics g, Rectangle boxRect, bool isChecked, bool isHovered)
        {
            Color bgColor = isHovered ? Color.FromArgb(240, 245, 255) : Color.White;
            using (Brush b = new SolidBrush(bgColor))
            using (Pen p = new Pen(isHovered ? Color.FromArgb(40, 110, 220) : Color.FromArgb(160, 170, 180), 1.5f))
            {
                using (GraphicsPath path = CreateRoundedRectanglePath(boxRect, 3))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
            }

            if (isChecked)
            {
                using (Pen checkPen = new Pen(Color.FromArgb(20, 140, 40), 2.2f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    checkPen.LineJoin = LineJoin.Round;

                    PointF p1 = new PointF(boxRect.X + 3.0f, boxRect.Y + 7.0f);
                    PointF p2 = new PointF(boxRect.X + 6.0f, boxRect.Y + 10.5f);
                    PointF p3 = new PointF(boxRect.X + 11.5f, boxRect.Y + 3.5f);
                    g.DrawLines(checkPen, new PointF[] { p1, p2, p3 });
                }
            }
        }

        private void ToggleAutoClose()
        {
            autoClose = !autoClose;
            SaveSettings();
            Invalidate();
        }

        private void DrawSlotCard(Graphics g, int index, int leftX)
        {
            Rectangle sRect = GetSlotRect(index, leftX);
            bool isActive = (index == activeSlotIndex);
            bool isHovered = (index == hoveringSlotIndex);
            TargetSlot slot = targetSlots[index];

            Rectangle cardBounds = new Rectangle(sRect.X, sRect.Y, sRect.Width - 1, sRect.Height - 1);
            using (GraphicsPath path = CreateRoundedRectanglePath(cardBounds, 6))
            {
                if (isActive)
                {
                    using (Brush b = new SolidBrush(Color.FromArgb(255, 253, 235)))
                    using (Pen p = new Pen(Color.FromArgb(235, 175, 15), 2))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }
                }
                else
                {
                    Color bg = isHovered ? Color.FromArgb(238, 243, 250) : Color.White;
                    using (Brush b = new SolidBrush(bg))
                    using (Pen p = new Pen(isHovered ? Color.FromArgb(140, 170, 210) : Color.FromArgb(220, 225, 230), 1))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }
                }
            }

            // Target Name: centered text, no numbers, no sub-labels, ellipses if too long
            string dispName = slot.GetDisplayName();
            Color nameColor;
            if (string.IsNullOrEmpty(slot.Path) && string.IsNullOrEmpty(slot.Label))
            {
                dispName = "EMPTY";
                nameColor = Color.FromArgb(160, 170, 180);
            }
            else
            {
                nameColor = isActive ? Color.FromArgb(20, 24, 32) : Color.FromArgb(60, 70, 85);
            }

            Rectangle textRect = new Rectangle(sRect.X + 8, sRect.Y, sRect.Width - 16, sRect.Height);
            using (Font fontN = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, dispName, fontN, textRect, nameColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void ToggleLeftDrawer()
        {
            isLeftExpanded = !isLeftExpanded;
            animLeftStart = leftProgress;
            animLeftTarget = isLeftExpanded ? 1.0f : 0.0f;
            leftStopwatch.Restart();
            if (!animTimer.Enabled)
            {
                animTimer.Start();
            }
        }

        private void ToggleHelpCard()
        {
            isHelpExpanded = !isHelpExpanded;
            animHelpStart = helpProgress;
            animHelpTarget = isHelpExpanded ? 1.0f : 0.0f;
            helpStopwatch.Restart();
            if (!animTimer.Enabled)
            {
                animTimer.Start();
            }
        }

        private void ToggleTargetCard()
        {
            isCardExpanded = !isCardExpanded;
            animCardStart = cardProgress;
            animCardTarget = isCardExpanded ? 1.0f : 0.0f;
            cardStopwatch.Restart();
            if (!animTimer.Enabled)
            {
                animTimer.Start();
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            bool isAnimating = false;

            if (cardStopwatch.IsRunning)
            {
                float elapsed = (float)cardStopwatch.ElapsedMilliseconds;
                float t = elapsed / ANIM_DURATION_MS;
                if (t >= 1.0f)
                {
                    cardProgress = animCardTarget;
                    cardStopwatch.Stop();
                }
                else
                {
                    float ease = t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                    cardProgress = animCardStart + (animCardTarget - animCardStart) * ease;
                    isAnimating = true;
                }
                currentArrowAngle = cardProgress * 180.0f;
            }

            if (helpStopwatch.IsRunning)
            {
                float elapsed = (float)helpStopwatch.ElapsedMilliseconds;
                float t = elapsed / ANIM_DURATION_MS;
                if (t >= 1.0f)
                {
                    helpProgress = animHelpTarget;
                    helpStopwatch.Stop();
                }
                else
                {
                    float ease = t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                    helpProgress = animHelpStart + (animHelpTarget - animHelpStart) * ease;
                    isAnimating = true;
                }
                currentRadiationAngle = helpProgress * 360.0f;
            }

            if (leftStopwatch.IsRunning)
            {
                float elapsed = (float)leftStopwatch.ElapsedMilliseconds;
                float t = elapsed / ANIM_DURATION_MS;
                if (t >= 1.0f)
                {
                    leftProgress = animLeftTarget;
                    leftStopwatch.Stop();
                }
                else
                {
                    float ease = t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                    leftProgress = animLeftStart + (animLeftTarget - animLeftStart) * ease;
                    isAnimating = true;
                }
                currentLeftArrowAngle = leftProgress * 180.0f;
            }

            if (!isAnimating && !cardStopwatch.IsRunning && !helpStopwatch.IsRunning && !leftStopwatch.IsRunning)
            {
                animTimer.Stop();
            }

            Invalidate();
        }

        private int GetCurrentHelpY()
        {
            return (int)Math.Round(helpCardHiddenY + (helpCardExpandedY - helpCardHiddenY) * helpProgress);
        }

        private Rectangle GetCurrentHelpCardRect()
        {
            return new Rectangle(helpCardRect.X, GetCurrentHelpY(), helpCardRect.Width, helpCardRect.Height);
        }

        private void DrawHelpCard(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;

            int currentY = GetCurrentHelpY();
            Rectangle rect = new Rectangle(helpCardRect.X + xOffset, currentY + yOffset, helpCardRect.Width, helpCardRect.Height);

            SmoothingMode prevMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (GraphicsPath path = CreateTopRoundedPath(rect, 10))
            {
                g.FillPath(Brushes.White, path);
                using (Pen borderPen = new Pen(Color.FromArgb(170, 180, 190), 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }
            g.SmoothingMode = prevMode;

            int midY = rect.Y + rect.Height / 2;
            using (Pen pen = new Pen(Color.FromArgb(120, 160, 170, 180), 1))
            {
                g.DrawLine(pen, rect.X + 10, midY, rect.Right - 10, midY);
            }

            Rectangle topRect = new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height / 2 - 1);
            using (Font font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "AZ-5 QUICK LAUNCHER", font, topRect, Color.FromArgb(255, 17, 24, 39),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            Rectangle bottomRect = new Rectangle(rect.X, midY + 1, rect.Width, rect.Height / 2 - 1);
            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, "Slam button to launch • Right-click to configure target", font, bottomRect, Color.FromArgb(255, 74, 85, 104),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private int GetCurrentCardY()
        {
            return (int)Math.Round(textCardHiddenY + (textCardExpandedY - textCardHiddenY) * cardProgress);
        }

        private Rectangle GetCurrentTargetCardRect()
        {
            return new Rectangle(textCardRect.X, GetCurrentCardY(), textCardRect.Width, textCardRect.Height);
        }

        private void DrawTextCard(Graphics g)
        {
            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;

            int currentY = GetCurrentCardY();
            Rectangle rect = new Rectangle(textCardRect.X + xOffset, currentY + yOffset, textCardRect.Width, textCardRect.Height);

            // Card background and border (SmoothingMode.None prevents antialiasing against Magenta)
            SmoothingMode prevMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (GraphicsPath path = CreateBottomRoundedPath(rect, 10))
            {
                g.FillPath(Brushes.White, path);
                using (Pen borderPen = new Pen(Color.FromArgb(170, 180, 190), 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }
            g.SmoothingMode = prevMode;

            // Target Path
            string pathText = ActiveSlot.Path;
            if (string.IsNullOrEmpty(pathText))
            {
                pathText = "No target path assigned • Click to configure";
            }

            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, pathText, font, rect, Color.FromArgb(255, 60, 70, 85),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
            }
        }

        private static bool PointInCircle(PointF pt, float cx, float cy, float radius)
        {
            float dx = pt.X - cx;
            float dy = pt.Y - cy;
            return (dx * dx + dy * dy) <= (radius * radius);
        }

        private void ClearHoverStates()
        {
            if (isHoveringClose || isHoveringHelp || isHoveringRadiation || isHoveringArrow || isHoveringNameplate || isHoveringLeftAutoClose || hoveringSlotIndex != -1)
            {
                isHoveringClose = false;
                isHoveringHelp = false;
                isHoveringRadiation = false;
                isHoveringArrow = false;
                isHoveringNameplate = false;
                isHoveringLeftAutoClose = false;
                hoveringSlotIndex = -1;
                Invalidate();
            }
        }

        private void SelectTargetSlot(int index)
        {
            if (index < 0 || index >= 4) return;
            activeSlotIndex = index;
            if (string.IsNullOrEmpty(targetSlots[index].Path))
            {
                AssignTargetFile(index);
            }
            else
            {
                SaveSettings();
                Invalidate();
            }
        }

        private void ShowSlotContextMenu(int index, Point screenLocation)
        {
            ContextMenuStrip slotMenu = new ContextMenuStrip();
            slotMenu.Items.Add("Assign File...", null, (s, e) => AssignTargetFile(index));
            slotMenu.Items.Add("Assign Folder...", null, (s, e) => AssignTargetFolder(index));
            
            var renItem = new ToolStripMenuItem("Rename Label...", null, (s, e) => RenameLabel(index));
            renItem.Enabled = !string.IsNullOrEmpty(targetSlots[index].Path) || !string.IsNullOrEmpty(targetSlots[index].Label);
            slotMenu.Items.Add(renItem);

            var clrItem = new ToolStripMenuItem("Clear Target", null, (s, e) => ClearTarget(index));
            clrItem.Enabled = !string.IsNullOrEmpty(targetSlots[index].Path) || !string.IsNullOrEmpty(targetSlots[index].Label);
            slotMenu.Items.Add(clrItem);

            slotMenu.Show(screenLocation);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Point virtualPt = e.Location;

            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            int casingTop = CASING_Y + yOffset;
            int casingBottom = CASING_Y + CASING_HEIGHT + yOffset;
            int casingLeft = CASING_X + xOffset;
            int casingRight = CASING_X + CASING_WIDTH + xOffset;

            // Handle right click on slots in open left drawer
            if (e.Button == MouseButtons.Right)
            {
                if (leftProgress > 0.1f && virtualPt.X < CASING_X)
                {
                    int currentLeftX = GetCurrentLeftX();
                    for (int i = 0; i < 4; i++)
                    {
                        Rectangle sRect = GetSlotRect(i, currentLeftX);
                        if (sRect.Contains(virtualPt))
                        {
                            ShowSlotContextMenu(i, Cursor.Position);
                            return;
                        }
                    }
                }
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 1. Check slots & Auto-Close in open left drawer
                if (leftProgress > 0.1f && virtualPt.X < CASING_X)
                {
                    int currentLeftX = GetCurrentLeftX();
                    Rectangle leftAcRect = GetLeftAutoCloseRect(currentLeftX);
                    if (leftAcRect.Contains(virtualPt))
                    {
                        ToggleAutoClose();
                        return;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        Rectangle sRect = GetSlotRect(i, currentLeftX);
                        if (sRect.Contains(virtualPt))
                        {
                            SelectTargetSlot(i);
                            return;
                        }
                    }
                }

                // 3. Check close button (top right)
                if (closeButtonRect.Contains(virtualPt))
                {
                    Application.Exit();
                    return;
                }

                // 4. Check help button (top left)
                if (helpButtonRect.Contains(virtualPt))
                {
                    ToggleHelpCard();
                    return;
                }

                // 5. Check radiation button (bottom left) -> toggles left target presets drawer
                if (radiationButtonRect.Contains(virtualPt))
                {
                    ToggleLeftDrawer();
                    return;
                }

                // 6. Check arrow button (bottom right) -> toggles bottom path drawer
                if (arrowButtonRect.Contains(virtualPt))
                {
                    ToggleTargetCard();
                    return;
                }

                // 7. Check nameplate (bottom of casing) -> if empty assigns target, else toggles presets
                Rectangle curNameplateRect = new Rectangle(nameplateRect.X + xOffset, nameplateRect.Y + yOffset, nameplateRect.Width, nameplateRect.Height);
                if (curNameplateRect.Contains(virtualPt))
                {
                    if (string.IsNullOrEmpty(ActiveSlot.Path))
                    {
                        AssignTargetFile(activeSlotIndex);
                    }
                    else
                    {
                        ToggleLeftDrawer();
                    }
                    return;
                }

                // 8. Check visible top help card (click to dismiss)
                Rectangle currentHelpRect = GetCurrentHelpCardRect();
                if (helpProgress > 0.05f && virtualPt.Y < casingTop && virtualPt.Y >= currentHelpRect.Top && currentHelpRect.Contains(virtualPt))
                {
                    ToggleHelpCard();
                    return;
                }

                // 9. Check visible bottom target path card (click to reassign)
                Rectangle currentTargetRect = GetCurrentTargetCardRect();
                if (cardProgress > 0.05f && virtualPt.Y >= casingBottom && virtualPt.Y <= currentTargetRect.Bottom && currentTargetRect.Contains(virtualPt))
                {
                    AssignTargetFile(activeSlotIndex);
                    return;
                }

                // 10. Check button casing (anywhere in casing bounds)
                if (virtualPt.X >= casingLeft && virtualPt.X < casingRight && virtualPt.Y >= casingTop && virtualPt.Y < casingBottom)
                {
                    isPressed = true;
                    isDragging = true;
                    dragStartCursor = Cursor.Position;
                    mouseDownCursor = Cursor.Position; // Save original press spot
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (isDragging)
            {
                isDragging = false;
                
                if (isPressed)
                {
                    isPressed = false;
                    Invalidate();
                    
                    // Click was verified because drag threshold was not exceeded
                    LaunchProgram();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Handle window dragging logic
            if (isDragging)
            {
                Point currentCursor = Cursor.Position;
                int dx = currentCursor.X - dragStartCursor.X;
                int dy = currentCursor.Y - dragStartCursor.Y;

                // Move window
                this.Location = new Point(this.Location.X + dx, this.Location.Y + dy);
                dragStartCursor = currentCursor;

                // If mouse moves past threshold relative to original click, cancel button click/press state
                if (isPressed)
                {
                    int totalDx = currentCursor.X - mouseDownCursor.X;
                    int totalDy = currentCursor.Y - mouseDownCursor.Y;
                    if (Math.Abs(totalDx) > dragThreshold || Math.Abs(totalDy) > dragThreshold)
                    {
                        isPressed = false;
                        Invalidate();
                    }
                }
                return;
            }

            Point virtualPt = e.Location;

            int yOffset = isPressed ? 2 : 0;
            int xOffset = isPressed ? 2 : 0;
            int casingTop = CASING_Y + yOffset;
            int casingBottom = CASING_Y + CASING_HEIGHT + yOffset;
            int casingLeft = CASING_X + xOffset;
            int casingRight = CASING_X + CASING_WIDTH + xOffset;

            // Hover checks for 4 corner buttons
            bool hoveringClose = closeButtonRect.Contains(virtualPt);
            bool hoveringHelp = helpButtonRect.Contains(virtualPt);
            bool hoveringRadiation = radiationButtonRect.Contains(virtualPt);
            bool hoveringArrow = arrowButtonRect.Contains(virtualPt);

            // Hover check for nameplate
            Rectangle curNameplateRect = new Rectangle(nameplateRect.X + xOffset, nameplateRect.Y + yOffset, nameplateRect.Width, nameplateRect.Height);
            bool hoveringNameplate = curNameplateRect.Contains(virtualPt);

            // Hover check for left drawer Auto-Close & slots
            int newHoverSlot = -1;
            bool hoveringLeftAc = false;
            if (leftProgress > 0.1f && virtualPt.X < CASING_X)
            {
                int currentLeftX = GetCurrentLeftX();
                hoveringLeftAc = GetLeftAutoCloseRect(currentLeftX).Contains(virtualPt);
                for (int i = 0; i < 4; i++)
                {
                    Rectangle sRect = GetSlotRect(i, currentLeftX);
                    if (sRect.Contains(virtualPt))
                    {
                        newHoverSlot = i;
                        break;
                    }
                }
            }

            // Visible drawer hit checks
            Rectangle currentHelpRect = GetCurrentHelpCardRect();
            bool inVisibleHelp = (helpProgress > 0.05f) && (virtualPt.Y < casingTop) && (virtualPt.Y >= currentHelpRect.Top) && currentHelpRect.Contains(virtualPt);

            Rectangle currentTargetRect = GetCurrentTargetCardRect();
            bool inVisibleTarget = (cardProgress > 0.05f) && (virtualPt.Y >= casingBottom) && (virtualPt.Y <= currentTargetRect.Bottom) && currentTargetRect.Contains(virtualPt);

            if (hoveringClose != isHoveringClose || 
                hoveringHelp != isHoveringHelp || 
                hoveringRadiation != isHoveringRadiation || 
                hoveringArrow != isHoveringArrow ||
                hoveringNameplate != isHoveringNameplate ||
                newHoverSlot != hoveringSlotIndex ||
                hoveringLeftAc != isHoveringLeftAutoClose)
            {
                isHoveringClose = hoveringClose;
                isHoveringHelp = hoveringHelp;
                isHoveringRadiation = hoveringRadiation;
                isHoveringArrow = hoveringArrow;
                isHoveringNameplate = hoveringNameplate;
                hoveringSlotIndex = newHoverSlot;
                isHoveringLeftAutoClose = hoveringLeftAc;
                Invalidate();
            }

            bool inCasing = (virtualPt.X >= casingLeft && virtualPt.X < casingRight && virtualPt.Y >= casingTop && virtualPt.Y < casingBottom);

            // Set cursor type
            if (hoveringClose || hoveringHelp || hoveringRadiation || hoveringArrow || hoveringNameplate || newHoverSlot != -1 || hoveringLeftAc || inVisibleHelp || inVisibleTarget || inCasing)
            {
                this.Cursor = Cursors.Hand;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHoverStates();
            this.Cursor = Cursors.Default;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (animTimer != null)
            {
                animTimer.Stop();
                animTimer.Dispose();
            }
        }

        private void RenameLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return;
            TargetSlot slot = targetSlots[slotIndex];
            string defaultVal = !string.IsNullOrEmpty(slot.Label) ? slot.Label : 
                (Directory.Exists(slot.Path) ? Path.GetFileName(slot.Path) : Path.GetFileNameWithoutExtension(slot.Path));

            string result = ShowInputDialog("Enter custom label for target:", "Rename Label", defaultVal);
            if (result != null)
            {
                slot.Label = SanitizeSingleLine(result);
                SaveSettings();
                Invalidate();
            }
        }

        public static string ShowInputDialog(string text, string caption, string defaultValue)
        {
            Form prompt = new Form()
            {
                Width = 360,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label textLabel = new Label() { Left = 20, Top = 15, Text = text, Width = 310 };
            TextBox textBox = new TextBox() { Left = 20, Top = 40, Width = 300, Text = defaultValue };
            Button confirmation = new Button() { Text = "OK", Left = 120, Width = 90, Top = 75, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 220, Width = 90, Top = 75, DialogResult = DialogResult.Cancel };
            
            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };
            
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private void AssignTargetFile(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return;
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Target File";
                ofd.Filter = "All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    targetSlots[slotIndex].Path = SanitizePath(ofd.FileName);
                    targetSlots[slotIndex].Label = ""; // Clear custom label on new assignment
                    activeSlotIndex = slotIndex;
                    SaveSettings();
                    Invalidate();
                }
            }
        }

        private void AssignTargetFolder(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return;
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Target Folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    targetSlots[slotIndex].Path = SanitizePath(fbd.SelectedPath);
                    targetSlots[slotIndex].Label = ""; // Clear custom label on new assignment
                    activeSlotIndex = slotIndex;
                    SaveSettings();
                    Invalidate();
                }
            }
        }

        private void ClearTarget(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return;
            targetSlots[slotIndex].Path = "";
            targetSlots[slotIndex].Label = "";
            SaveSettings();
            Invalidate();
        }

        private void LaunchProgram()
        {
            TargetSlot slot = ActiveSlot;
            string path = SanitizePath(slot.Path);
            if (string.IsNullOrEmpty(path))
            {
                AssignTargetFile(activeSlotIndex);
                return;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show("The assigned target could not be found:\n" + path, "Target Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PlayClickSound();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = path;
                psi.UseShellExecute = true;
                
                if (File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        psi.WorkingDirectory = dir;
                    }
                }
                else if (Directory.Exists(path))
                {
                    psi.WorkingDirectory = path;
                }

                Process.Start(psi);
                if (autoClose)
                {
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch target: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlayClickSound()
        {
            try
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    int sampleRate = 44100;
                    double duration = 0.12;
                    int numSamples = (int)(sampleRate * duration);
                    
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write((int)(36 + numSamples * 2));
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    bw.Write((int)16);
                    bw.Write((short)1);
                    bw.Write((short)1);
                    bw.Write((int)sampleRate);
                    bw.Write((int)(sampleRate * 2));
                    bw.Write((short)2);
                    bw.Write((short)16);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    bw.Write((int)(numSamples * 2));
                    
                    Random rand = new Random();
                    for (int i = 0; i < numSamples; i++)
                    {
                        double t = (double)i / sampleRate;
                        double freq = 120.0 - 80.0 * (t / duration);
                        double sine = Math.Sin(2.0 * Math.PI * freq * t);
                        double noise = (rand.NextDouble() * 2.0 - 1.0) * Math.Exp(-120.0 * t);
                        double env = Math.Exp(-12.0 * t);
                        double sampleVal = (sine * 0.65 + noise * 0.35) * env;
                        short val = (short)(Math.Max(-1.0, Math.Min(1.0, sampleVal)) * 32767);
                        bw.Write(val);
                    }
                    bw.Flush();
                    ms.Position = 0;
                    using (var player = new System.Media.SoundPlayer(ms))
                    {
                        player.PlaySync();
                    }
                }
            }
            catch { }
        }

        private static string SanitizeSingleLine(string input, int maxLength = 128)
        {
            if (string.IsNullOrEmpty(input)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(Math.Min(input.Length, maxLength));
            foreach (char c in input)
            {
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    sb.Append(' ');
                }
                else if (!char.IsControl(c))
                {
                    sb.Append(c);
                }
                if (sb.Length >= maxLength) break;
            }
            return sb.ToString().Trim();
        }

        private static string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            string trimmed = path.Trim().Trim('"', '\'');
            
            char[] invalidChars = Path.GetInvalidPathChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
            {
                if (c != '\r' && c != '\n' && !char.IsControl(c) && Array.IndexOf(invalidChars, c) < 0)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        private string GetConfigPath()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir, "settings.txt");
        }

        private void LoadSettings()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    if (lines.Length > 0)
                    {
                        int slotIdx;
                        if (lines.Length >= 9 && int.TryParse(lines[0], out slotIdx) && slotIdx >= 0 && slotIdx < 4)
                        {
                            activeSlotIndex = slotIdx;
                            for (int i = 0; i < 4; i++)
                            {
                                int lIdx = 1 + i * 2;
                                if (lIdx < lines.Length) targetSlots[i].Path = SanitizePath(lines[lIdx]);
                                if (lIdx + 1 < lines.Length) targetSlots[i].Label = SanitizeSingleLine(lines[lIdx + 1]);
                            }
                            if (lines.Length > 10)
                            {
                                bool parsedAutoClose;
                                if (bool.TryParse(lines[10], out parsedAutoClose))
                                {
                                    autoClose = parsedAutoClose;
                                }
                            }
                        }
                        else
                        {
                            // Backward compatibility with previous single-target settings
                            targetSlots[0].Path = SanitizePath(lines[0]);
                            if (lines.Length > 1) targetSlots[0].Label = SanitizeSingleLine(lines[1]);
                            activeSlotIndex = 0;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                string path = GetConfigPath();

                bool hasAnyData = false;
                for (int i = 0; i < 4; i++)
                {
                    targetSlots[i].Path = SanitizePath(targetSlots[i].Path);
                    targetSlots[i].Label = SanitizeSingleLine(targetSlots[i].Label);
                    if (!string.IsNullOrEmpty(targetSlots[i].Path) || !string.IsNullOrEmpty(targetSlots[i].Label))
                    {
                        hasAnyData = true;
                    }
                }

                if (!hasAnyData && activeSlotIndex == 0 && !autoClose)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    return;
                }

                string[] lines = new string[11];
                lines[0] = activeSlotIndex.ToString();
                for (int i = 0; i < 4; i++)
                {
                    lines[1 + i * 2] = targetSlots[i].Path;
                    lines[1 + i * 2 + 1] = targetSlots[i].Label;
                }
                lines[9] = "1.000";
                lines[10] = autoClose.ToString();

                File.WriteAllLines(path, lines);
            }
            catch { }
        }
    }
}
