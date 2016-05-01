using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using OpenTK.Graphics.OpenGL;
using Common.Libs.VMath;
using Common.Libs.MiscFunctions;
using Common.Libs.MatrixMath;

namespace MeshFlowViewer
{
    [Serializable]
    public class CameraProperties : IBinaryConvertible
    {
        public string Name { get; set; }

        public PropertyBag Properties = new PropertyBag();

        private Property<Vec3f> Position = new Property<Vec3f>("Position", new Vec3f(0, -1, 0));
        private PropertyValidated<Vec3f> Forward = new PropertyValidated<Vec3f>("Forward", Vec3f.Y, NormalizeVector);
        private PropertyValidated<Vec3f> Up = new PropertyValidated<Vec3f>("Up", Vec3f.Z, NormalizeVector);
        private PropertyValidated<Vec3f> NaturalUp = new PropertyValidated<Vec3f>("Natural Up", Vec3f.Z, NormalizeVector);
        private Property<Vec3f> Target = new Property<Vec3f>("TargetDistance", Vec3f.Zero);

        public PropertyBool Ortho = new PropertyBool("Orthographic Projection", false);
        public Property<double> Near = new Property<double>("Near", 0.5);
        public Property<double> Far = new Property<double>("Far", 10000.0);
        public Property<double> Width = new Property<double>("Width", 600);
        public Property<double> Height = new Property<double>("Height", 600);
        public Property<double> FOV = new Property<double>("FOV", 80);
        public Property<double> Scale = new Property<double>("Scale", 1.0);                                         // only affects orthographic projection

        public PropertyBool AlwaysUp = new PropertyBool("Always Up", true);

        private CameraProperties SyncToCamera;
        private bool Synchronizing;

        protected Quatf qrot;
        protected float dist;

        public Quatf GetRotation() { return qrot; }
        public float GetDistance() { return dist; }
        public bool GetOrtho() { return Ortho.Val; }

        private bool bIgnoreChanges = false;

        private static Vec3f NormalizeVector(Vec3f newvec, Vec3f oldvec) { return Vec3f.Normalize(newvec); }

        public Vec3f GetPosition() { return Position.Val; }
        public Vec3f GetTarget() { return Target.Val; }
        public Vec3f GetForward() { return Forward.Val; }
        public Vec3f GetUp() { return Up.Val; }
        public Vec3f GetNaturalUp() { return NaturalUp.Val; }
        public Vec3f GetRight() { return Forward.Val ^ Up.Val; }

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    //bw.WriteParams( Position.Val, Forward.Val, Up.Val, NaturalUp.Val, Target.Val );
        //    bw.WriteT(Target.Val);
        //    bw.WriteParams(Ortho.Val);
        //    bw.WriteParams(Near.Val, Far.Val, Width.Val, Height.Val, FOV.Val, Scale.Val);
        //    bw.WriteT(qrot);
        //    bw.Write(dist);
        //}

        public void ReadBinary(BinaryReader br)
        {
            //br.ReadProperty( Position );
            //br.ReadProperty( Forward );
            //br.ReadProperty( Up );
            //br.ReadProperty( NaturalUp );
            br.ReadProperty(Target);
            br.ReadProperty(Ortho);
            br.ReadProperty(Near);
            br.ReadProperty(Far);
            br.ReadProperty(Width);
            br.ReadProperty(Height);
            br.ReadProperty(FOV);
            br.ReadProperty(Scale);
            br.Read(out qrot);
            br.Read(out dist);
        }

        public static CameraProperties ReadBinaryFile(BinaryReader br)
        {
            CameraProperties cam = new CameraProperties();
            cam.ReadBinary(br);
            return cam;
        }

        public void Sync(CameraProperties cam)
        {
            if (cam == null || cam == this) { Desync(); return; }
            if (cam == this.SyncToCamera && cam.SyncToCamera == this) return;

            Desync();
            cam.Desync();

            this.SyncToCamera = cam;
            cam.SyncToCamera = this;

            cam.Properties.PropertyChanged += this.Synchronize;
            this.Properties.PropertyChanged += cam.Synchronize;

            Synchronize(null, null);
        }

        public void Desync()
        {
            CameraProperties that = SyncToCamera;
            if (that == null) return;

            that.Properties.PropertyChanged -= this.Synchronize;
            this.Properties.PropertyChanged -= that.Synchronize;

            that.SyncToCamera = null;
            this.SyncToCamera = null;
        }

