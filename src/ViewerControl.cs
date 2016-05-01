using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Windows.Forms;
using System.Drawing;

using Common.Libs.VMath;
using Common.Libs.MatrixMath;

// openTk graphics namespace conflicts, using needed properties only
using ColorFormat = OpenTK.Graphics.ColorFormat;
using GraphicsMode = OpenTK.Graphics.GraphicsMode;
using GraphicsContextFlags = OpenTK.Graphics.GraphicsContextFlags;

namespace MeshFlowViewer
{
    public enum CameraStates
    {
        Idle,
        Zoom,
        Truck,
        TruckXY,
        TruckRightForward,
        PanAndTilt,
        Orbit,
        OrbitAndDolly
    }

    public class ViewerControl : GLControl
    {
        //public static ViewerControl viewerControl;

        #region OpenGL informations
        public static ColorFormat m_GLColorFormat = new ColorFormat(24);
        public static int m_GLDepth = 24;
        public static int m_GLStencil = 8;
        public static int m_GLSamples = 0;
        public static bool m_GLStereo = false;
        #endregion

        //private System.Timers.Timer m_RefreshTimer = new System.Timers.Timer(5);
        //public bool m_IsRendering { get; private set; }

        //camera matrix 
        protected Matrix m_CamProj;
        protected Matrix m_CamTrans;
        protected Matrix m_CamMatrix;
        protected Matrix m_CamUnproject;

        //camera
        protected Camera m_Camera = new Camera();

        // button status
        protected bool b_ControlPressed = false;
        protected bool b_ShiftPressed;

        // mouse last position
        protected Point m_LastMouseLocation;
        protected CameraStates m_CameraState = CameraStates.Idle;

        // modeling history & viewables
        protected ModelingHistory hist;
        protected IndexedViewableAlpha[] viewables;

        // currentClusterIndex
        protected int currentClusterLayer = 0;
        protected int currentClusterIndex = 0;
        List<Cluster> clusters;

        // wire frame render option
        protected bool wireFrameRender = false;
        protected bool AutoPlay = false;
        public void ToggleAutoPlay() { AutoPlay = !AutoPlay; RefreshControl(); }
        public void StartAutoPlay() { AutoPlay = true; RefreshControl(); }
        public void PauseAutoplay() { AutoPlay = false; RefreshControl(); }
        public void Next() { currentClusterIndex++; SetCluster(); }
        public void Prev() { currentClusterIndex--; SetCluster(); }

        // delegate for changing / synchronizing timeline
        public delegate void CurrentIndexChangedDelegate(float ratio);
        public event CurrentIndexChangedDelegate CurrentIndexChanged;


        public ViewerControl(ModelingHistory history) : 
            base(new GraphicsMode(m_GLColorFormat, m_GLDepth, m_GLStencil, m_GLSamples, 48, 2, m_GLStereo), 3, 0, GraphicsContextFlags.Debug)
        {
            hist = history;
            
            //clusters = hist.Layers.GetClusters();
            clusters = hist.Layers.GetClusteringLayer(currentClusterLayer).clusters;

            this.SuspendLayout();
            this.BackColor = System.Drawing.Color.DimGray;
            this.ResumeLayout(false);

            base.CreateControl();
            
            this.Cursor = Cursors.Cross;
            VSync = false;

            //setup viewables 
            SetCluster();

            //setup camera 
            m_Camera.Width = Width;
            m_Camera.Height = Height;
            m_Camera.Set(new Vec3f(),Quatf.AxisAngleToQuatf(Vec3f.Z,-45),10,false);
            
            //m_RefreshTimer.Elapsed += delegate { RefreshControl(); };
        }

        public void SetModelingHistory(ModelingHistory history)
        {
            this.hist = history;
        }

        protected void ClampHelper(ref int x,int _min,int _max,bool loop=false)
        {
          
            if (x >= _max)
                x = loop?_min:_max-1;
            if (x < _min)
                x = loop?_max-1:_min;
        }

