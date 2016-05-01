using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Libs.VMath;
using Common.Libs.MatrixMath;
using OpenTK.Graphics.OpenGL;

namespace MeshFlowViewer
{
    public class Camera
    {
        private Vec3f Position = new Vec3f(0,1,0);
        private Vec3f Forward = Vec3f.Y;
        private Vec3f Up = Vec3f.Z;
        private Vec3f NaturalUp = Vec3f.Z;
        private Vec3f Target = Vec3f.Zero;

        public bool Ortho { set; get; }
        public double Near { set; get; }
        public double Far { set; get; }
        public double Width { set; get; }
        public double Height { set; get; }
        public double FOV { set; get; }
        public double Scale { set; get; }

        protected Quatf qrot;
        protected float dist;

        public Quatf GetRotation() { return qrot; }
        public float GetDistance() { return dist; }

        public Vec3f GetPosition() { return Position; }
        public Vec3f GetTarget() { return Target; }
        public Vec3f GetForward() { return Forward; }
        public Vec3f GetUp() { return Up; }
        public Vec3f GetNaturalUp() { return NaturalUp; }
        public Vec3f GetRight() { return Forward ^ Up; }

        public Camera()
        {
            Ortho = false;
            Near = 0.5f;
            Far = 10000.0f;
            //Width = 1200;
            //Height = 1000;
            FOV = 80;
            Scale = 1.0f;
        }

        public Matrix GetTransformationMatrix()
        {
            Matrix lookat = Matrix.LookAt(Position, Target, Up);
            if (Ortho) return lookat * Matrix.Scale(Scale);
            return lookat * Matrix.Scale(100.0);
        }

        public Matrix GetProjectionMatrix()
        {
            if (Ortho) return Matrix.Orthographic(FOV, Width / Height, -Far, Far);
            return Matrix.Perspective2(FOV, Width / Height / 1.5, Near, Far); 
        }

        #region TMP: GL matrix functions 
        //private Matrix DoubleArrayToMatrix(double[] m)
        //{
        //    return new Matrix(new double[,] {
        //        { m[00], m[04], m[08], m[12] },
        //        { m[01], m[05], m[09], m[13] },
        //        { m[02], m[06], m[10], m[14] },
        //        { m[03], m[07], m[11], m[15] }
        //    });
        //}

        //public Matrix GetGLModelMatrix()
        //{
        //    double[] m = new double[16];
        //    GL.GetDouble(GetPName.ModelviewMatrix, m);
        //    return DoubleArrayToMatrix(m);
        //}

        //public Matrix GetGLProjectionMatrix()
        //{
        //    double[] m = new double[16];
        //    GL.GetDouble(GetPName.ProjectionMatrix, m);
        //    return DoubleArrayToMatrix(m);
        //}
        #endregion

        public void Set(Vec3f tar, Quatf rot, float dist)
        {
            rot = rot.Normalize();
            this.qrot = rot;
            this.dist = dist;
            Vec3f _forward = rot.Rotate(Vec3f.Y);
            Vec3f _up = rot.Rotate(Vec3f.Z);
            Position = tar - _forward * dist;
            Scale = 50.0f / dist;
            Forward = Vec3f.Normalize(_forward);
            Target = tar;
            Up = _up;
        }

        public void Set(Vec3f tar, Quatf rot, float dist, bool ortho)
        {
            Set(tar, rot, dist);
            Ortho = ortho;
        }

        public void Set(Camera c)
        {
            Set(c.GetTarget(),c.GetRotation(),c.GetDistance(),c.Ortho);
        }

        public void Reset()
        {
            Set(new Vec3f(), Quatf.AxisAngleToQuatf(Vec3f.Z, -45) * Quatf.AxisAngleToQuatf(Vec3f.Y, -45), 10, false);
        }

        public void RotatecAboutAxis(Vec3f axis, double theta_degrees)
        {
            double theta = theta_degrees * Math.PI / 180;
            Vec3f tar = Quatf.AxisAngleToQuatf(axis, (float)theta).Rotate(Forward * dist) + Position;
            Set(tar, qrot * Quatf.AxisAngleToQuatf(axis, (float)theta), dist);
        }
        public void Pan(double theta_degrees) { RotatecAboutAxis(NaturalUp, theta_degrees); }
        public void Tilt(double theta_degrees) { RotatecAboutAxis(GetRight(), theta_degrees); }

        #region Camera Truck functions
        public void DollyIntoTarget(double percentage)
        {
            percentage /= Scale;
            Set(Target, qrot, dist * 1.0f - (float)percentage);
        }

        public void Truck(double dright, double dup)
        {
            Vec3f truck = GetRight() * (float)dright + Up * (float)dup;
            Set(Target + truck, qrot, dist);
        }

        public void TruckRightForward(double dright, double dfwd)
        {
            Vec3f truck = GetRight() * (float)dright + Forward * (float)dfwd;
            Set(Target + truck, qrot, dist);
        }

        public void TruckXY(double dx, double dy)
        {
            Vec3f truck = Vec3f.X * (float)dx + Vec3f.Y * (float)dy;
            Set(Target + truck, qrot, dist);
        }

        public void OrbitTarget(double theta_degrees)
        {
            double theta = theta_degrees * Math.PI / 180.0;
            Quatf newrot = qrot * Quatf.AxisAngleToQuatf(NaturalUp, (float)theta);
            Set(Target, newrot, dist);
        }
        
        public void OrbitTargetUpDown(double theta_degrees, bool clampflips=false)
        {
            double theta = theta_degrees * Math.PI / 180.0;
            Quatf newrot = qrot * Quatf.AxisAngleToQuatf(GetRight(), (float)theta);

            //if (clampflips)
            //{               
            //    Vec3f newup = Vec3f.Normalize(newrot.Rotate(Vec3f.Y));
            //    float dot = FMath.PI / 2.0f - Vec3f.AngleBetween(newup, NaturalUp);
            //    if (dot < 0)
            //    {
            //        Vec3f newforward = Vec3f.Normalize(newrot.Rotate(-Vec3f.Z));
            //        Vec3f newright = newforward ^ newup;
            //        float sign = -Math.Sign(newforward % NaturalUp);
            //        newrot = newrot * Quatf.RotAxisAngleToQuatf(newright, dot * sign);
            //    }
            //}

            Set(Target, newrot, dist);
        }
        #endregion
    }
}
