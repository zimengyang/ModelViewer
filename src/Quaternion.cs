using System;
using Common.Libs.VMath;
using Common.Libs.MatrixMath;

namespace MeshFlowViewer
{
    [Serializable]
    public struct Quatf
    {
        public static Quatf Identity = new Quatf(1, 0, 0, 0);

        private float x, y, z, w;
        public float Scalar { get { return w; } }
        public Vec3f Vector { get { return new Vec3f(x, y, z); } }
        public Vec4f Vector4 { get { return new Vec4f(x, y, z, w); } }

        public Quatf(float w, float x, float y, float z) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Quatf(Quatf q) { x = q.x; y = q.y; z = q.z; w = q.w; }
        public Quatf(float w, Vec3f v) { x = v.x; y = v.y; z = v.z; this.w = w; }

        public float Length { get { return FMath.Sqrt(x * x + y * y + z * z + w * w); } }
        public float LengthSqr { get { return x * x + y * y + z * z + w * w; } }

        public string ToStringWXYZ()
        {
            return string.Format("<{0:0.00000},{1:0.00000},{2:0.00000},{3:0.00000}>", w, x, y, z);
        }

        public static Quatf operator +(Quatf q1, Quatf q2) { return new Quatf(q1.w + q2.w, q1.x + q2.x, q1.y + q2.y, q1.z + q2.z); }
        public static Quatf operator -(Quatf q1, Quatf q2) { return new Quatf(q1.w - q2.w, q1.x - q2.x, q1.y - q2.y, q1.z - q2.z); }
        public static Quatf operator *(Quatf q, float s) { return new Quatf(q.w * s, q.x * s, q.y * s, q.z * s); }
        public static Quatf operator *(Quatf q1, Quatf q2)
        {
            return new Quatf()
            {
                w = q1.w * q2.w - q1.x * q2.x - q1.y * q2.y - q1.z * q2.z,
                x = q1.w * q2.x + q1.x * q2.w + q1.y * q2.z - q1.z * q2.y,
                y = q1.w * q2.y - q1.x * q2.z + q1.y * q2.w + q1.z * q2.x,
                z = q1.w * q2.z + q1.x * q2.y - q1.y * q2.x + q1.z * q2.w,
            };
        }
        public static Quatf operator /(Quatf q1, Quatf q2) { return q1 * q2.Inverse(); }
        public static Quatf operator /(Quatf q, float s) { return new Quatf(q.w / s, q.x / s, q.y / s, q.z / s); }

        public static float operator %(Quatf q0, Quatf q1) { return q0.x * q1.x + q0.y * q1.y + q0.z * q1.z + q0.w * q1.w; }

        public static Vec3f Quat_PointMult(Quatf q1, Quatf q2)
        {
            return new Vec3f()
            {
                x = q1.w * q2.x + q1.x * q2.w + q1.y * q2.z - q1.z * q2.y,
                y = q1.w * q2.y - q1.x * q2.z + q1.y * q2.w + q1.z * q2.x,
                z = q1.w * q2.z + q1.x * q2.y - q1.y * q2.x + q1.z * q2.w
            };
        }

        public static Vec3f Quat_RotPt(Vec3f pt, Vec3f rotaxis, float theta)
        {
            Quatf qrot = new Quatf(FMath.Cos(theta * 0.5f), rotaxis * FMath.Sin(theta * 0.5f));
            Quatf qpt = new Quatf(0.0f, pt);
            Quatf qconj = qrot.Conjugate();

            return Quat_PointMult(qrot * qpt, qconj);
        }

        public Vec3f Rotate(Vec3f v)
        {
            float[] m = Normalize().ToMatrix16f();

            float vx = v.x * m[0] + v.y * m[4] + v.z * m[8] + m[12];
            float vy = v.x * m[1] + v.y * m[5] + v.z * m[9] + m[13];
            float vz = v.x * m[2] + v.y * m[6] + v.z * m[10] + m[14];
            float vw = m[15];
            return new Vec3f(vx / vw, vy / vw, vz / vw);
        }

        public static Quatf RotAxisAngleToQuatf(Vec3f axis, float theta)
        {
            float c = FMath.Cos(theta * 0.5f);
            axis *= FMath.Sin(theta * 0.5f);
            return (new Quatf(c, axis)).Normalize();
        }

        public static Quatf Lerp(Quatf q0, Quatf q1, float t)
        {
            return (q0 * (1.0f - t) + q1 * t).Normalize();
        }

        public static Quatf Slerp(Quatf q0, Quatf q1, float t)
        {
            Quatf q2;
            float dot = q0 % q1;

            if (dot < 0.00f) { dot = -dot; q2 = q1 * -1.0f; }
            else q2 = q1;

            if (dot < 0.95f)
            {
                float ang = FMath.Acos(dot);
                return (q0 * FMath.Sin(ang * (1.0f - t)) + q2 * FMath.Sin(ang * t)) / FMath.Sin(ang);
            }
            else return Lerp(q0, q2, t);
        }

