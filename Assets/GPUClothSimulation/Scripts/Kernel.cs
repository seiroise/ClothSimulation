using UnityEngine;

namespace Seiro.ClothSimulation.GPU
{
    public class Kernel
    {
        public int index;
        public uint threadX;
        public uint threadY;
        public uint threadZ;

        Kernel(ComputeShader shader, string key)
        {
            index = shader.FindKernel(key);
            if (index < 0)
            {
                Debug.LogWarningFormat("Can't find {0} kernel.", key);
                return;
            }
            shader.GetKernelThreadGroupSizes(index, out threadX, out threadY, out threadZ);
        }

        public static implicit operator int(Kernel src)
        {
            return src.index;
        }

        public static implicit operator bool(Kernel src)
        {
            return src != null;
        }

        public static bool TryGetKernel(ComputeShader shader, string key, out Kernel dst)
        {
            dst = null;

            if (!shader)
            {
                return false;
            }

            var index = shader.FindKernel(key);
            if (index < 0)
            {
                return false;
            }

            dst = new Kernel(shader, key);
            return true;
        }
    }
}