        private void Synchronize(object obj, PropertyChangedEventArgs e)
        {
            if (SyncToCamera.Synchronizing) return;

            Synchronizing = true;
            Properties.DeferPropertyChanged = true;

            Position.Val = SyncToCamera.Position.Val;
            Forward.Val = SyncToCamera.Forward.Val;
            Up.Val = SyncToCamera.Up.Val;
            NaturalUp.Val = SyncToCamera.NaturalUp.Val;
            Target.Val = SyncToCamera.Target.Val;
            Ortho.Val = SyncToCamera.Ortho.Val;
            Near.Val = SyncToCamera.Near.Val;
            Far.Val = SyncToCamera.Far.Val;
            Width.Val = SyncToCamera.Width.Val;
            Height.Val = SyncToCamera.Height.Val;
            FOV.Val = SyncToCamera.FOV.Val;
            Scale.Val = SyncToCamera.Scale.Val;
            qrot = SyncToCamera.qrot;
            dist = SyncToCamera.dist;

            Properties.DeferPropertyChanged = false;
            Synchronizing = false;
        }

        public Matrix GetTransformationMatrix()
        {
            Matrix lookat = Matrix.LookAt(Position.Val, Target.Val, Up.Val);
            if (Ortho) return lookat * Matrix.Scale(Scale.Val);
            return lookat * Matrix.Scale(100.0);
        }

        public Matrix GetProjectionMatrix()
        {
            if (Ortho) return Matrix.Orthographic(FOV, Width / Height, -Far, Far);
            return Matrix.Perspective2(FOV, Width / Height / 1.5, Near, Far); //Width / Height / 3.0
        }

        private Matrix DoubleArrayToMatrix(double[] m)
        {
            return new Matrix(new double[,] {
                { m[00], m[04], m[08], m[12] },
                { m[01], m[05], m[09], m[13] },
                { m[02], m[06], m[10], m[14] },
                { m[03], m[07], m[11], m[15] }
            });
        }
        public Matrix GetGLModelMatrix()
        {
            double[] m = new double[16];
            GL.GetDouble(GetPName.ModelviewMatrix, m);
            return DoubleArrayToMatrix(m);
        }
        public Matrix GetGLProjectionMatrix()
        {
            double[] m = new double[16];
            GL.GetDouble(GetPName.ProjectionMatrix, m);
            return DoubleArrayToMatrix(m);
        }

        public List<Vec3f> Unproject(double sx, double sy)
        {
            double x = sx / Width.Val * 2.0 - 1.0;
            double y = sy / Height.Val * -2.0 + 1.0;

            Matrix matModel = GetTransformationMatrix().TransposeSelf();
            Matrix matProj = GetProjectionMatrix().TransposeSelf();
            Matrix inv = Matrix.Invert(matProj * matModel);

            Matrix msnear = new Matrix(new double[] { x, y, 0.0, 1.0 }, true);      // should be -1.0 instead of 0.0?
            Matrix msfar = new Matrix(new double[] { x, y, 1.0, 1.0 }, true);

            Matrix mnear = inv * msnear; mnear /= mnear[3, 0];
            Matrix mfar = inv * msfar; mfar /= mfar[3, 0];

            Vec3f near = new Vec3f((float)mnear[0, 0], (float)mnear[1, 0], (float)mnear[2, 0]);
            Vec3f far = new Vec3f((float)mfar[0, 0], (float)mfar[1, 0], (float)mfar[2, 0]);

            return new List<Vec3f>(new Vec3f[] { near, far });
        }

        public CameraProperties()
        {
            Properties.AddProperty(Position);
            Properties.AddProperty(Forward);
            Properties.AddProperty(Target);
            Properties.AddProperty(Up);
            Properties.AddProperty(Near);
            Properties.AddProperty(Far);
            Properties.AddProperty(Ortho);
            Properties.AddProperty(Width);
            Properties.AddProperty(Height);
            Properties.AddProperty(FOV);
            Properties.AddProperty(Scale);
        }

        public CameraProperties(Vec3f tar, Quatf rot, float dist, bool ortho) : this() { Set(tar, rot, dist, ortho); }