        public static Quatf Slerp_NoInvert(Quatf q0, Quatf q1, float t)
        {
            float dot = q0 % q1;

            if (dot > -0.95f && dot < 0.95f)
            {
                float ang = FMath.Acos(dot);
                return (q0 * FMath.Sin(ang * (1.0f - t)) + q1 * FMath.Sin(ang * t)) / FMath.Sin(ang);
            }
            else return Lerp(q0, q1, t);
        }

        public Quatf Conjugate() { return new Quatf(w, -x, -y, -z); }
        public Quatf Normalize() { return this / Length; }
        public Quatf Inverse() { return this.Conjugate() * (1.0f / LengthSqr); }

        public float[] ToMatrix16f()
        {
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;

            return new float[] {
                1.0f - 2.0f * ( yy * zz ), 2.0f * (xy - wz), 2.0f * (xz + wy), 0.0f,
                2.0f * (xy + wz), 1.0f - 2.0f * (xx + zz), 2.0f * (yz - wx), 0.0f,
                2.0f * (xz - wy), 2.0f * (yz + wx), 1.0f - 2.0f * (xx + yy), 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f
            };
        }

        public Vec3f ToEuler()
        {
            float phi = FMath.Atan2(2.0f * (x * y + z * w), 1.0f - 2.0f * (y * y + z * z));
            float theta = FMath.Asin(2.0f * (x * z - w * y));
            float psi = FMath.Atan2(2.0f * (x * w + y * z), 1.0f - 2.0f * (z * z + w * w));
            return new Vec3f(phi, theta, psi);
        }

        public static Quatf MatrixToQuatf(Matrix m)
        {
            double tr = m[0, 0] + m[1, 1] + m[2, 2] + m[3, 3];
            double w, x, y, z;
            if (tr > 0)
            {
                w = Math.Sqrt(m[0, 0] + m[1, 1] + m[2, 2] + m[3, 3]) / 2.0;
                double w4 = 4.0 * w;
                x = (m[1, 2] - m[2, 1]) / w4;
                y = (m[2, 0] - m[0, 2]) / w4;
                z = (m[0, 1] - m[1, 0]) / w4;

            }

            if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2])
            {
                double s = Math.Sqrt(m[0, 0] - m[1, 1] - m[2, 2] + m[3, 3]) * 2;
                w = (m[1, 2] - m[2, 1]) / s;
                x = 0.25 * s;
                y = (m[1, 0] + m[0, 1]) / s;
                z = (m[0, 2] + m[2, 0]) / s;
            }
            else if (m[1, 1] > m[0, 0] && m[1, 1] > m[2, 2])
            {
                double s = Math.Sqrt(m[1, 1] - m[0, 0] - m[2, 2] + m[3, 3]) * 2;
                w = (m[2, 0] - m[0, 2]) / s;
                x = (m[1, 0] + m[0, 1]) / s;
                y = 0.25 * s;
                z = (m[2, 1] + m[1, 2]) / s;
            }
            else {
                double s = Math.Sqrt(m[2, 2] - m[0, 0] - m[1, 1] + m[3, 3]) * 2;
                w = (m[0, 1] - m[1, 0]) / s;
                x = (m[2, 0] + m[0, 2]) / s;
                y = (m[2, 1] + m[1, 2]) / s;
                z = 0.25 * s;
            }
            return new Quatf((float)w, (float)x, (float)y, (float)z);
        }

        public static Quatf EulerToQuatf(Vec3f ang) { return EulerToQuatf(ang.x, ang.y, ang.z); }

        public static Quatf EulerToQuatf(float alpha, float beta, float gamma)
        {
            float c1 = FMath.Cos(alpha / 2.0f);
            float s1 = FMath.Sin(alpha / 2.0f);
            float c2 = FMath.Cos(beta / 2.0f);
            float s2 = FMath.Sin(beta / 2.0f);
            float c3 = FMath.Cos(gamma / 2.0f);
            float s3 = FMath.Sin(gamma / 2.0f);

            return new Quatf()
            {
                w = c1 * c2 * c3 - s1 * s2 * s3,
                x = c1 * c2 * s3 + s1 * s2 * c3,
                y = s1 * c2 * c3 + c1 * s2 * s3,
                z = c1 * s2 * c3 - s1 * c2 * s3
            };
        }

        public static Quatf AxisAngleToQuatf(Vec3f axis, float angle) { return new Quatf(FMath.Cos(angle), axis * FMath.Sin(angle)); }
    }
}