using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;


namespace MeshFlowViewer
{
    class Timeline : Control
    {
        protected float currentRatio = 0.0f;
        protected enum MouseState { move, normal };
        protected MouseState mouseState = MouseState.normal;

        // const UI variables
        int background_x = 20;
        int background_y = 15;
        int currentIndex_x = 15;
        int currentIndex_y = 5;

        // brushes
        protected Brush brTimelineColor = new SolidBrush(Color.FromArgb(192, 0, 0, 0));
        protected Brush brCurrentTime = new SolidBrush(Color.FromArgb(192, 255, 255, 0));
        protected Brush brBckColor = new SolidBrush(Color.FromArgb(255, 128, 128, 128));
        protected Pen penCurrentTimeLine = new Pen(Color.FromArgb(255, 255, 0, 0), 2);

        // delegate for changing / synchronizing timeline viewer window
        public delegate void TimeLineIndexChangedDelegate(float ratio);
        public event TimeLineIndexChangedDelegate TimeLineIndexChanged;

        public Timeline()
        {
            InitializeControl();

            //ViewerControl.viewerControl.CurrentIndexChanged += SetCurrentIndex;
        }

        public void AddCurrentIndexChangedDelegate(ref ViewerControl.CurrentIndexChangedDelegate CurrentIndexChanged)
        {
            CurrentIndexChanged += SetCurrentIndex;
        }

        public void SetCurrentIndex(float ratio)
        {
            currentRatio = ratio;
            //this.Invoke((MethodInvoker)delegate { Invalidate(); });
            this.Refresh();
            //RefreshControl();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            Graphics g = e.Graphics;

            DrawBackground(g);
            
            DrawCurrentIndex(g);  
        }

        private void DrawBackground(Graphics g)
        {
            g.FillRectangle(brTimelineColor, 0, 0, Width, Height);
            g.FillRectangle(brBckColor, background_x, background_y, Width - 2 * background_x, Height - 2 * background_y);
        }

        private void DrawCurrentIndex(Graphics g)
        {
            int left = background_x + (int)((Width - 2 * background_x) * currentRatio);
            g.DrawLine(penCurrentTimeLine, new Point(left, 0), new Point(left, Height));
            g.FillRectangle(brCurrentTime, left - currentIndex_x / 2, currentIndex_y, currentIndex_x, Height - 2 * currentIndex_y);
        }

        public void InitializeControl()
        {
            CreateHandle();
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            MinimumSize = new Size(50, 50);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100;
            const int WM_SYSKEYDOWN = 0x104;

            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                switch (keyData)
                {
                    case Keys.Tab:
                        Console.WriteLine("tab pressed in timeline control");
                        return true;
                    //default:

                }
            }

            //return false;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            mouseState = MouseState.move;
            base.OnMouseDown(e);

        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            mouseState = MouseState.normal;
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if(mouseState == MouseState.move)
            {
                Console.WriteLine("mousePos:" + e.X + "," + e.Y);
                TimeLineSetRatio(e.X);
                TimeLineIndexChanged(currentRatio);
            }
                
             
            base.OnMouseMove(e);
        }

        public void TimeLineSetRatio(int x)
        {
            if (x < background_x)
                currentRatio = 0;
            else if (x >= Width - background_x)
                currentRatio = 1;
            else
            {
                currentRatio = (float)(x-background_x)/(float)(Width-2*background_x);
            }
            RefreshControl();
        }

        #region refresh helper functions
        public void RefreshControl()
        {
            this.Invoke((MethodInvoker)InvokeHelper);
            //this.Invoke((MethodInvoker)delegate { Invalidate(); });
        }

        private void InvokeHelper()
        {
            Invalidate(true);
        }
        #endregion
    }
}
