using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace ObjLoader.Services.Mmd.Parsers
{
    public static class VmdParser
    {
        private const int HeaderSize = 30;
        private const int ModelNameSize = 20;
        private const int BoneNameSize = 15;
        private const int MorphNameSize = 15;

        public static VmdData Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var data = new VmdData();

            var headerBytes = br.ReadBytes(HeaderSize);
            var header = Encoding.ASCII.GetString(headerBytes).TrimEnd('\0');

            if (!header.StartsWith("Vocaloid Motion Data"))
                return data;

            var modelNameBytes = br.ReadBytes(ModelNameSize);
            data.ModelName = Encoding.GetEncoding(932).GetString(modelNameBytes).TrimEnd('\0');

            if (fs.Position >= fs.Length) return data;

            uint boneFrameCount = br.ReadUInt32();
            for (uint i = 0; i < boneFrameCount; i++)
            {
                var boneNameBytes = br.ReadBytes(BoneNameSize);
                int boneNameEnd = Array.IndexOf(boneNameBytes, (byte)0);
                if (boneNameEnd < 0) boneNameEnd = BoneNameSize;
                string boneName = Encoding.GetEncoding(932).GetString(boneNameBytes, 0, boneNameEnd).Trim();

                uint frameNumber = br.ReadUInt32();

                float px = br.ReadSingle();
                float py = br.ReadSingle();
                float pz = br.ReadSingle();

                float rx = br.ReadSingle();
                float ry = br.ReadSingle();
                float rz = br.ReadSingle();
                float rw = br.ReadSingle();

                byte[] interpolationBytes = br.ReadBytes(64);
                Interpolation64 interpolation = default;
                interpolationBytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(new Span<Interpolation64>(ref interpolation)));

                data.BoneFrames.Add(new VmdBoneFrame
                {
                    BoneName = boneName,
                    FrameNumber = frameNumber,
                    Position = new Vector3(px, py, pz),
                    Rotation = new Quaternion(rx, ry, rz, rw),
                    Interpolation = interpolation
                });
            }

            if (fs.Position >= fs.Length) return data;

            uint morphFrameCount = br.ReadUInt32();
            for (uint i = 0; i < morphFrameCount; i++)
            {
                var morphNameBytes = br.ReadBytes(MorphNameSize);
                int morphNameEnd = Array.IndexOf(morphNameBytes, (byte)0);
                if (morphNameEnd < 0) morphNameEnd = MorphNameSize;
                string morphName = Encoding.GetEncoding(932).GetString(morphNameBytes, 0, morphNameEnd).Trim();

                uint frameNumber = br.ReadUInt32();
                float weight = br.ReadSingle();

                data.MorphFrames.Add(new VmdMorphFrame
                {
                    MorphName = morphName,
                    FrameNumber = frameNumber,
                    Weight = weight
                });
            }

            if (fs.Position >= fs.Length) return data;

            uint cameraFrameCount = br.ReadUInt32();
            for (uint i = 0; i < cameraFrameCount; i++)
            {
                uint frameNumber = br.ReadUInt32();
                float distance = br.ReadSingle();

                float cx = br.ReadSingle();
                float cy = br.ReadSingle();
                float cz = br.ReadSingle();

                float crx = br.ReadSingle();
                float cry = br.ReadSingle();
                float crz = br.ReadSingle();

                byte[] interpolationBytes = br.ReadBytes(24);
                Interpolation24 interpolation = default;
                interpolationBytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(new Span<Interpolation24>(ref interpolation)));

                uint viewAngle = br.ReadUInt32();
                byte perspective = br.ReadByte();

                data.CameraFrames.Add(new VmdCameraFrame
                {
                    FrameNumber = frameNumber,
                    Distance = distance,
                    Position = new Vector3(cx, cy, cz),
                    Rotation = new Vector3(crx, cry, crz),
                    Interpolation = interpolation,
                    ViewAngle = viewAngle,
                    IsOrthographic = perspective != 0
                });
            }

            return data;
        }
    }
}