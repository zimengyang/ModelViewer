using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

namespace MeshFlowViewer
{
    #region test set/get property
    //class TMP
    //{
    //    private float a;
    //    public float A
    //    {
    //        get { return a; }
    //        set { a = value; }
    //    }

    //    public float b { set; get; }

    //    public void print()
    //    {
    //        Console.WriteLine("a = " + A.ToString());
    //        Console.WriteLine("b = " + b.ToString());
    //    }
    //}

    //TMP tmp = new TMP()
    //{
    //    A = 10.0f,
    //    b = 100.0f
    //};
    //tmp.print();
    #endregion

    #region lamda expression example
    //List<int> m_list = new List<int>() { 10,20,30};

    //m_list.ForEach( delegate(int val)
    //{
    //     Console.WriteLine(val + " delegate.");
    //});

    //m_list.ForEach(x => Console.WriteLine(x + " lamda expression."));
    #endregion

    class Program
    {
        public static string m_TVMeshFilename = null;
        public static ModelingHistory hist;

        private static void ParseArgs(string[] args)
        {
            //m_TVMeshFilename = Path.Combine(Directory.GetCurrentDirectory(),args[0]);
            m_TVMeshFilename = args[0];

            Console.WriteLine("tvm file path: {0}", m_TVMeshFilename);
            if (!File.Exists(m_TVMeshFilename))
                Console.WriteLine("specified tvm file not existed!");
            else
            {
                //Console.WriteLine("tvm file path correct, file found.");
                Console.WriteLine("loading from binary file:" + m_TVMeshFilename);

                using (FileStream fs = new FileStream(m_TVMeshFilename, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(fs);
                    br.Read(out hist);
                }

                if (hist == null)
                {
                    Console.WriteLine("Could not load binary file.");
                }
                else
                {
                    hist.AddDefaultClusterLayers();
                }
            }
        }

        static void Main(string[] args)
        {
            // parse command line arguments
            ParseArgs(args);

            // run the main form window here
            MyForm testForm = new MyForm(hist);
            
            Application.Run(testForm);

            //System.Console.ReadKey();
        }
    }
}
