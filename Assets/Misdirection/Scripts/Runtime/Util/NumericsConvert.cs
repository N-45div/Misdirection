using NVector3 = System.Numerics.Vector3;
using UVector3 = UnityEngine.Vector3;

namespace Misdirection
{
    /// <summary>Converts between Unity vectors and the System.Numerics vectors used by Misdirection.Core.</summary>
    public static class NumericsConvert
    {
        public static NVector3 ToNumerics(this UVector3 v)
        {
            return new NVector3(v.x, v.y, v.z);
        }

        public static UVector3 ToUnity(this NVector3 v)
        {
            return new UVector3(v.X, v.Y, v.Z);
        }
    }
}