        public void SetCluster(bool loop = false)
        {
            // update currentClusterLayer
            ClampHelper(ref currentClusterLayer, 0, hist.Layers.nlevels);
            //clusters = hist.Layers.GetClusteringLayer(currentClusterLayer).clusters;
            clusters = hist.Layers.GetClusteringLayer(currentClusterLayer).GetClusters();

            // update currentClusterIndex
            ClampHelper(ref currentClusterIndex, 0, clusters.Count,loop);

            viewables = hist.GetViewables(clusters[currentClusterIndex], false, false, false, false);

            // fire currenttime changed event
            if(CurrentIndexChanged != null)
                CurrentIndexChanged((float)(currentClusterIndex) /(float)(clusters.Count-1));

            Console.WriteLine("layer index = {0}, cluster index = {1}.", currentClusterLayer, currentClusterIndex);
            RefreshControl();
        }

        public void SetClusterIndex(float ratio)
        {
            currentClusterIndex = (int)(ratio * clusters.Count);
//            currentClusterLayer = _currentClusterLayer;

            // update currentClusterLayer
            ClampHelper(ref currentClusterLayer, 0, hist.Layers.nlevels);
            //clusters = hist.Layers.GetClusteringLayer(currentClusterLayer).clusters;
            clusters = hist.Layers.GetClusteringLayer(currentClusterLayer).GetClusters();

            // update currentClusterIndex
            ClampHelper(ref currentClusterIndex, 0, clusters.Count, false);

            viewables = hist.GetViewables(clusters[currentClusterIndex], true, true, true, true);

            Console.WriteLine("layer index = {0}, cluster index = {1}.", currentClusterLayer, currentClusterIndex);
            RefreshControl();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UpdateAutoPlay();

            base.OnPaint(e);
            MakeCurrent();

            InitializeRender();
            //SetCluster();
            RenderData();
           
            SwapBuffers();
            //m_IsRendering = false;
        }

        private void UpdateAutoPlay()
        {
            if (!AutoPlay)
                return;
            else
            {
                currentClusterIndex++;
                Thread.Sleep(50);
                SetCluster(true); 
            }
        }

        private void RenderData()
        {
            // clear 
            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

            // set viewport and render viewable
            GL.Viewport(0, 0, Width, Height);
            //DrawCube();
            //RenderViewable(hist.DeEmphasizeNonHighlighted(viewables[0], Color.DimGray), wireFrameRender);
            if(viewables.Count() > 0)
                RenderViewable(viewables[0], wireFrameRender);

            // render Gizmo
            GL.Viewport(0, 0, 150, (int)(150.0f * Height / Width));
            DrawRotationGizmo();
            //Refresh();
        }

        private void InitializeRender()
        {
            // set clear color 
            GL.ClearColor(BackColor);

            // enable functions
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.PointSmooth);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.Blend);
            GL.ShadeModel(ShadingModel.Smooth);

            // blend functions
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            // hint functions
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            // load projection and modelview matrix to openGL
            m_CamProj = m_Camera.GetProjectionMatrix();
            m_CamTrans = m_Camera.GetTransformationMatrix();
            m_CamMatrix = m_CamTrans * m_CamProj;
            m_CamUnproject = Matrix.Invert(m_CamMatrix);

            GL.MatrixMode(MatrixMode.Projection);
            Matrix4d mProj4d = m_CamProj.ToOpenTKMatrix4d();
            GL.LoadMatrix(ref mProj4d);

