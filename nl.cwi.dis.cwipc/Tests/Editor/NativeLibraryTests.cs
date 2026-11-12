using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Cwipc.Tests
{
    /// <summary>
    /// Minimal tests that the native cwipc libraries can be found and used from C#.
    /// They need cwipc installed on the machine, like the package itself.
    /// See cwi-dis/cwipc_unity#63.
    /// </summary>
    public class NativeLibraryTests
    {
        const int NPoints = 10000;

        static cwipc.pointcloud GetSyntheticPointcloud()
        {
            cwipc.source src = cwipc.synthetic(0, NPoints);
            Assert.IsNotNull(src, "cwipc.synthetic() returned null");
            Assert.IsTrue(src.start(), "synthetic source did not start");
            Assert.IsTrue(src.available(true), "synthetic source has no point cloud available");
            cwipc.pointcloud pc = src.get();
            src.stop();
            src.free();
            Assert.IsNotNull(pc, "synthetic source returned no point cloud");
            return pc;
        }

        [Test]
        public void SyntheticSourceProducesPointcloud()
        {
            // This is also the "native library loads" test: cwipc.synthetic() loads cwipc_util.
            cwipc.pointcloud pc = GetSyntheticPointcloud();
            try
            {
                Assert.Greater(pc.count(), 0, "synthetic point cloud is empty");
            }
            finally
            {
                pc.free();
            }
        }

        [Test]
        public void EncodeDecodeRoundTrip()
        {
            cwipc.pointcloud pc = GetSyntheticPointcloud();
            int originalCount = pc.count();
            cwipc.encoder_params par = new cwipc.encoder_params
            {
                do_inter_frame = false,
                gop_size = 1,
                exp_factor = 0,
                octree_bits = 9,
                jpeg_quality = 85,
                macroblock_size = 16,
                tilenumber = 0,
                voxelsize = 0,
                n_parallel = 1
            };
            cwipc.encoder enc = cwipc.new_encoder(par);
            cwipc.decoder dec = cwipc.new_decoder();
            IntPtr buffer = IntPtr.Zero;
            try
            {
                enc.feed(pc);
                Assert.IsTrue(enc.available(true), "encoder produced no data");
                int size = enc.get_encoded_size();
                Assert.Greater(size, 0, "encoded data is empty");
                buffer = Marshal.AllocHGlobal(size);
                Assert.IsTrue(enc.copy_data(buffer, size), "encoder copy_data failed");

                dec.feed(buffer, size);
                Assert.IsTrue(dec.available(true), "decoder produced no point cloud");
                cwipc.pointcloud decoded = dec.get();
                Assert.IsNotNull(decoded, "decoder returned no point cloud");
                try
                {
                    // Octree quantization merges points, so expect fewer, but not none.
                    Assert.Greater(decoded.count(), 0, "decoded point cloud is empty");
                    Assert.LessOrEqual(decoded.count(), originalCount, "decoded point cloud has more points than the original");
                }
                finally
                {
                    decoded.free();
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                enc.free();
                dec.free();
                pc.free();
            }
        }
    }
}