        public static CameraProperties SmoothGaussian(CameraProperties[] cams, int t0, float sigma)
        {
            int l = cams.Length;
            float wsum = 0;
            float ss = sigma * sigma;

            Vec3f t = new Vec3f();
            Quatf r = new Quatf(0, 0, 0, 0);
            float d = 0;

            t0 = Math.Max(0, Math.Min(cams.Length - 1, t0));

            // window to ~99%
            int s = Math.Max(0, (int)((float)t0 - sigma * 3.0f));
            int e = Math.Min(l - 1, (int)((float)t0 + sigma * 3.0f));

            for (int ti = s; ti <= e; ti++)
            {
                CameraProperties cam = cams[ti];

                float dtim = (ti - t0);
                float w = (float)Math.Exp(-dtim * dtim / ss);
                wsum += w;

                t += cam.Target.Val * w;
                r += cam.qrot * w;
                d += cam.dist * w;
            }

            t *= 1.0f / wsum;
            r *= 1.0f / wsum;
            d *= 1.0f / wsum;

            if (t0 < 0 || t0 >= l) System.Console.WriteLine("t0 = " + t0 + ", l = " + l);

            return new CameraProperties(t, r, d, cams[t0].Ortho.Val);
        }
        public static CameraProperties SmoothBilateral(CameraProperties[] cams, int t0, float sigmatime, float sigmax)
        {
            int l = cams.Length;
            float wsumt = 0;
            float wsumr = 0;
            float wsumd = 0;
            float sigtimesq = sigmatime * sigmatime;
            float sigxsq = sigmax * sigmax;

            Vec3f tar0 = cams[t0].GetTarget();
            Quatf rot0 = cams[t0].qrot;
            float dis0 = cams[t0].dist;

            Vec3f t = new Vec3f();
            Quatf r = new Quatf(0, 0, 0, 0);
            float d = 0;

            // window to ~99%
            float siglg = Math.Max(sigmatime, sigmax);
            int s = Math.Max(0, (int)((float)t0 - siglg * 3.0f));
            int e = Math.Min(l - 1, (int)((float)t0 + siglg * 3.0f));

            for (int ti = s; ti <= e; ti++)
            {
                CameraProperties cam = cams[ti];

                float dtim = (ti - t0);
                float dtar = (cam.GetTarget() - tar0).Length;
                float drot = (cam.qrot - rot0).Length;
                float ddis = (cam.dist - dis0);

                float wg = (float)Math.Exp(-dtim * dtim / sigtimesq);
                float wt = wg * (float)Math.Exp(-dtar * dtar / sigxsq);
                float wr = wg * (float)Math.Exp(-drot * drot / sigxsq);
                float wd = wg * (float)Math.Exp(-ddis * ddis / sigxsq);

                wsumt += wt;
                wsumr += wr;
                wsumd += wd;

                t += cam.Target.Val * wt;
                r += cam.qrot * wr;
                d += cam.dist * wd;
            }

            t *= 1.0f / wsumt;
            r *= 1.0f / wsumr;
            d *= 1.0f / wsumd;

            return new CameraProperties(t, r, d, cams[t0].Ortho.Val);
        }

        public static CameraProperties SmoothBinary(CameraProperties[] cams, int t0, float epsilon, bool fromt0, float wtar, float wrot, float wdis)
        {
            int total = cams.Length;

            int left = t0 - 1;
            bool lcdone = (left < 0);
            Vec3f lctar = cams[t0].GetTarget();
            Quatf lcrot = cams[t0].GetRotation();
            float lcdis = cams[t0].GetDistance();
            float leftdiff = -1;

            int right = t0 + 1;
            bool rcdone = (right > total - 1);
            Vec3f rctar = cams[t0].GetTarget();
            Quatf rcrot = cams[t0].GetRotation();
            float rcdis = cams[t0].GetDistance();
            float rightdiff = -1;

            Vec3f avgtar = cams[t0].GetTarget();
            Quatf avgrot = cams[t0].GetRotation();
            float avgdis = cams[t0].GetDistance();
            float maxdis = cams[t0].GetDistance();
            float avgortho = (cams[t0].GetOrtho() ? 1.0f : 0.0f);
            int count = 1;

            while (!lcdone || !rcdone)
            {
                if (!lcdone)
                {
                    Vec3f ltar = cams[left].GetTarget();
                    Quatf lrot = cams[left].GetRotation();
                    float ldis = cams[left].GetDistance();

                    leftdiff = (ltar - lctar).Length * wtar + (lrot - lcrot).Length * wrot + Math.Abs(ldis - lcdis) * wdis;

                    if (leftdiff < epsilon)
                    {
                        avgtar += ltar;
                        avgrot += lrot;
                        avgdis += ldis;
                        maxdis = Math.Max(maxdis, ldis);
                        avgortho += (cams[left].GetOrtho() ? 1.0f : 0.0f);
                        count++;

                        if (!fromt0) { lctar = ltar; lcrot = lrot; lcdis = ldis; }

                        left--;
                        lcdone = (left < 0);
                    }
                    else {
                        lcdone = true;
                    }
                }

                if (!rcdone)
                {
                    Vec3f rtar = cams[right].GetTarget();
                    Quatf rrot = cams[right].GetRotation();
                    float rdis = cams[right].GetDistance();

                    rightdiff = (rtar - rctar).Length * wtar + (rrot - rcrot).Length * wrot + Math.Abs(rdis - rcdis) * wdis;

                    if (rightdiff < epsilon)
                    {
                        avgtar += rtar;
                        avgrot += rrot;
                        avgdis += rdis;
                        maxdis = Math.Max(maxdis, rdis);
                        avgortho += (cams[right].GetOrtho() ? 1.0f : 0.0f);
                        count++;

                        if (!fromt0) { rctar = rtar; rcrot = rrot; rcdis = rdis; }

                        right++;
                        rcdone = (right > total - 1);
                    }
                    else {
                        rcdone = true;
                    }
                }
            }

            avgtar /= (float)count;
            avgrot /= (float)count;
            avgdis /= (float)count;
            avgortho /= (float)count;

            avgdis = maxdis;

            return new CameraProperties(avgtar, avgrot, avgdis, (avgortho >= 0.5f));
        }

