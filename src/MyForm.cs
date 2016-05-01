using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using OpenTK;


namespace MeshFlowViewer
{
    class MyForm : Form
    {
        protected TableLayoutPanel tlp;
        protected ViewerControl viewer;
        protected Timeline timeline;
        protected ModelingHistory hist;

        // tool strip buttons 
        protected ToolStripButton tsbPlay;
        protected ToolStripButton tsbPause;
        protected ToolStripButton tsbUpLayer;
        protected ToolStripButton tsbDownLayer;
        protected ToolStripButton tsbNext;
        protected ToolStripButton tsbPrev;

        public MyForm(ModelingHistory history)
        {
            // set modeling history
            hist = history;

            // set tlp 
            InitializeTableLayoutPanel();

            // add toolstrip to tlp
            InitializeToolStrip();

            //add viewer control to form
            InitializeViewControl();

            // add timeline control to form
            InitializeTimeline();

            // Add changed influence on timeline with control
            //timeline.AddCurrentIndexChangedDelegate(ref viewer.CurrentIndexChanged);
            viewer.CurrentIndexChanged += timeline.SetCurrentIndex;
            timeline.TimeLineIndexChanged += viewer.SetClusterIndex;

            // add items to the main window (form)
            this.Text = "Mesh Flow Viewer";
            this.Size = new Size(800, 800);
            
            //this.KeyPreview = true;
            this.Controls.Add(tlp);
        }

        private void InitializeViewControl()
        {
            viewer = new ViewerControl(hist)
            {
                Dock = DockStyle.Fill,
                Width = this.Width,
                Height = this.Height
            };
            //ViewerControl.viewerControl = viewer;
            //viewer.SetModelingHistory(hist);
            tlp.Controls.Add(viewer);
        }

        private void InitializeTimeline()
        {
            timeline = new Timeline()
            {
                Dock = DockStyle.Fill,
                Height = 50
            };
            timeline.Padding = timeline.Margin = new Padding(0, 0, 0, 0);
            tlp.Controls.Add(timeline);
        }

        private void InitializeToolStrip()
        {
            ToolStrip ts = new ToolStrip();
            ts.GripStyle = ToolStripGripStyle.Hidden; ;
            ts.Dock = DockStyle.Fill;
            ts.RenderMode = ToolStripRenderMode.Professional;

            // add toolstripbutton
            tsbPlay = CreateToolStrip("play.png","",delegate { viewer.StartAutoPlay(); });
            tsbPause = CreateToolStrip("pause.png", "", delegate { viewer.PauseAutoplay(); });
            tsbUpLayer = CreateToolStrip("up.png", "", delegate { viewer.UpCurrentLayer(); });
            tsbDownLayer = CreateToolStrip("down.png", "", delegate { viewer.DownCurrentLayer(); });
            tsbNext = CreateToolStrip("next.png", "", delegate { viewer.Next(); });
            tsbPrev = CreateToolStrip("prev.png", "", delegate { viewer.Prev(); });

           
            
            ts.Items.Add(tsbPrev);
            ts.Items.Add(tsbNext);

            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(tsbPlay);
            ts.Items.Add(tsbPause);

            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(tsbUpLayer);
            ts.Items.Add(tsbDownLayer);




            tlp.Controls.Add(ts);
        }

        private void InitializeTableLayoutPanel()
        {
            tlp = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0),
                AutoSize = false,
                BackColor = Color.White,
                ForeColor = Color.White,
                ColumnCount = 1,
                RowCount = 3,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            //tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private ToolStripButton CreateToolStrip(string image, string label, EventHandler clickaction)
        {
            //string imgPath = Path.Combine(Directory.GetCurrentDirectory(),"Icons",image);
            string imgPath = Path.Combine("C:\\Projects\\ConsoleApplication1\\App\\Icons", image);
            Bitmap bitmap = new Bitmap(imgPath);
            ToolStripButton tsb = new ToolStripButton(label, bitmap);
            //tsb.ForeColor = Color.Red;
            tsb.Click += clickaction;
            return tsb;
        }
    }
}