            GL.MatrixMode(MatrixMode.Modelview);
            Matrix4d mTran4d = m_CamTrans.ToOpenTKMatrix4d();
            GL.LoadMatrix(ref mTran4d);

        }

        #region Mouse handle functions
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
            Cursor = Cursors.Cross;
            RefreshControl();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            RefreshControl();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            b_ControlPressed = ((ModifierKeys & Keys.Control) != 0);
            b_ShiftPressed =   ((ModifierKeys & Keys.Shift) != 0);

            m_LastMouseLocation = e.Location;

            switch (e.Button)
            {
                case MouseButtons.Left:
                    if (b_ShiftPressed)
                    {
                        if (b_ControlPressed)
                            m_CameraState = CameraStates.TruckXY;
                        else
                            m_CameraState = CameraStates.Truck;
                    }
                    else
                        m_CameraState = CameraStates.Orbit;
                    break;
                case MouseButtons.Right:
                    m_CameraState = CameraStates.TruckRightForward;
                    break;
                case MouseButtons.Middle:
                    m_CameraState = CameraStates.OrbitAndDolly;
                    break;
                default:
                    break;
            }

        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            m_CameraState = CameraStates.Idle;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            //zoom
            m_Camera.DollyIntoTarget(e.Delta * 0.1f);
            RefreshControl();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            HandleMouse(e);

            m_LastMouseLocation = e.Location;
        }

        protected void HandleMouse(MouseEventArgs e)
        {
            if (m_CameraState == CameraStates.Idle)
                return;

            int dx = e.X - m_LastMouseLocation.X;
            int dy = e.Y - m_LastMouseLocation.Y;

            switch (m_CameraState)
            {
                case CameraStates.Truck:
                    m_Camera.Truck(dx * -0.01, dy * 0.01);
                    RefreshControl();
                    break;
                case CameraStates.TruckXY:
                    m_Camera.TruckXY(dx * -0.01, dy * 0.01);
                    RefreshControl();
                    break;
                case CameraStates.TruckRightForward:
                    m_Camera.TruckRightForward(dx * -0.01, dy * -0.01);
                    RefreshControl();
                    break;
                case CameraStates.PanAndTilt:
                    m_Camera.Pan(dx);
                    m_Camera.Tilt(dy);
                    RefreshControl();
                    break;
                case CameraStates.OrbitAndDolly:
                    m_Camera.OrbitTarget(dx);
                    m_Camera.DollyIntoTarget(-dy);
                    RefreshControl();
                    break;
                case CameraStates.Orbit:
                    m_Camera.OrbitTarget(dx);
                    m_Camera.OrbitTargetUpDown(dy);
                    RefreshControl();
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Keyboard process functions
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100;
            const int WM_SYSKEYDOWN = 0x104;

            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {

                switch (keyData)
                {
                    case Keys.Escape:
                        Application.Exit();
                        break;
                    case Keys.R:
                        m_Camera.Reset();
                        RefreshControl();
                        break;
                    case Keys.Left:
                        currentClusterIndex--;
                        SetCluster();
                        break;
                    case Keys.Right:
                        currentClusterIndex++;
                        SetCluster();
                        break;
                    case Keys.Up:
                        currentClusterLayer++;
                        SetCluster();
                        break;
                    case Keys.Down:
                        currentClusterLayer--;
                        SetCluster();
                        break;
                    case Keys.W:
                        wireFrameRender = !wireFrameRender;
                        RefreshControl();
                        break;
                    case Keys.P:
                        //hist.PrintLayers();
                        AutoPlay = !AutoPlay;
                        RefreshControl();
                        break;
                    case Keys.I:
                        hist.PrintLayers();
                        break;
                    default:
                        break;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        #endregion

        #region refresh helper functions
        public void RefreshControl()
        {
            this.Invoke((MethodInvoker) InvokeHelper);
        }

        private void InvokeHelper()
        {
            Invalidate(true);
        }
        #endregion

        #region projection helper functions

        public Vec3f Project3DTo2D(Vec3f pt) { return MatVecMult(m_CamMatrix, pt); }
        public Vec3f Unproject2DTo3D(Vec3f pt) { return MatVecMult(m_CamUnproject, pt); }
        private Vec3f MatVecMult(Matrix mat, Vec3f v)
        {
            double x = mat[0, 0] * v.x + mat[1, 0] * v.y + mat[2, 0] * v.z + 1 * mat[3, 0];
            double y = mat[0, 1] * v.x + mat[1, 1] * v.y + mat[2, 1] * v.z + 1 * mat[3, 1];
            double z = mat[0, 2] * v.x + mat[1, 2] * v.y + mat[2, 2] * v.z + 1 * mat[3, 2];
            double w = mat[0, 3] * v.x + mat[1, 3] * v.y + mat[2, 3] * v.z + 1 * mat[3, 3];

            return new Vec3f((float)(x / w), (float)(y / w), (float)(z / w));
        }
        #endregion

        #region Draw Functions
        protected void DrawCube()
        {
            GL.Begin(BeginMode.Quads);

            GL.Color3(Color.Red);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);

            GL.Color3(Color.Green);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);

            GL.Color3(Color.Blue);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);

            GL.Color3(Color.Yellow);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);

            GL.Color3(Color.Magenta);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);

            GL.Color3(Color.Violet);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);

            GL.End();
        }

        protected void DrawLine(Vec3f p1, Vec3f c1, Vec3f p2, Vec3f c2) 
        {
            GL.Begin(BeginMode.Lines);
                GL.Color3(c1.r, c1.g, c1.b);
                GL.Vertex3(p1.x, p1.y, p1.z);
                GL.Color3(c2.r, c2.g, c2.b);
                GL.Vertex3(p2.x, p2.y, p2.z);
            GL.End();
        }

        protected void DrawRotationGizmo()
        {
            float size = 2.0f;
            float scale = (m_Camera.Ortho ? (8.0f / (float)m_Camera.Scale) : (1.0f / 600.0f));

            Vec3f center = Unproject2DTo3D(new Vec3f(0.0f, 0.0f, -0.5f));
            Vec3f xaxis = Vec3f.X * size * scale + center;
            Vec3f yaxis = Vec3f.Y * size * scale + center;
            Vec3f zaxis = Vec3f.Z * size * scale + center;
            GL.LineWidth(1.0f);
            DrawLine(center, Vec3f.X, xaxis, Vec3f.X);
            DrawLine(center, Vec3f.Y, yaxis, Vec3f.Y);
            DrawLine(center, Vec3f.Z, zaxis, Vec3f.Z);
        }

        #endregion


        #region Render viewables function
        protected void RenderViewable(IndexedViewableAlpha v) { RenderViewable(v, false); }
        protected void RenderViewable(IndexedViewableAlpha v, bool wireframe)
        {
            BeginMode mode;

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);

            while (v != null)
            {

                GL.VertexPointer(3, VertexPointerType.Float, 0, v.Verts);
                int indsetcount = v.Indices.Length;
                for (int iIndSet = 0; iIndSet < indsetcount; iIndSet++)
                {
                    int[] indset = v.Indices[iIndSet];
                    int count = indset.Count();
                    if (count == 0) continue;

                    switch (v.GroupSizes[iIndSet])
                    {
                        case 1: mode = BeginMode.Points; break;
                        case 2: mode = BeginMode.Lines; break;
                        case 3: mode = BeginMode.Triangles; break;
                        case 4: mode = BeginMode.Quads; break;
                        default: throw new ArgumentException("ViewerControl.RenderViewable: Unimplemented group size: " + v.GroupSizes[iIndSet]);
                    }

                    if (wireframe && v.GroupSizes[iIndSet] > 2) continue;

                    GL.ColorPointer(4, ColorPointerType.Float, 0, v.Colors[iIndSet]);

                    if (v.PointSizes != null)
                    {
                        float mult = Math.Min((float)m_Camera.Scale / 6.0f, 2.0f);
                        if (m_Camera.Ortho)
                            GL.PointSize(v.PointSizes[iIndSet] * mult);
                        else
                            GL.PointSize(v.PointSizes[iIndSet]);
                    }
                    if (v.LineWidths != null)
                    {
                        GL.LineWidth(v.LineWidths[iIndSet]);
                    }

                    GL.DrawElements(mode, count, DrawElementsType.UnsignedInt, indset);
                }

                v = v.attached;
            }

            GL.DisableClientState(ArrayCap.ColorArray);
            GL.DisableClientState(ArrayCap.VertexArray);

            GL.PointSize(2.5f);

        }

        #endregion

        public void UpCurrentLayer()
        {
            currentClusterLayer++;
            SetCluster();
        }

        public void DownCurrentLayer()
        {
            currentClusterLayer--;
            SetCluster();
        }
    }
}