        public static CameraProperties[] GetSmoothedCameras_Gaussian(CameraProperties[] original, float sigmat)
        {
            CameraProperties[] smoothedcams = new CameraProperties[original.Length];
            for (int i = 0; i < original.Length; i++)
                smoothedcams[i] = SmoothGaussian(original, i, sigmat);
            return smoothedcams;
        }

        public static CameraProperties[] GetSmoothedCameras_Bilateral(CameraProperties[] original, float sigmat, float sigmax)
        {
            CameraProperties[] smoothedcams = new CameraProperties[original.Length];
            for (int i = 0; i < original.Length; i++)
                smoothedcams[i] = SmoothBilateral(original, i, sigmat, sigmax);
            return smoothedcams;
        }

        public static CameraProperties GetSpecificCamera(CameraProperties[] cameras, ViewSelections viewsel)
        {
            switch (viewsel)
            {
                case ViewSelections.Artist: return cameras[0];
                case ViewSelections.BestView: return cameras[1];
                case ViewSelections.User: return cameras[2];
                default: throw new Exception("Unimplemented ViewSelection: " + viewsel);
            }
        }

        public void Resize(double width, double height)
        {
            Properties.DeferPropertyChanged = true;
            Width.Set(width);
            Height.Set(height);
            Properties.DeferPropertyChanged = false;
        }

        public void Set(Vec3f tar, Quatf rot, float dist, bool ortho)
        {
            Properties.DeferPropertyChanged = true;
            Set(tar, rot, dist);
            Ortho.Set(ortho);
            Properties.DeferPropertyChanged = false;
        }
        public void Set(Vec3f tar, Quatf rot, float dist)
        {
            rot = rot.Normalize();
            /*if( AlwaysUp )
			{
				// prevent camera from rolling
				Vec3f newright = Vec3f.Normalize( rot.Rotate( Vec3f.X ) );
				Vec3f newup = Vec3f.Normalize( rot.Rotate( Vec3f.Y ) );
				Vec3f newforward = Vec3f.Normalize( rot.Rotate( -Vec3f.Z ) );
				
				if( Math.Abs( newforward % NaturalUp ) < 0.95f ) {
					Vec3f goodright = Vec3f.Normalize( newforward ^ Vec3f.Z );
					float dot = Vec3f.AngleBetween( goodright, newright );
					System.Console.WriteLine( newup.ToStringFormatted() + "  " + newright.ToStringFormatted() + "  " + goodright.ToStringFormatted() );
					float sign = -Math.Sign( newright % NaturalUp );
					//rot = rot * Quatf.RotAxisAngleToQuatf( newforward, dot * sign );
				}
			}*/

            this.qrot = rot;
            this.dist = dist;

            Properties.DeferPropertyChanged = true;
            bIgnoreChanges = true;

            Vec3f fwd = rot.Rotate(-Vec3f.Z);
            Vec3f up = rot.Rotate(Vec3f.Y);

            Position.Set(tar - fwd * dist);
            Scale.Set(50.0f / dist);
            Target.Set(tar);
            Forward.Set(Vec3f.Normalize(fwd));
            Up.Set(up);

            bIgnoreChanges = false;
            Properties.DeferPropertyChanged = false;
        }

        public void Set(CameraProperties camera)
        {
            Set(camera.GetTarget(), camera.GetRotation(), camera.GetDistance(), camera.GetOrtho());
        }

        /// <summary>
        /// Pan the Camera around the Up vector by <c>theta_degrees</c> degrees.
        /// </summary>
        /// <param name="theta_degrees">
        /// A <see cref="System.Double"/>
        /// </param>
        public void RotatecAboutAxis(Vec3f axis, double theta_degrees)
        {
            double theta = theta_degrees * Math.PI / 180;
            Vec3f tar = Quatf.AxisAngleToQuatf(axis, (float)theta).Rotate(Forward.Val * dist) + Position.Val;
            Set(tar, qrot * Quatf.AxisAngleToQuatf(axis, (float)theta), dist);
        }
        public void Pan(double theta_degrees) { RotatecAboutAxis(NaturalUp.Val, theta_degrees); }
        public void Tilt(double theta_degrees) { RotatecAboutAxis(GetRight(), theta_degrees); }

        /// <summary>
        /// Orbit Camera Position around Target, maintaining distance to Target and the Up vector
        /// </summary>
        /// <param name="theta_degrees">
        /// A <see cref="System.Double"/>
        /// </param>
        public void OrbitTarget(double theta_degrees)
        {
            double theta = theta_degrees * Math.PI / 180.0;
            Quatf newrot = qrot * Quatf.AxisAngleToQuatf(NaturalUp.Val, (float)theta);
            Set(Target.Val, newrot, dist);
        }

        /*public void OrbitTargetUpDownNatural( double theta_degrees )
		{
			float fn = Forward.Val % NaturalUp.Val;
			if( ( theta_degrees < 0.0 && fn >= 0.95f ) || ( theta_degrees > 0.0 && fn <= -0.95f ) ) return;
			
			double theta = theta_degrees * Math.PI / 180.0;
			Vec3f posreltar = Position.Val - Target.Val;
			Vec3f right = Forward.Val ^ NaturalUp.Val;
			float dist = (Target.Val - Position.Val).Length;
			
			Properties.DeferPropertyChanged = true;
			bIgnoreChanges = true;
			Position.Set( Target.Val + Vec3f.Normalize( VecExtensions.RotateVectorAroundAxis( posreltar, right, (float) theta ) ) * dist );
			Up.Set( VecExtensions.RotateVectorAroundAxis( Up.Val, right, (float) theta ) );
			bIgnoreChanges = false;
			Target.Set( Target.Val );
			Properties.DeferPropertyChanged = false;
		}*/

        public void OrbitTargetUpDown(double theta_degrees, bool clampflips)
        {
            double theta = theta_degrees * Math.PI / 180.0;
            Quatf newrot = qrot * Quatf.AxisAngleToQuatf(GetRight(), (float)theta);

            if (clampflips)
            {
                // prevent camera from flipping over!
                Vec3f newup = Vec3f.Normalize(newrot.Rotate(Vec3f.Y));
                float dot = FMath.PI / 2.0f - Vec3f.AngleBetween(newup, NaturalUp.Val);
                if (dot < 0)
                {
                    Vec3f newforward = Vec3f.Normalize(newrot.Rotate(-Vec3f.Z));
                    Vec3f newright = newforward ^ newup;
                    float sign = -Math.Sign(newforward % NaturalUp.Val);
                    newrot = newrot * Quatf.RotAxisAngleToQuatf(newright, dot * sign);
                }
            }

            Set(Target.Val, newrot, dist);
        }

        /// <summary>
        /// Truck Position towards Target 
        /// </summary>
        /// <param name="percentage">
        /// A <see cref="System.Double"/> representing the ratio of the distance the Camera will move.
        /// </param>
        public void DollyIntoTarget(double percentage)
        {
            percentage /= Scale.Val;
            Set(Target.Val, qrot, dist * 1.0f - (float)percentage);
        }

        public void Truck(double dright, double dup)
        {
            Vec3f truck = GetRight() * (float)dright + Up.Val * (float)dup;
            Set(Target.Val + truck, qrot, dist);
        }

        public void TruckRightForward(double dright, double dfwd)
        {
            Vec3f truck = GetRight() * (float)dright + Forward.Val * (float)dfwd;
            Set(Target.Val + truck, qrot, dist);
        }

        public void TruckXYAxis(double dx, double dy)
        {
            Vec3f truck = Vec3f.X * (float)dx + Vec3f.Y * (float)dy;
            Set(Target.Val + truck, qrot, dist);
        }
    }
}







